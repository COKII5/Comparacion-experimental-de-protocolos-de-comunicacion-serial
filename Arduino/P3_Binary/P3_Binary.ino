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

constexpr uint8_t START_OF_FRAME = 0x7EU;
constexpr uint8_t ESCAPE = 0x7DU;
constexpr uint8_t ESCAPE_XOR = 0x20U;
constexpr uint8_t TYPE_STATE = 0x01U;
constexpr uint8_t BODY_LENGTH = 11U;
constexpr uint8_t MAX_FRAME_LENGTH = 1U + 2U * BODY_LENGTH;

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

uint16_t crc16Ccitt(const uint8_t* data, const uint8_t length) {
  uint16_t crc = 0xFFFFU;
  for (uint8_t i = 0; i < length; ++i) {
    crc = static_cast<uint16_t>(crc ^ (static_cast<uint16_t>(data[i]) << 8));
    for (uint8_t bit = 0; bit < 8U; ++bit) {
      crc = (crc & 0x8000U) != 0U
                ? static_cast<uint16_t>((crc << 1) ^ 0x1021U)
                : static_cast<uint16_t>(crc << 1);
    }
  }
  return crc;
}

uint8_t appendEscaped(uint8_t* frame, uint8_t length, const uint8_t value) {
  if (value == START_OF_FRAME || value == ESCAPE) {
    frame[length] = ESCAPE;
    ++length;
    frame[length] = static_cast<uint8_t>(value ^ ESCAPE_XOR);
  } else {
    frame[length] = value;
  }
  ++length;
  return length;
}

void sendPacket(const uint16_t seq, const uint8_t buttons, const uint16_t pot, const uint16_t echo) {
  uint8_t body[BODY_LENGTH];
  body[0] = 8U;
  body[1] = TYPE_STATE;
  body[2] = static_cast<uint8_t>(seq & 0xFFU);
  body[3] = static_cast<uint8_t>(seq >> 8);
  body[4] = buttons;
  body[5] = static_cast<uint8_t>(pot & 0xFFU);
  body[6] = static_cast<uint8_t>(pot >> 8);
  body[7] = static_cast<uint8_t>(echo & 0xFFU);
  body[8] = static_cast<uint8_t>(echo >> 8);
  const uint16_t crc = crc16Ccitt(body, 9U);
  body[9] = static_cast<uint8_t>(crc & 0xFFU);
  body[10] = static_cast<uint8_t>(crc >> 8);

  uint8_t frame[MAX_FRAME_LENGTH];
  uint8_t length = 0;
  frame[length] = START_OF_FRAME;
  ++length;
  for (uint8_t i = 0; i < BODY_LENGTH; ++i) {
    length = appendEscaped(frame, length, body[i]);
  }
  Serial.write(frame, length);
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
