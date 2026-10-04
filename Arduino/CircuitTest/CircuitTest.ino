constexpr uint8_t BUTTON_COUNT = 4;
constexpr uint8_t BUTTON_PINS[BUTTON_COUNT] = {7, 6, 4, 5};
constexpr uint8_t POT_PIN = A0;
constexpr uint32_t BAUD_RATE = 115200UL;
constexpr uint32_t PRINT_PERIOD_MS = 100UL;
constexpr uint16_t POT_MAX = 1023U;
constexpr uint32_t PERCENT_SCALE = 100UL;

uint32_t lastPrintMs = 0;

void setup() {
  Serial.begin(BAUD_RATE);
  for (uint8_t i = 0; i < BUTTON_COUNT; ++i) {
    pinMode(BUTTON_PINS[i], INPUT_PULLUP);
  }
  Serial.println(F("Circuit test: B1=D7 B2=D6 B3=D4 B4=D5, potentiometer on A0"));
}

void loop() {
  const uint32_t now = static_cast<uint32_t>(millis());
  if (now - lastPrintMs < PRINT_PERIOD_MS) {
    return;
  }
  lastPrintMs = now;
  printButtons();
  printPotentiometer();
}

void printButtons() {
  for (uint8_t i = 0; i < BUTTON_COUNT; ++i) {
    const bool pressed = digitalRead(BUTTON_PINS[i]) == LOW;
    Serial.print('B');
    Serial.print(static_cast<uint8_t>(i + 1U));
    Serial.print('=');
    Serial.print(pressed ? 1 : 0);
    Serial.print(' ');
  }
}

void printPotentiometer() {
  const uint16_t pot = static_cast<uint16_t>(analogRead(POT_PIN));
  const uint32_t percent = static_cast<uint32_t>(pot) * PERCENT_SCALE / POT_MAX;
  Serial.print(F("| POT="));
  Serial.print(pot);
  Serial.print(F(" ("));
  Serial.print(percent);
  Serial.println(F("%)"));
}
