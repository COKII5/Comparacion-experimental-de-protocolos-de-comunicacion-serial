constexpr uint8_t BUTTON_COUNT = 4;
constexpr uint8_t BUTTON_PINS[BUTTON_COUNT] = {7, 6, 4, 5};
constexpr uint8_t POT_PIN = A0;
constexpr uint32_t BAUD_RATE = 115200UL;
constexpr uint32_t DEBOUNCE_MS = 20UL;
constexpr uint32_t DEFAULT_SEND_PERIOD_MS = 20UL;
constexpr uint8_t COMMAND_BUFFER_SIZE = 12;

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

unsigned int buttonBit(const uint8_t buttons, const uint8_t index) {
  return static_cast<unsigned int>((buttons >> index) & 0x01U);
}

void sendPacket(const uint16_t seq, const uint8_t buttons, const uint16_t pot, const uint16_t echo) {
  char line[40];
  const int written = snprintf(line, sizeof(line), "%u,%u,%u,%u,%u,%u,%u",
                               static_cast<unsigned int>(seq),
                               buttonBit(buttons, 0), buttonBit(buttons, 1),
                               buttonBit(buttons, 2), buttonBit(buttons, 3),
                               static_cast<unsigned int>(pot), static_cast<unsigned int>(echo));
  const size_t length = static_cast<size_t>(written);
  uint8_t checksum = 0;
  for (size_t i = 0; i < length; ++i) {
    checksum ^= static_cast<uint8_t>(line[i]);
  }
  snprintf(line + length, sizeof(line) - length, "*%02X\n", static_cast<unsigned int>(checksum));
  Serial.write(line);
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
    const int raw = Serial.read();
    if (raw < 0) {
      break;
    }
    const char c = static_cast<char>(raw);
    if (c == '\n' || c == '\r') {
      if (commandLength > 0U) {
        commandBuffer[commandLength] = '\0';
        executeCommand(commandBuffer);
        commandLength = 0;
      }
    } else if (commandLength < COMMAND_BUFFER_SIZE - 1U) {
      commandBuffer[commandLength] = c;
      ++commandLength;
    }
  }
}

void executeCommand(const char* command) {
  switch (command[0]) {
    case 'P':
      echoId = static_cast<uint16_t>(strtoul(command + 1, nullptr, 10));
      sendState();
      break;
    case 'T':
      sendPacket(TEST_SEQ, TEST_BUTTONS, TEST_POT, TEST_ECHO);
      break;
    case 'R':
      sendPeriodMs = static_cast<uint32_t>(strtoul(command + 1, nullptr, 10));
      break;
    case 'X':
      continuousTest = (command[1] == '1');
      break;
    default:
      break;
  }
}
