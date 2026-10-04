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
CRC_CHECK_INPUT: Final[bytes] = b"123456789"
CRC_CHECK_VALUE: Final[int] = 0x29B1

@dataclass(frozen=True)
class ControllerState:
    seq: int
    buttons: int
    pot: int
    echo: int

    def bits(self) -> list[int]:
        return [(self.buttons >> i) & 1 for i in range(BUTTON_COUNT)]

    def canonical_payload(self) -> bytes:
        return bytes([
            self.seq & 0xFF, self.seq >> 8,
            self.buttons,
            self.pot & 0xFF, self.pot >> 8,
            self.echo & 0xFF, self.echo >> 8,
        ])

TEST_VALUE: Final[ControllerState] = ControllerState(seq=0x7D7E, buttons=0x0A, pot=0x027E, echo=0x007D)

def xor8(data: bytes) -> int:
    checksum: int = 0
    for value in data:
        checksum ^= value
    return checksum

def sum8(data: bytes) -> int:
    return sum(data) & 0xFF

def crc16_ccitt_false(data: bytes) -> int:
    crc: int = 0xFFFF
    for value in data:
        crc ^= value << 8
        for _ in range(8):
            crc = ((crc << 1) ^ 0x1021) if crc & 0x8000 else (crc << 1)
            crc &= 0xFFFF
    return crc

def encode_csv(state: ControllerState) -> bytes:
    body: str = ",".join(str(v) for v in [state.seq, *state.bits(), state.pot, state.echo])
    return f"{body}*{xor8(body.encode('ascii')):02X}\n".encode("ascii")

def encode_json(state: ControllerState) -> bytes:
    checksum: int = sum8(state.canonical_payload())
    bits: str = ",".join(str(b) for b in state.bits())
    text: str = f'{{"seq":{state.seq},"b":[{bits}],"pot":{state.pot},"echo":{state.echo},"ck":{checksum}}}\n'
    return text.encode("ascii")

def encode_binary(state: ControllerState) -> bytes:
    body: bytes = bytes([STATE_LENGTH, TYPE_STATE]) + state.canonical_payload()
    crc: int = crc16_ccitt_false(body)
    body += bytes([crc & 0xFF, crc >> 8])
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
    return " ".join(f"{b:02X}" for b in data)

def parse_args(argv: list[str]) -> ControllerState:
    if len(argv) == 4:
        seq, buttons, pot, echo = (int(x, 0) for x in argv)
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
