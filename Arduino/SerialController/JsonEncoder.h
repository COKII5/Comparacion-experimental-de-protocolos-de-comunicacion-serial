#pragma once

#include <Arduino.h>
#include "ButtonBits.h"

constexpr size_t JSON_LINE_SIZE = 72U;

inline void sendJsonPacket(const uint16_t seq, const uint8_t buttons, const uint16_t pot, const uint16_t echo) {
  const uint8_t checksum = static_cast<uint8_t>(
      lowByte(seq) + highByte(seq) + buttons +
      lowByte(pot) + highByte(pot) +
      lowByte(echo) + highByte(echo));
  char line[JSON_LINE_SIZE];
  snprintf(line, sizeof(line),
           "{\"seq\":%u,\"b\":[%u,%u,%u,%u],\"pot\":%u,\"echo\":%u,\"ck\":%u}\n",
           static_cast<unsigned int>(seq),
           buttonBit(buttons, 0), buttonBit(buttons, 1),
           buttonBit(buttons, 2), buttonBit(buttons, 3),
           static_cast<unsigned int>(pot), static_cast<unsigned int>(echo),
           static_cast<unsigned int>(checksum));
  Serial.write(line);
}
