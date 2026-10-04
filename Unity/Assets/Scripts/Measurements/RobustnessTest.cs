using System.Collections;
using System.Globalization;
using PhysicalDigital.Communication;
using UnityEngine;

namespace PhysicalDigital.Measurements
{
    public sealed class RobustnessTest
    {
        private const float SettleSeconds = 0.5f;

        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        private readonly SerialLink link;
        private readonly MeasurementStorage storage;
        private readonly MeasurementProgress progress;

        public RobustnessTest(SerialLink link, MeasurementStorage storage, MeasurementProgress progress)
        {
            this.link = link;
            this.storage = storage;
            this.progress = progress;
        }

        public IEnumerator Run(MeasurementSummary summary, double bitErrorRate, float durationSeconds)
        {
            progress.SetTask("Robustness: the Arduino repeats the test value (X1) and Unity flips random bits");
            link.SendCommand("X1");
            yield return new WaitForSecondsRealtime(SettleSeconds);

            LinkSnapshot before = link.GetSnapshot();
            link.BeginRobustness(bitErrorRate);
            float elapsed = 0f;
            while (elapsed < durationSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                progress.SetFraction(elapsed / durationSeconds);
                yield return null;
            }
            RobustnessCounters counters = link.EndRobustness();
            LinkSnapshot after = link.GetSnapshot();
            link.SendCommand("X0");

            Store(summary, before, after, counters, bitErrorRate);
            WriteFile(summary, counters, bitErrorRate, durationSeconds);
        }

        private static void Store(MeasurementSummary summary, LinkSnapshot before, LinkSnapshot after,
            RobustnessCounters counters, double bitErrorRate)
        {
            long integrity = after.Decoder.IntegrityErrors - before.Decoder.IntegrityErrors;
            long format = after.Decoder.FormatErrors - before.Decoder.FormatErrors;
            summary.Ber = bitErrorRate;
            summary.RobustAccepted = counters.Accepted;
            summary.RobustUndetected = counters.Undetected;
            summary.RobustDetected = integrity + format;
            summary.RobustDetectedByIntegrity = integrity;
            summary.BitsFlipped = counters.BitsFlipped;
        }

        private void WriteFile(MeasurementSummary summary, RobustnessCounters counters, double bitErrorRate, float durationSeconds)
        {
            string csv = "ber,seconds,bytes,bits_flipped,accepted,undetected,detected,detected_by_integrity\n" +
                string.Format(Invariant, "{0},{1:F1},{2},{3},{4},{5},{6},{7}\n", bitErrorRate, durationSeconds,
                    counters.BytesSeen, counters.BitsFlipped, counters.Accepted, counters.Undetected,
                    summary.RobustDetected, summary.RobustDetectedByIntegrity);
            storage.TryWrite(MeasurementStorage.TimestampedName("robustness", link.ProtocolTag, "csv"), false,
                writer => writer.Write(csv));
        }
    }
}
