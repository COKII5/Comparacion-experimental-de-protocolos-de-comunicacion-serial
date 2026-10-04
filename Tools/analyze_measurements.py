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
TITLE_FONT_SIZE: Final[int] = 11
LABEL_FONT_SIZE: Final[int] = 9
NOTE_FONT_SIZE: Final[int] = 8
TITLE_PAD: Final[int] = 10
GRID_LINE_WIDTH: Final[float] = 0.6
BAR_WIDTH: Final[float] = 0.55
BAR_EDGE_WIDTH: Final[int] = 2
VALUE_LABEL_OFFSET: Final[float] = 0.02
Y_HEADROOM: Final[float] = 1.18
BOX_WIDTH: Final[float] = 0.5
MEDIAN_LABEL_OFFSET: Final[float] = 0.3
SINGLE_FIGURE_SIZE: Final[tuple[float, float]] = (6.4, 3.4)
LATENCY_FIGURE_SIZE: Final[tuple[float, float]] = (6.4, 3.6)
DOUBLE_FIGURE_SIZE: Final[tuple[float, float]] = (7.2, 3.4)
PERCENT: Final[float] = 100.0


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
            samples[protocol.key] = [float(row["rtt_ms"]) for row in csv.DictReader(handle)]
    return samples


def number(row: Row, field: str) -> float:
    try:
        return float(row[field])
    except (KeyError, ValueError):
        return math.nan


def present_protocols(available: dict[str, Row] | dict[str, list[float]]) -> list[Protocol]:
    return [protocol for protocol in PROTOCOLS if protocol.key in available]


def style_axes(axes: Axes, title: str, y_label: str) -> None:
    axes.set_title(title, loc="left", fontsize=TITLE_FONT_SIZE, color=INK, pad=TITLE_PAD)
    axes.set_ylabel(y_label, fontsize=LABEL_FONT_SIZE, color=INK_SECONDARY)
    axes.grid(axis="y", color=GRID, linewidth=GRID_LINE_WIDTH)
    axes.set_axisbelow(True)
    for side in ("top", "right", "left"):
        axes.spines[side].set_visible(False)
    axes.spines["bottom"].set_color(AXIS)
    axes.tick_params(axis="both", colors=MUTED, labelsize=LABEL_FONT_SIZE, length=0)
    for tick_label in axes.get_xticklabels():
        tick_label.set_color(INK_SECONDARY)


def draw_bars(axes: Axes, present: list[Protocol], values: list[float], value_format: str) -> None:
    positions: list[int] = list(range(len(present)))
    colors: list[str] = [protocol.color for protocol in present]
    axes.bar(positions, values, width=BAR_WIDTH, color=colors, edgecolor="white", linewidth=BAR_EDGE_WIDTH)
    axes.set_xticks(positions, [protocol.label.replace(" (", "\n(") for protocol in present])
    finite: list[float] = [value for value in values if not math.isnan(value)]
    top: float = max(finite + [0.0])
    for position, value in zip(positions, values):
        if not math.isnan(value):
            axes.text(position, value + top * VALUE_LABEL_OFFSET, value_format.format(value),
                      ha="center", va="bottom", fontsize=LABEL_FONT_SIZE, color=INK)
    axes.set_ylim(0, top * Y_HEADROOM if top > 0 else 1.0)


def save(figure: Figure, output: str, name: str) -> str:
    figure.tight_layout()
    path: str = os.path.join(output, name)
    figure.savefig(path)
    plt.close(figure)
    return path


def latency_figure(rtt: dict[str, list[float]], output: str) -> str | None:
    present: list[Protocol] = present_protocols(rtt)
    if not present:
        return None
    figure, axes = plt.subplots(figsize=LATENCY_FIGURE_SIZE, dpi=DPI)
    data: list[list[float]] = [rtt[protocol.key] for protocol in present]
    boxes = axes.boxplot(
        data, widths=BOX_WIDTH, patch_artist=True, showfliers=True,
        medianprops={"color": INK, "linewidth": 1.5},
        whiskerprops={"color": MUTED, "linewidth": 1}, capprops={"color": MUTED, "linewidth": 1},
        flierprops={"marker": "o", "markersize": 3, "markerfacecolor": MUTED, "markeredgecolor": "none", "alpha": 0.6},
    )
    for patch, protocol in zip(boxes["boxes"], present):
        patch.set_facecolor(protocol.color)
        patch.set_edgecolor("white")
        patch.set_linewidth(BAR_EDGE_WIDTH)
    axes.set_xticks(list(range(1, len(present) + 1)), [protocol.label for protocol in present])
    for position, values in enumerate(data, start=1):
        median: float = statistics.median(values)
        axes.text(position + MEDIAN_LABEL_OFFSET, median, f"{median:.2f}",
                  va="center", fontsize=NOTE_FONT_SIZE, color=INK_SECONDARY)
    style_axes(axes, "Round-trip latency (RTT) per protocol", "ms")
    return save(figure, output, "fig_latency.png")


def size_throughput_figure(summary: dict[str, Row], output: str) -> str | None:
    present: list[Protocol] = present_protocols(summary)
    if not present:
        return None
    figure, (left, right) = plt.subplots(1, 2, figsize=DOUBLE_FIGURE_SIZE, dpi=DPI)
    draw_bars(left, present, [number(summary[protocol.key], "frame_bytes") for protocol in present], "{:.1f}")
    style_axes(left, "Bytes per frame", "bytes")
    draw_bars(right, present, [number(summary[protocol.key], "max_frames_per_s") for protocol in present], "{:.0f}")
    style_axes(right, "Frames per second (maximum)", "frames/s")
    return save(figure, output, "fig_size_throughput.png")


def undetected_share(row: Row) -> float:
    undetected: float = number(row, "undetected_errors")
    detected: float = number(row, "detected_errors")
    total: float = undetected + detected
    return PERCENT * undetected / total if total > 0 else 0.0


def robustness_figure(summary: dict[str, Row], output: str) -> str | None:
    present: list[Protocol] = [protocol for protocol in present_protocols(summary)
                               if number(summary[protocol.key], "ber") > 0]
    if not present:
        return None
    shares: list[float] = [undetected_share(summary[protocol.key]) for protocol in present]
    figure, axes = plt.subplots(figsize=SINGLE_FIGURE_SIZE, dpi=DPI)
    draw_bars(axes, present, shares, "{:.2f} %")
    ber: float = number(summary[present[0].key], "ber")
    style_axes(axes, f"Undetected errors (injected BER = {ber:g})", "% of corrupted frames accepted")
    return save(figure, output, "fig_robustness.png")


def deserialization_figure(summary: dict[str, Row], output: str) -> str | None:
    present: list[Protocol] = present_protocols(summary)
    if not present:
        return None
    figure, axes = plt.subplots(figsize=SINGLE_FIGURE_SIZE, dpi=DPI)
    draw_bars(axes, present, [number(summary[protocol.key], "deserialization_us") for protocol in present], "{:.2f}")
    style_axes(axes, "Deserialization time in Unity per frame", "µs")
    return save(figure, output, "fig_deserialization.png")


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
    present: list[Protocol] = present_protocols(summary)
    if not present:
        return None
    lines: list[str] = [
        "| Metric | " + " | ".join(protocol.label for protocol in present) + " |",
        "|---|" + "---:|" * len(present),
    ]
    for name, field, value_format in TABLE_ROWS:
        cells: list[str] = [format_cell(summary[protocol.key].get(field, ""), value_format) for protocol in present]
        lines.append(f"| {name} | " + " | ".join(cells) + " |")
    text: str = "\n".join(lines) + "\n"
    path: str = os.path.join(output, "comparison_table.md")
    with open(path, "w", encoding="utf-8") as handle:
        handle.write(text)
    print(text)
    return path


def parse_arguments(root: str) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Builds the comparison table and the report figures.")
    parser.add_argument("--measurements", default=os.path.join(root, "Measurements"))
    parser.add_argument("--output", default=os.path.join(root, "Informe", "figures"))
    return parser.parse_args()


def main() -> None:
    root: str = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    arguments: argparse.Namespace = parse_arguments(root)
    measurements: str = arguments.measurements
    output: str = arguments.output
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
