from __future__ import annotations

import sys
from dataclasses import dataclass
from typing import Callable, Final

START_OF_FRAME: Final[int] = 0x7E
ESCAPE: Final[int] = 0x7D
ESCAPE_XOR: Final[int] = 0x20
TYPE_STATE: Final[int] = 0x01
STATE_LENGTH: Final[int] = 8
BUTTON_COUNT: Final[int] = 4
BITS_PER_BYTE: Final[int] = 8
BYTE_MASK: Final[int] = 0xFF
WORD_MASK: Final[int] = 0xFFFF
CRC_INITIAL: Final[int] = 0xFFFF
CRC_POLYNOMIAL: Final[int] = 0x1021
CRC_TOP_BIT: Final[int] = 0x8000
CRC_CHECK_INPUT: Final[bytes] = b"123456789"
CRC_CHECK_VALUE: Final[int] = 0x29B1
ARGUMENT_COUNT: Final[int] = 4


@dataclass(frozen=True)
class ControllerState:
    seq: int
    buttons: int
    pot: int
    echo: int

    def bits(self) -> list[int]:
        return [(self.buttons >> index) & 1 for index in range(BUTTON_COUNT)]

    def canonical_payload(self) -> bytes:
        return bytes([
            self.seq & BYTE_MASK, self.seq >> BITS_PER_BYTE,
            self.buttons,
            self.pot & BYTE_MASK, self.pot >> BITS_PER_BYTE,
            self.echo & BYTE_MASK, self.echo >> BITS_PER_BYTE,
        ])


TEST_VALUE: Final[ControllerState] = ControllerState(seq=0x7D7E, buttons=0x0A, pot=0x027E, echo=0x007D)


def xor8(data: bytes) -> int:
    checksum: int = 0
    for value in data:
        checksum ^= value
    return checksum


def sum8(data: bytes) -> int:
    return sum(data) & BYTE_MASK


def crc16_ccitt_false(data: bytes) -> int:
    crc: int = CRC_INITIAL
    for value in data:
        crc ^= value << BITS_PER_BYTE
        for _ in range(BITS_PER_BYTE):
            crc = ((crc << 1) ^ CRC_POLYNOMIAL) if crc & CRC_TOP_BIT else (crc << 1)
            crc &= WORD_MASK
    return crc


def encode_csv(state: ControllerState) -> bytes:
    body: str = ",".join(str(field) for field in [state.seq, *state.bits(), state.pot, state.echo])
    return f"{body}*{xor8(body.encode('ascii')):02X}\n".encode("ascii")


def encode_json(state: ControllerState) -> bytes:
    checksum: int = sum8(state.canonical_payload())
    bits: str = ",".join(str(bit) for bit in state.bits())
    text: str = f'{{"seq":{state.seq},"b":[{bits}],"pot":{state.pot},"echo":{state.echo},"ck":{checksum}}}\n'
    return text.encode("ascii")


def encode_binary(state: ControllerState) -> bytes:
    body: bytes = bytes([STATE_LENGTH, TYPE_STATE]) + state.canonical_payload()
    crc: int = crc16_ccitt_false(body)
    body += bytes([crc & BYTE_MASK, crc >> BITS_PER_BYTE])
    frame: bytearray = bytearray([START_OF_FRAME])
    for value in body:
        if value in (START_OF_FRAME, ESCAPE):
            frame += bytes([ESCAPE, value ^ ESCAPE_XOR])
        else:
            frame.append(value)
    return bytes(frame)


ENCODERS: Final[dict[str, Callable[[ControllerState], bytes]]] = {
    "CSV": encode_csv,
    "JSON": encode_json,
    "Binary": encode_binary,
}


def hexdump(data: bytes) -> str:
    return " ".join(f"{value:02X}" for value in data)


def parse_args(argv: list[str]) -> ControllerState:
    if len(argv) == ARGUMENT_COUNT:
        seq, buttons, pot, echo = (int(argument, 0) for argument in argv)
        return ControllerState(seq=seq, buttons=buttons, pot=pot, echo=echo)
    return TEST_VALUE


def main() -> None:
    assert crc16_ccitt_false(CRC_CHECK_INPUT) == CRC_CHECK_VALUE, "CRC-16/CCITT-FALSE is wrong"
    state: ControllerState = parse_args(sys.argv[1:])
    print(f"Value: seq={state.seq} buttons={state.bits()} pot={state.pot} echo={state.echo}")
    print(f"Canonical payload: {hexdump(state.canonical_payload())}\n")
    for name, encode in ENCODERS.items():
        frame: bytes = encode(state)
        print(f"[{name}] {len(frame)} bytes")
        if name != "Binary":
            print("  text:", frame.decode("ascii").rstrip("\n"))
        print("  hex :", hexdump(frame))
        print()


if __name__ == "__main__":
    main()
