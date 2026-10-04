from __future__ import annotations

import argparse
import csv
import glob
import math
import os
import statistics
from dataclasses import dataclass
from typing import Final

import matplotlib

matplotlib.use("Agg")
import matplotlib.pyplot as plt  # noqa: E402
from matplotlib.axes import Axes  # noqa: E402
from matplotlib.figure import Figure  # noqa: E402

Row = dict[str, str]

@dataclass(frozen=True)
class Protocol:
    key: str
    label: str
    color: str

PROTOCOLS: Final[list[Protocol]] = [
    Protocol("CSV", "CSV", "#2a78d6"),
    Protocol("JSON_JsonUtility", "JSON (JsonUtility)", "#eb6834"),
    Protocol("Binary", "Binary", "#1baf7a"),
    Protocol("JSON_Newtonsoft", "JSON (Newtonsoft)", "#eda100"),
]
INK: Final[str] = "#0b0b0b"
INK_SECONDARY: Final[str] = "#52514e"
MUTED: Final[str] = "#898781"
GRID: Final[str] = "#e1e0d9"
AXIS: Final[str] = "#c3c2b7"
DPI: Final[int] = 200

def summary_key(row: Row) -> str:
    if row["protocol"] == "JSON":
        return f"JSON_{row['deserializer']}"
    return row["protocol"]

def read_summary(folder: str) -> dict[str, Row]:
    path: str = os.path.join(folder, "summary.csv")
    if not os.path.exists(path):
        return {}
    latest: dict[str, Row] = {}
    with open(path, newline="", encoding="utf-8") as handle:
        for row in csv.DictReader(handle):
            latest[summary_key(row)] = row
    return latest

def read_rtt(folder: str) -> dict[str, list[float]]:
    samples: dict[str, list[float]] = {}
    for protocol in PROTOCOLS:
        files: list[str] = sorted(glob.glob(os.path.join(folder, f"rtt_{protocol.key}_*.csv")))
        if not files:
            continue
        with open(files[-1], newline="", encoding="utf-8") as handle:
            samples[protocol.key] = [float(r["rtt_ms"]) for r in csv.DictReader(handle)]
    return samples

def number(row: Row, field: str) -> float:
    try:
        return float(row[field])
    except (KeyError, ValueError):
        return math.nan

def style_axes(ax: Axes, title: str, y_label: str) -> None:
    ax.set_title(title, loc="left", fontsize=11, color=INK, pad=10)
    ax.set_ylabel(y_label, fontsize=9, color=INK_SECONDARY)
    ax.grid(axis="y", color=GRID, linewidth=0.6)
    ax.set_axisbelow(True)
    for side in ("top", "right", "left"):
        ax.spines[side].set_visible(False)
    ax.spines["bottom"].set_color(AXIS)
    ax.tick_params(axis="both", colors=MUTED, labelsize=9, length=0)
    for tick_label in ax.get_xticklabels():
        tick_label.set_color(INK_SECONDARY)

def draw_bars(ax: Axes, present: list[Protocol], values: list[float], value_format: str) -> None:
    positions: list[int] = list(range(len(present)))
    ax.bar(positions, values, width=0.55, color=[p.color for p in present], edgecolor="white", linewidth=2)
    ax.set_xticks(positions, [p.label.replace(" (", "\n(") for p in present])
    finite: list[float] = [v for v in values if not math.isnan(v)]
    top: float = max(finite + [0.0])
    for x, value in zip(positions, values):
        if not math.isnan(value):
            ax.text(x, value + top * 0.02, value_format.format(value), ha="center", va="bottom", fontsize=9, color=INK)
    ax.set_ylim(0, top * 1.18 if top > 0 else 1.0)

def save(fig: Figure, output: str, name: str) -> str:
    fig.tight_layout()
    path: str = os.path.join(output, name)
    fig.savefig(path)
    plt.close(fig)
    return path

def latency_figure(rtt: dict[str, list[float]], output: str) -> str | None:
    present: list[Protocol] = [p for p in PROTOCOLS if p.key in rtt]
    if not present:
        return None
    fig, ax = plt.subplots(figsize=(6.4, 3.6), dpi=DPI)
    data: list[list[float]] = [rtt[p.key] for p in present]
    boxes = ax.boxplot(
        data, widths=0.5, patch_artist=True, showfliers=True,
        medianprops={"color": INK, "linewidth": 1.5},
        whiskerprops={"color": MUTED, "linewidth": 1}, capprops={"color": MUTED, "linewidth": 1},
        flierprops={"marker": "o", "markersize": 3, "markerfacecolor": MUTED, "markeredgecolor": "none", "alpha": 0.6},
    )
    for patch, protocol in zip(boxes["boxes"], present):
        patch.set_facecolor(protocol.color)
        patch.set_edgecolor("white")
        patch.set_linewidth(2)
    ax.set_xticks(list(range(1, len(present) + 1)), [p.label for p in present])
    for position, values in enumerate(data, start=1):
        median: float = statistics.median(values)
        ax.text(position + 0.3, median, f"{median:.2f}", va="center", fontsize=8, color=INK_SECONDARY)
    style_axes(ax, "Round-trip latency (RTT) per protocol", "ms")
    return save(fig, output, "fig_latency.png")

def size_throughput_figure(summary: dict[str, Row], output: str) -> str | None:
    present: list[Protocol] = [p for p in PROTOCOLS if p.key in summary]
    if not present:
        return None
    fig, (left, right) = plt.subplots(1, 2, figsize=(7.2, 3.4), dpi=DPI)
    draw_bars(left, present, [number(summary[p.key], "frame_bytes") for p in present], "{:.1f}")
    style_axes(left, "Bytes per frame", "bytes")
    draw_bars(right, present, [number(summary[p.key], "max_frames_per_s") for p in present], "{:.0f}")
    style_axes(right, "Frames per second (maximum)", "frames/s")
    return save(fig, output, "fig_size_throughput.png")

def robustness_figure(summary: dict[str, Row], output: str) -> str | None:
    present: list[Protocol] = [p for p in PROTOCOLS if p.key in summary and number(summary[p.key], "ber") > 0]
    if not present:
        return None
    shares: list[float] = []
    for protocol in present:
        undetected: float = number(summary[protocol.key], "undetected_errors")
        detected: float = number(summary[protocol.key], "detected_errors")
        total: float = undetected + detected
        shares.append(100.0 * undetected / total if total > 0 else 0.0)
    fig, ax = plt.subplots(figsize=(6.4, 3.4), dpi=DPI)
    draw_bars(ax, present, shares, "{:.2f} %")
    ber: float = number(summary[present[0].key], "ber")
    style_axes(ax, f"Undetected errors (injected BER = {ber:g})", "% of corrupted frames accepted")
    return save(fig, output, "fig_robustness.png")

def deserialization_figure(summary: dict[str, Row], output: str) -> str | None:
    present: list[Protocol] = [p for p in PROTOCOLS if p.key in summary]
    if not present:
        return None
    fig, ax = plt.subplots(figsize=(6.4, 3.4), dpi=DPI)
    draw_bars(ax, present, [number(summary[p.key], "deserialization_us") for p in present], "{:.2f}")
    style_axes(ax, "Deserialization time in Unity per frame", "µs")
    return save(fig, output, "fig_deserialization.png")

TABLE_ROWS: Final[list[tuple[str, str, str]]] = [
    ("Value test", "value_test", "{}"),
    ("Bytes per frame", "frame_bytes", "{:.1f}"),
    ("RTT mean (ms)", "rtt_mean_ms", "{:.2f}"),
    ("RTT median (ms)", "rtt_median_ms", "{:.2f}"),
    ("RTT p95 (ms)", "rtt_p95_ms", "{:.2f}"),
    ("RTT std. dev. (ms)", "rtt_std_ms", "{:.2f}"),
    ("Deserialization (µs/frame)", "deserialization_us", "{:.2f}"),
    ("Thread → game latency (ms)", "queue_latency_ms", "{:.2f}"),
    ("Max frames/s", "max_frames_per_s", "{:.0f}"),
    ("Theoretical frames/s (115200 8N1)", "theoretical_frames_per_s", "{:.0f}"),
    ("Lost packets (throughput)", "throughput_lost", "{:.0f}"),
    ("Detected errors (robustness)", "detected_errors", "{:.0f}"),
    ("UNDETECTED errors (robustness)", "undetected_errors", "{:.0f}"),
]

def format_cell(raw: str, value_format: str) -> str:
    if value_format == "{}":
        return raw
    try:
        return value_format.format(float(raw))
    except ValueError:
        return raw

def comparison_table(summary: dict[str, Row], output: str) -> str | None:
    present: list[Protocol] = [p for p in PROTOCOLS if p.key in summary]
    if not present:
        return None
    lines: list[str] = [
        "| Metric | " + " | ".join(p.label for p in present) + " |",
        "|---|" + "---:|" * len(present),
    ]
    for name, field, value_format in TABLE_ROWS:
        cells: list[str] = [format_cell(summary[p.key].get(field, ""), value_format) for p in present]
        lines.append(f"| {name} | " + " | ".join(cells) + " |")
    text: str = "\n".join(lines) + "\n"
    path: str = os.path.join(output, "comparison_table.md")
    with open(path, "w", encoding="utf-8") as handle:
        handle.write(text)
    print(text)
    return path

def main() -> None:
    root: str = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--measurements", default=os.path.join(root, "Measurements"))
    parser.add_argument("--output", default=os.path.join(root, "Informe", "figures"))
    args: argparse.Namespace = parser.parse_args()
    measurements: str = args.measurements
    output: str = args.output
    os.makedirs(output, exist_ok=True)

    plt.rcParams["font.family"] = ["Segoe UI", "DejaVu Sans", "sans-serif"]
    summary: dict[str, Row] = read_summary(measurements)
    rtt: dict[str, list[float]] = read_rtt(measurements)
    if not summary and not rtt:
        print(f"No data in {measurements}. Run the measurements from Unity first.")
        return

    generated: list[str | None] = [
        comparison_table(summary, output),
        latency_figure(rtt, output),
        size_throughput_figure(summary, output),
        robustness_figure(summary, output),
        deserialization_figure(summary, output),
    ]
    for path in generated:
        if path is not None:
            print("Generated:", path)

if __name__ == "__main__":
    main()
