# Measurements (raw data)

Unity writes the result of every test here. Do not edit these files by hand: they are the evidence for the report.

| File | Content |
|---|---|
| `value_test_<protocol>_<date>.txt` | Expected and decoded test value, plus the raw frame bytes (hex and text). |
| `rtt_<protocol>_<date>.csv` | One row per ping: `sample, ping_id, rtt_ms, frame_bytes`. |
| `throughput_<protocol>_<date>.csv` | Frames and bytes per second while the Arduino sends without pause (`R0`). |
| `robustness_<protocol>_<date>.csv` | Injected BER, flipped bits, accepted frames, detected and undetected errors. |
| `summary.csv` | One row per "Full run". The analysis script uses the latest row of each protocol. |
| `screenshot_<protocol>_<date>.png` | Simulator screenshots (F12 key). |

To build the report table and charts:

```
python Tools/analyze_measurements.py
```
