#pragma once

#include <Arduino.h>
#include "ButtonBits.h"

constexpr size_t CSV_LINE_SIZE = 40U;

inline void sendCsvPacket(const uint16_t seq, const uint8_t buttons, const uint16_t pot, const uint16_t echo) {
  char line[CSV_LINE_SIZE];
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
