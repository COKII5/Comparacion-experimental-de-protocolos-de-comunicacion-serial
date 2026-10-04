#pragma once

#include <Arduino.h>

inline unsigned int buttonBit(const uint8_t buttons, const uint8_t index) {
  return static_cast<unsigned int>((buttons >> index) & 0x01U);
}
