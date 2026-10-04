#include "CsvEncoder.h"
#include "JsonEncoder.h"
#include "BinaryEncoder.h"

enum class Protocol : uint8_t { Csv, Json, Binary };

constexpr Protocol ACTIVE_PROTOCOL = Protocol::Csv;

constexpr uint8_t BUTTON_COUNT = 4;
constexpr uint8_t BUTTON_PINS[BUTTON_COUNT] = {7, 6, 4, 5};
constexpr uint8_t POT_PIN = A0;
constexpr uint32_t BAUD_RATE = 115200UL;
constexpr uint32_t DEBOUNCE_MS = 20UL;
constexpr uint32_t DEFAULT_SEND_PERIOD_MS = 20UL;
constexpr uint8_t COMMAND_BUFFER_SIZE = 12;
constexpr uint32_t MAX_PING_ID = 65535UL;
constexpr uint32_t MAX_SEND_PERIOD_MS = 1000UL;
constexpr uint32_t DECIMAL_BASE = 10UL;

constexpr uint16_t TEST_SEQ = 0x7D7EU;
constexpr uint8_t TEST_BUTTONS = 0x0AU;
constexpr uint16_t TEST_POT = 0x027EU;
constexpr uint16_t TEST_ECHO = 0x007DU;

uint8_t stableButtons = 0;
uint8_t lastReading[BUTTON_COUNT] = {0, 0, 0, 0};
uint32_t lastChangeMs[BUTTON_COUNT] = {0, 0, 0, 0};
uint16_t sequenceNumber = 0;
uint16_t echoId = 0;
uint32_t sendPeriodMs = DEFAULT_SEND_PERIOD_MS;
uint32_t lastSendMs = 0;
bool continuousTest = false;
char commandBuffer[COMMAND_BUFFER_SIZE];
uint8_t commandLength = 0;
bool commandOverflow = false;

void sendPacket(const uint16_t seq, const uint8_t buttons, const uint16_t pot, const uint16_t echo) {
  switch (ACTIVE_PROTOCOL) {
    case Protocol::Csv:
      sendCsvPacket(seq, buttons, pot, echo);
      break;
    case Protocol::Json:
      sendJsonPacket(seq, buttons, pot, echo);
      break;
    case Protocol::Binary:
      sendBinaryPacket(seq, buttons, pot, echo);
      break;
  }
}

void setup() {
  Serial.begin(BAUD_RATE);
  for (uint8_t i = 0; i < BUTTON_COUNT; ++i) {
    pinMode(BUTTON_PINS[i], INPUT_PULLUP);
  }
}

void loop() {
  processCommands();
  const bool changed = readButtons();
  if (continuousTest) {
    sendPacket(TEST_SEQ, TEST_BUTTONS, TEST_POT, TEST_ECHO);
    return;
  }
  if (changed || static_cast<uint32_t>(millis()) - lastSendMs >= sendPeriodMs) {
    sendState();
  }
}

void sendState() {
  lastSendMs = static_cast<uint32_t>(millis());
  const uint16_t pot = static_cast<uint16_t>(analogRead(POT_PIN));
  sendPacket(sequenceNumber, stableButtons, pot, echoId);
  ++sequenceNumber;
}

bool readButtons() {
  bool changed = false;
  const uint32_t now = static_cast<uint32_t>(millis());
  for (uint8_t i = 0; i < BUTTON_COUNT; ++i) {
    const uint8_t reading = (digitalRead(BUTTON_PINS[i]) == LOW) ? 1U : 0U;
    if (reading != lastReading[i]) {
      lastReading[i] = reading;
      lastChangeMs[i] = now;
    } else if (now - lastChangeMs[i] >= DEBOUNCE_MS) {
      const uint8_t mask = static_cast<uint8_t>(1U << i);
      const uint8_t current = (stableButtons & mask) != 0U ? 1U : 0U;
      if (current != reading) {
        stableButtons = static_cast<uint8_t>(stableButtons ^ mask);
        changed = true;
      }
    }
  }
  return changed;
}

void processCommands() {
  while (Serial.available() > 0) {
    const int received = Serial.read();
    if (received < 0) {
      break;
    }
    const char character = static_cast<char>(received);
    if (character == '\n' || character == '\r') {
      finishCommand();
    } else if (commandLength < COMMAND_BUFFER_SIZE - 1U) {
      commandBuffer[commandLength] = character;
      ++commandLength;
    } else {
      commandOverflow = true;
    }
  }
}

void finishCommand() {
  if (commandLength > 0U && !commandOverflow) {
    commandBuffer[commandLength] = '\0';
    executeCommand(commandBuffer);
  }
  commandLength = 0;
  commandOverflow = false;
}

bool parseUnsigned(const char* text, const uint32_t maxValue, uint32_t& value) {
  if (*text == '\0') {
    return false;
  }
  uint32_t result = 0;
  for (const char* digit = text; *digit != '\0'; ++digit) {
    if (*digit < '0' || *digit > '9') {
      return false;
    }
    result = result * DECIMAL_BASE + static_cast<uint32_t>(*digit - '0');
    if (result > maxValue) {
      return false;
    }
  }
  value = result;
  return true;
}

void executeCommand(const char* command) {
  const char* argument = command + 1;
  uint32_t value = 0;
  switch (command[0]) {
    case 'P':
      if (parseUnsigned(argument, MAX_PING_ID, value)) {
        echoId = static_cast<uint16_t>(value);
        sendState();
      }
      break;
    case 'T':
      if (*argument == '\0') {
        sendPacket(TEST_SEQ, TEST_BUTTONS, TEST_POT, TEST_ECHO);
      }
      break;
    case 'R':
      if (parseUnsigned(argument, MAX_SEND_PERIOD_MS, value)) {
        sendPeriodMs = value;
      }
      break;
    case 'X':
      if (parseUnsigned(argument, 1UL, value)) {
        continuousTest = (value == 1UL);
      }
      break;
    default:
      break;
  }
}
