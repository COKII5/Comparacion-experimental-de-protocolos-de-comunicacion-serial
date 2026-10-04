#pragma once

#include <Arduino.h>

constexpr uint8_t START_OF_FRAME = 0x7EU;
constexpr uint8_t ESCAPE = 0x7DU;
constexpr uint8_t ESCAPE_XOR = 0x20U;
constexpr uint8_t TYPE_STATE = 0x01U;
constexpr uint8_t PAYLOAD_LENGTH = 8U;
constexpr uint8_t CRC_BYTES = 2U;
constexpr uint8_t BODY_LENGTH = 1U + PAYLOAD_LENGTH + CRC_BYTES;
constexpr uint8_t MAX_FRAME_LENGTH = 1U + 2U * BODY_LENGTH;
constexpr uint8_t BITS_PER_BYTE = 8U;
constexpr uint16_t CRC_INITIAL = 0xFFFFU;
constexpr uint16_t CRC_POLYNOMIAL = 0x1021U;
constexpr uint16_t CRC_TOP_BIT = 0x8000U;

inline uint16_t crc16Ccitt(const uint8_t* data, const uint8_t length) {
  uint16_t crc = CRC_INITIAL;
  for (uint8_t i = 0; i < length; ++i) {
    crc = static_cast<uint16_t>(crc ^ (static_cast<uint16_t>(data[i]) << BITS_PER_BYTE));
    for (uint8_t bit = 0; bit < BITS_PER_BYTE; ++bit) {
      crc = (crc & CRC_TOP_BIT) != 0U
                ? static_cast<uint16_t>((crc << 1) ^ CRC_POLYNOMIAL)
                : static_cast<uint16_t>(crc << 1);
    }
  }
  return crc;
}

inline uint8_t putByte(uint8_t* buffer, const uint8_t index, const uint8_t value) {
  buffer[index] = value;
  return static_cast<uint8_t>(index + 1U);
}

inline uint8_t putWord(uint8_t* buffer, const uint8_t index, const uint16_t value) {
  const uint8_t next = putByte(buffer, index, lowByte(value));
  return putByte(buffer, next, highByte(value));
}

inline uint8_t appendEscaped(uint8_t* frame, uint8_t length, const uint8_t value) {
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

inline void sendBinaryPacket(const uint16_t seq, const uint8_t buttons, const uint16_t pot, const uint16_t echo) {
  uint8_t body[BODY_LENGTH];
  uint8_t bodyLength = putByte(body, 0U, PAYLOAD_LENGTH);
  bodyLength = putByte(body, bodyLength, TYPE_STATE);
  bodyLength = putWord(body, bodyLength, seq);
  bodyLength = putByte(body, bodyLength, buttons);
  bodyLength = putWord(body, bodyLength, pot);
  bodyLength = putWord(body, bodyLength, echo);
  bodyLength = putWord(body, bodyLength, crc16Ccitt(body, bodyLength));

  uint8_t frame[MAX_FRAME_LENGTH];
  uint8_t frameLength = 0;
  frame[frameLength] = START_OF_FRAME;
  ++frameLength;
  for (uint8_t i = 0; i < bodyLength; ++i) {
    frameLength = appendEscaped(frame, frameLength, body[i]);
  }
  Serial.write(frame, frameLength);
}
