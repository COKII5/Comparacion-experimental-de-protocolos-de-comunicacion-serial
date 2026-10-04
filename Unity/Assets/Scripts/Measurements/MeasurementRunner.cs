using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using PhysicalDigital.Communication;
using PhysicalDigital.Game;
using PhysicalDigital.Protocols;
using UnityEngine;

namespace PhysicalDigital.Measurements
{
    public sealed class MeasurementRunner : MonoBehaviour
    {
        private const float ValueTestTimeoutSeconds = 1.5f;
        private const float SettleSeconds = 0.5f;
        private const string TimestampFormat = "yyyyMMdd_HHmmss";

        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

        [Header("Round-trip latency")]
        public int pingSamples = 500;
        public float pingIntervalSeconds = 0.02f;
        public float pingTimeoutSeconds = 0.5f;

        [Header("Maximum throughput")]
        public float throughputSeconds = 5f;

        [Header("Robustness (bit error injection)")]
        public double bitErrorRate = 0.002;
        public float robustnessSeconds = 15f;

        private SerialLink link;
        private ControllerInput input;
        private Coroutine running;
        private Summary summary;

        public bool IsRunning => running != null;
        public float Progress01 { get; private set; }
        public string CurrentTask { get; private set; } = "";
        public string Results { get; private set; } = "No measurements yet.";

        public string OutputFolder
        {
            get
            {
#if UNITY_EDITOR
                string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "Measurements"));
#else
                string folder = Path.Combine(Application.persistentDataPath, "Measurements");
#endif
                Directory.CreateDirectory(folder);
                return folder;
            }
        }

        private string Stamp => DateTime.Now.ToString(TimestampFormat, Invariant);

        private void Start()
        {
            link = GetComponent<SerialLink>();
            input = GetComponent<ControllerInput>();
        }

        public void StartValueTest()
        {
            Run(Single(ValueTest));
        }

        public void StartLatency()
        {
            Run(Single(Latency));
        }

        public void StartThroughput()
        {
            Run(Single(Throughput));
        }

        public void StartRobustness()
        {
            Run(Single(Robustness));
        }

        public void StartFullRun()
        {
            Run(FullRun());
        }

        public void Cancel()
        {
            if (running != null)
            {
                StopCoroutine(running);
            }
            running = null;
            link.EndRobustness();
            link.SendCommand("X0");
            link.SendCommand("R" + SerialLink.DefaultPeriodMs);
            CurrentTask = "Cancelled";
            Progress01 = 0f;
        }

        private void Run(IEnumerator routine)
        {
            if (running != null)
            {
                return;
            }
            if (!link.IsReady)
            {
                Results = "Connect the Arduino (and wait for its reset) before measuring.";
                return;
            }
            running = StartCoroutine(Wrap(routine));
        }

        private IEnumerator Wrap(IEnumerator routine)
        {
            yield return routine;
            running = null;
            Progress01 = 0f;
            CurrentTask = "";
        }

        private IEnumerator Single(Func<IEnumerator> test)
        {
            summary = NewSummary();
            yield return test();
            Results = summary.Describe();
        }

        private IEnumerator FullRun()
        {
            summary = NewSummary();
            yield return ValueTest();
            yield return Latency();
            yield return Throughput();
            yield return Robustness();
            string path = Path.Combine(OutputFolder, "summary.csv");
            bool newFile = !File.Exists(path);
            using (StreamWriter writer = new StreamWriter(path, true, Utf8NoBom))
            {
                if (newFile)
                {
                    writer.WriteLine(Summary.CsvHeader);
                }
                writer.WriteLine(summary.ToCsv());
            }
            Results = summary.Describe() + "\nRow appended to summary.csv";
        }

        private Summary NewSummary()
        {
            bool isJson = link.protocol == ProtocolKind.Json;
            return new Summary
            {
                Date = DateTime.Now,
                Protocol = isJson ? "JSON" : link.ProtocolLabel,
                Deserializer = isJson ? link.jsonBackend.ToString() : "manual",
                Port = link.portName,
                Baud = link.baudRate,
            };
        }

        private IEnumerator ValueTest()
        {
            CurrentTask = "Value test: the Arduino sends the test value (command T)";
            link.BeginTestCapture();
            link.SendCommand("T");
            float waited = 0f;
            TestFrame frame = default;
            bool ok = false;
            while (waited < ValueTestTimeoutSeconds)
            {
                if (link.TryGetTestResult(out frame))
                {
                    ok = true;
                    break;
                }
                waited += Time.unscaledDeltaTime;
                Progress01 = waited / ValueTestTimeoutSeconds;
                yield return null;
            }
            summary.ValueTestOk = ok;

            StringBuilder report = new StringBuilder();
            report.AppendLine($"Protocol: {link.ProtocolLabel}");
            report.AppendLine($"Date: {DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", Invariant)}");
            report.AppendLine($"Expected: {ControllerState.TestValue}");
            if (ok)
            {
                report.AppendLine($"Decoded:  {frame.State}");
                report.AppendLine("Result: OK (values match)");
                report.AppendLine($"Bytes on the wire: {frame.Raw.Length}");
                report.AppendLine("Hex:  " + ToHex(frame.Raw));
                if (link.protocol != ProtocolKind.Binary)
                {
                    report.AppendLine("Text: " + Encoding.ASCII.GetString(frame.Raw).TrimEnd('\n'));
                }
            }
            else
            {
                report.AppendLine($"Result: FAIL (no valid frame with the test value within {ValueTestTimeoutSeconds} s)");
            }
            File.WriteAllText(Path.Combine(OutputFolder, $"value_test_{link.ProtocolTag}_{Stamp}.txt"), report.ToString());
            summary.ValueTestDetail = ok ? ToHex(frame.Raw) : "no response";
        }

        private static string ToHex(byte[] bytes)
        {
            return BitConverter.ToString(bytes).Replace('-', ' ');
        }

        private struct RttRow
        {
            public int Sample;
            public int PingId;
            public double Milliseconds;
            public int FrameBytes;
        }

        private IEnumerator Latency()
        {
            List<RttRow> rows = new List<RttRow>(pingSamples);
            int timeouts = 0;
            LinkSnapshot before = link.GetSnapshot();
            input.ResetQueueLatency();

            for (int i = 0; i < pingSamples; i++)
            {
                CurrentTask = $"Latency: ping {i + 1}/{pingSamples}";
                Progress01 = i / (float)pingSamples;
                if (!link.IsOpen)
                {
                    break;
                }

                int id = link.SendPing();
                float waited = 0f;
                RttSample sample;
                bool received = false;
                while (true)
                {
                    if (link.TryTakeRtt(id, out sample))
                    {
                        received = true;
                        break;
                    }
                    waited += Time.unscaledDeltaTime;
                    if (waited > pingTimeoutSeconds)
                    {
                        break;
                    }
                    yield return null;
                }
                if (received)
                {
                    rows.Add(new RttRow { Sample = i + 1, PingId = id, Milliseconds = sample.Milliseconds, FrameBytes = sample.FrameBytes });
                }
                else
                {
                    timeouts++;
                }
                yield return new WaitForSecondsRealtime(pingIntervalSeconds);
            }

            LinkSnapshot after = link.GetSnapshot();
            string path = Path.Combine(OutputFolder, $"rtt_{link.ProtocolTag}_{Stamp}.csv");
            using (StreamWriter writer = new StreamWriter(path, false, Utf8NoBom))
            {
                writer.WriteLine("sample,ping_id,rtt_ms,frame_bytes");
                foreach (RttRow row in rows)
                {
                    writer.WriteLine(string.Format(Invariant, "{0},{1},{2:F4},{3}", row.Sample, row.PingId, row.Milliseconds, row.FrameBytes));
                }
            }

            double[] values = new double[rows.Count];
            for (int i = 0; i < rows.Count; i++)
            {
                values[i] = rows[i].Milliseconds;
            }
            Array.Sort(values);
            summary.RttCount = values.Length;
            summary.RttTimeouts = timeouts;
            if (values.Length > 0)
            {
                double mean = Mean(values);
                summary.RttMean = mean;
                summary.RttMedian = Percentile(values, 0.5);
                summary.RttP95 = Percentile(values, 0.95);
                summary.RttMin = values[0];
                summary.RttMax = values[values.Length - 1];
                summary.RttStd = StandardDeviation(values, mean);
            }
            long parsed = after.Decoder.ParsedFrames - before.Decoder.ParsedFrames;
            long ticks = after.Decoder.ParseTicks - before.Decoder.ParseTicks;
            long frames = after.Decoder.FramesOk - before.Decoder.FramesOk;
            long frameBytes = after.Decoder.BytesInFrames - before.Decoder.BytesInFrames;
            summary.ParseMicros = parsed > 0 ? ticks * 1e6 / Stopwatch.Frequency / parsed : 0.0;
            summary.BytesPerFrame = frames > 0 ? (double)frameBytes / frames : 0.0;
            summary.QueueLatencyMs = input.AvgQueueLatencyMs;
        }

        private static double Mean(double[] values)
        {
            double sum = 0.0;
            foreach (double value in values)
            {
                sum += value;
            }
            return sum / values.Length;
        }

        private static double StandardDeviation(double[] values, double mean)
        {
            double sumSquares = 0.0;
            foreach (double value in values)
            {
                sumSquares += (value - mean) * (value - mean);
            }
            return Math.Sqrt(sumSquares / Math.Max(1, values.Length - 1));
        }

        private static double Percentile(double[] sorted, double p)
        {
            double position = (sorted.Length - 1) * p;
            int low = (int)Math.Floor(position);
            int high = (int)Math.Ceiling(position);
            return sorted[low] + (sorted[high] - sorted[low]) * (position - low);
        }

        private IEnumerator Throughput()
        {
            CurrentTask = "Throughput: the Arduino sends as fast as it can (R0)";
            link.SendCommand("R0");
            yield return new WaitForSecondsRealtime(SettleSeconds);

            LinkSnapshot a = link.GetSnapshot();
            long start = Stopwatch.GetTimestamp();
            double elapsed = 0.0;
            while (elapsed < throughputSeconds)
            {
                Progress01 = (float)(elapsed / throughputSeconds);
                yield return null;
                elapsed = SerialLink.TicksToMs(Stopwatch.GetTimestamp() - start) / 1000.0;
            }
            LinkSnapshot b = link.GetSnapshot();
            link.SendCommand("R" + SerialLink.DefaultPeriodMs);

            long frames = b.Decoder.FramesOk - a.Decoder.FramesOk;
            long bytes = b.Decoder.BytesTotal - a.Decoder.BytesTotal;
            long frameBytes = b.Decoder.BytesInFrames - a.Decoder.BytesInFrames;
            double avgBytes = frames > 0 ? (double)frameBytes / frames : 0.0;
            summary.MaxFramesPerSecond = frames / elapsed;
            summary.MaxBytesPerSecond = bytes / elapsed;
            summary.TheoreticalFramesPerSecond = avgBytes > 0.0 ? link.baudRate / 10.0 / avgBytes : 0.0;
            summary.ThroughputLost = b.LostPackets - a.LostPackets;
            summary.ThroughputErrors = (b.Decoder.FormatErrors - a.Decoder.FormatErrors)
                                       + (b.Decoder.IntegrityErrors - a.Decoder.IntegrityErrors);

            File.WriteAllText(Path.Combine(OutputFolder, $"throughput_{link.ProtocolTag}_{Stamp}.csv"),
                "seconds,frames,bytes,frames_per_s,bytes_per_s,theoretical_frames_per_s,lost,errors\n" +
                string.Format(Invariant, "{0:F3},{1},{2},{3:F1},{4:F1},{5:F1},{6},{7}\n", elapsed, frames, bytes,
                    summary.MaxFramesPerSecond, summary.MaxBytesPerSecond, summary.TheoreticalFramesPerSecond,
                    summary.ThroughputLost, summary.ThroughputErrors));
        }

        private IEnumerator Robustness()
        {
            CurrentTask = "Robustness: the Arduino repeats the test value (X1) and Unity flips random bits";
            link.SendCommand("X1");
            yield return new WaitForSecondsRealtime(SettleSeconds);

            LinkSnapshot a = link.GetSnapshot();
            link.BeginRobustness(bitErrorRate);
            float elapsed = 0f;
            while (elapsed < robustnessSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                Progress01 = elapsed / robustnessSeconds;
                yield return null;
            }
            RobustnessCounters counters = link.EndRobustness();
            LinkSnapshot b = link.GetSnapshot();
            link.SendCommand("X0");

            long integrity = b.Decoder.IntegrityErrors - a.Decoder.IntegrityErrors;
            long format = b.Decoder.FormatErrors - a.Decoder.FormatErrors;
            summary.Ber = bitErrorRate;
            summary.RobustAccepted = counters.Accepted;
            summary.RobustUndetected = counters.Undetected;
            summary.RobustDetected = integrity + format;
            summary.RobustDetectedByIntegrity = integrity;
            summary.BitsFlipped = counters.BitsFlipped;

            File.WriteAllText(Path.Combine(OutputFolder, $"robustness_{link.ProtocolTag}_{Stamp}.csv"),
                "ber,seconds,bytes,bits_flipped,accepted,undetected,detected,detected_by_integrity\n" +
                string.Format(Invariant, "{0},{1:F1},{2},{3},{4},{5},{6},{7}\n", bitErrorRate, robustnessSeconds,
                    counters.BytesSeen, counters.BitsFlipped, counters.Accepted, counters.Undetected,
                    summary.RobustDetected, summary.RobustDetectedByIntegrity));
        }

        private sealed class Summary
        {
            public const string CsvHeader =
                "date,protocol,deserializer,port,baud,value_test," +
                "rtt_n,rtt_timeouts,rtt_mean_ms,rtt_median_ms,rtt_p95_ms,rtt_min_ms,rtt_max_ms,rtt_std_ms," +
                "frame_bytes,deserialization_us,queue_latency_ms," +
                "max_frames_per_s,max_bytes_per_s,theoretical_frames_per_s,throughput_lost,throughput_errors," +
                "ber,accepted_frames,detected_errors,detected_by_integrity,undetected_errors,bits_flipped";

            public DateTime Date;
            public string Protocol = "";
            public string Deserializer = "";
            public string Port = "";
            public int Baud;
            public bool ValueTestOk;
            public string ValueTestDetail = "";
            public int RttCount;
            public int RttTimeouts;
            public double RttMean;
            public double RttMedian;
            public double RttP95;
            public double RttMin;
            public double RttMax;
            public double RttStd;
            public double BytesPerFrame;
            public double ParseMicros;
            public double QueueLatencyMs;
            public double MaxFramesPerSecond;
            public double MaxBytesPerSecond;
            public double TheoreticalFramesPerSecond;
            public long ThroughputLost;
            public long ThroughputErrors;
            public double Ber;
            public long RobustAccepted;
            public long RobustUndetected;
            public long RobustDetected;
            public long RobustDetectedByIntegrity;
            public long BitsFlipped;

            public string ToCsv()
            {
                return string.Format(Invariant,
                    "{0:yyyy-MM-dd HH:mm:ss},{1},{2},{3},{4},{5}," +
                    "{6},{7},{8:F4},{9:F4},{10:F4},{11:F4},{12:F4},{13:F4}," +
                    "{14:F2},{15:F3},{16:F3}," +
                    "{17:F1},{18:F1},{19:F1},{20},{21}," +
                    "{22},{23},{24},{25},{26},{27}",
                    Date, Protocol, Deserializer, Port, Baud, ValueTestOk ? "OK" : "FAIL",
                    RttCount, RttTimeouts, RttMean, RttMedian, RttP95, RttMin, RttMax, RttStd,
                    BytesPerFrame, ParseMicros, QueueLatencyMs,
                    MaxFramesPerSecond, MaxBytesPerSecond, TheoreticalFramesPerSecond, ThroughputLost, ThroughputErrors,
                    Ber, RobustAccepted, RobustDetected, RobustDetectedByIntegrity, RobustUndetected, BitsFlipped);
            }

            public string Describe()
            {
                StringBuilder text = new StringBuilder();
                text.AppendLine($"<b>{Protocol}</b> ({Deserializer})");
                if (!string.IsNullOrEmpty(ValueTestDetail))
                {
                    text.AppendLine($"Value test: {(ValueTestOk ? "OK" : "FAIL")}");
                }
                if (RttCount > 0)
                {
                    text.AppendLine(string.Format(Invariant, "RTT mean {0:F2} ms · median {1:F2} · p95 {2:F2} (n={3}, timeouts {4})",
                        RttMean, RttMedian, RttP95, RttCount, RttTimeouts));
                }
                if (BytesPerFrame > 0.0)
                {
                    text.AppendLine(string.Format(Invariant, "{0:F1} bytes/frame · deserialization {1:F2} µs", BytesPerFrame, ParseMicros));
                }
                if (MaxFramesPerSecond > 0.0)
                {
                    text.AppendLine(string.Format(Invariant, "Max {0:F0} frames/s (theoretical {1:F0}) · lost {2}",
                        MaxFramesPerSecond, TheoreticalFramesPerSecond, ThroughputLost));
                }
                if (Ber > 0.0)
                {
                    text.AppendLine(string.Format(Invariant, "BER {0}: detected {1} · UNDETECTED {2} of {3} accepted",
                        Ber, RobustDetected, RobustUndetected, RobustAccepted));
                }
                return text.ToString().TrimEnd();
            }
        }
    }
}
