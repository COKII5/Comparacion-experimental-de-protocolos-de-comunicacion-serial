using System.Collections;
using System.Diagnostics;
using System.Globalization;
using PhysicalDigital.Communication;
using UnityEngine;

namespace PhysicalDigital.Measurements
{
    public sealed class ThroughputTest
    {
        private const float SettleSeconds = 0.5f;
        private const double SerialBitsPerByte = 10.0;

        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        private readonly SerialLink link;
        private readonly MeasurementStorage storage;
        private readonly MeasurementProgress progress;

        public ThroughputTest(SerialLink link, MeasurementStorage storage, MeasurementProgress progress)
        {
            this.link = link;
            this.storage = storage;
            this.progress = progress;
        }

        public IEnumerator Run(MeasurementSummary summary, float durationSeconds)
        {
            progress.SetTask("Throughput: the Arduino sends as fast as it can (R0)");
            link.SendCommand("R0");
            yield return new WaitForSecondsRealtime(SettleSeconds);

            LinkSnapshot before = link.GetSnapshot();
            long start = Stopwatch.GetTimestamp();
            double elapsed = 0.0;
            while (elapsed < durationSeconds)
            {
                progress.SetFraction((float)(elapsed / durationSeconds));
                yield return null;
                elapsed = StopwatchTicks.ToSeconds(Stopwatch.GetTimestamp() - start);
            }
            LinkSnapshot after = link.GetSnapshot();
            link.SendCommand("R" + SerialLink.DefaultPeriodMs);

            Store(summary, before, after, elapsed);
            WriteFile(summary, before, after, elapsed);
        }

        private void Store(MeasurementSummary summary, LinkSnapshot before, LinkSnapshot after, double elapsed)
        {
            long frames = after.Decoder.FramesOk - before.Decoder.FramesOk;
            long bytes = after.Decoder.BytesTotal - before.Decoder.BytesTotal;
            long frameBytes = after.Decoder.BytesInFrames - before.Decoder.BytesInFrames;
            double avgBytes = frames > 0 ? (double)frameBytes / frames : 0.0;
            summary.MaxFramesPerSecond = frames / elapsed;
            summary.MaxBytesPerSecond = bytes / elapsed;
            summary.TheoreticalFramesPerSecond = avgBytes > 0.0 ? link.BaudRate / SerialBitsPerByte / avgBytes : 0.0;
            summary.ThroughputLost = after.LostPackets - before.LostPackets;
            summary.ThroughputErrors = (after.Decoder.FormatErrors - before.Decoder.FormatErrors)
                                       + (after.Decoder.IntegrityErrors - before.Decoder.IntegrityErrors);
        }

        private void WriteFile(MeasurementSummary summary, LinkSnapshot before, LinkSnapshot after, double elapsed)
        {
            long frames = after.Decoder.FramesOk - before.Decoder.FramesOk;
            long bytes = after.Decoder.BytesTotal - before.Decoder.BytesTotal;
            string csv = "seconds,frames,bytes,frames_per_s,bytes_per_s,theoretical_frames_per_s,lost,errors\n" +
                string.Format(Invariant, "{0:F3},{1},{2},{3:F1},{4:F1},{5:F1},{6},{7}\n", elapsed, frames, bytes,
                    summary.MaxFramesPerSecond, summary.MaxBytesPerSecond, summary.TheoreticalFramesPerSecond,
                    summary.ThroughputLost, summary.ThroughputErrors);
            storage.TryWrite(MeasurementStorage.TimestampedName("throughput", link.ProtocolTag, "csv"), false,
                writer => writer.Write(csv));
        }
    }
}
