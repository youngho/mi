# Teensy 4.1 레이저 커튼 (L / R)

LM393은 산란광을 못 잡는다. **재귀반사 테이프 차단**이 정식 검출이다.

**설명서:** [laser-lm393-interrupt.md](laser-lm393-interrupt.md)

| 스케치 | 역할 |
|--------|------|
| `laserModuleL/` | Node A Slave — θ1 → UART |
| `laserModuleR/` | Node B Master — θ2 + 삼각측량 → USB HID |

Unity 좌표 검증: [docs/bds-check.md](../docs/bds-check.md)
