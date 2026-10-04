using System;
using System.Globalization;
using System.Text;

namespace PhysicalDigital.Measurements
{
    public sealed class MeasurementSummary
    {
        public const string CsvHeader =
            "date,protocol,deserializer,port,baud,value_test," +
            "rtt_n,rtt_timeouts,rtt_mean_ms,rtt_median_ms,rtt_p95_ms,rtt_min_ms,rtt_max_ms,rtt_std_ms," +
            "frame_bytes,deserialization_us,queue_latency_ms," +
            "max_frames_per_s,max_bytes_per_s,theoretical_frames_per_s,throughput_lost,throughput_errors," +
            "ber,accepted_frames,detected_errors,detected_by_integrity,undetected_errors,bits_flipped";

        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

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
