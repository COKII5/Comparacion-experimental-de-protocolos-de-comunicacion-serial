using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using PhysicalDigital.Communication;
using PhysicalDigital.Game;
using UnityEngine;

namespace PhysicalDigital.Measurements
{
    public sealed class LatencyTest
    {
        private const double MedianFraction = 0.5;
        private const double P95Fraction = 0.95;
        private const double MicrosecondsPerSecond = 1e6;

        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        private readonly SerialLink link;
        private readonly ControllerInput input;
        private readonly MeasurementStorage storage;
        private readonly MeasurementProgress progress;

        public LatencyTest(SerialLink link, ControllerInput input, MeasurementStorage storage, MeasurementProgress progress)
        {
            this.link = link;
            this.input = input;
            this.storage = storage;
            this.progress = progress;
        }

        public IEnumerator Run(MeasurementSummary summary, int samples, float intervalSeconds, float timeoutSeconds)
        {
            RttCollection collection = new RttCollection(samples);
            LinkSnapshot before = link.GetSnapshot();
            input.ResetQueueLatency();
            yield return CollectSamples(collection, samples, intervalSeconds, timeoutSeconds);
            LinkSnapshot after = link.GetSnapshot();

            WriteFile(collection.Rows);
            StoreRttStatistics(summary, collection);
            StoreDecoderStatistics(summary, before, after);
            summary.QueueLatencyMs = input.AvgQueueLatencyMs;
        }

        private IEnumerator CollectSamples(RttCollection collection, int samples, float intervalSeconds, float timeoutSeconds)
        {
            for (int sample = 1; sample <= samples; sample++)
            {
                progress.SetTask($"Latency: ping {sample}/{samples}");
                progress.SetFraction((sample - 1) / (float)samples);
                if (!link.IsOpen)
                {
                    yield break;
                }
                yield return MeasureOnePing(sample, collection, timeoutSeconds);
                yield return new WaitForSecondsRealtime(intervalSeconds);
            }
        }

        private IEnumerator MeasureOnePing(int sampleNumber, RttCollection collection, float timeoutSeconds)
        {
            int pingId = link.SendPing();
            float waited = 0f;
            while (true)
            {
                if (link.TryTakeRtt(pingId, out RttSample rtt))
                {
                    collection.Rows.Add(new RttRow { Sample = sampleNumber, PingId = pingId, Milliseconds = rtt.Milliseconds, FrameBytes = rtt.FrameBytes });
                    yield break;
                }
                waited += Time.unscaledDeltaTime;
                if (waited > timeoutSeconds)
                {
                    collection.Timeouts++;
                    yield break;
                }
                yield return null;
            }
        }

        private void WriteFile(List<RttRow> rows)
        {
            storage.TryWrite(MeasurementStorage.TimestampedName("rtt", link.ProtocolTag, "csv"), false, writer =>
            {
                writer.WriteLine("sample,ping_id,rtt_ms,frame_bytes");
                foreach (RttRow row in rows)
                {
                    writer.WriteLine(string.Format(Invariant, "{0},{1},{2:F4},{3}", row.Sample, row.PingId, row.Milliseconds, row.FrameBytes));
                }
            });
        }

        private static void StoreRttStatistics(MeasurementSummary summary, RttCollection collection)
        {
            double[] values = new double[collection.Rows.Count];
            for (int i = 0; i < values.Length; i++)
            {
                values[i] = collection.Rows[i].Milliseconds;
            }
            Array.Sort(values);
            summary.RttCount = values.Length;
            summary.RttTimeouts = collection.Timeouts;
            if (values.Length == 0)
            {
                return;
            }
            double mean = Statistics.Mean(values);
            summary.RttMean = mean;
            summary.RttMedian = Statistics.Percentile(values, MedianFraction);
            summary.RttP95 = Statistics.Percentile(values, P95Fraction);
            summary.RttMin = values[0];
            summary.RttMax = values[values.Length - 1];
            summary.RttStd = Statistics.StandardDeviation(values, mean);
        }

        private static void StoreDecoderStatistics(MeasurementSummary summary, LinkSnapshot before, LinkSnapshot after)
        {
            long parsed = after.Decoder.ParsedFrames - before.Decoder.ParsedFrames;
            long ticks = after.Decoder.ParseTicks - before.Decoder.ParseTicks;
            long frames = after.Decoder.FramesOk - before.Decoder.FramesOk;
            long frameBytes = after.Decoder.BytesInFrames - before.Decoder.BytesInFrames;
            summary.ParseMicros = parsed > 0 ? ticks * MicrosecondsPerSecond / Stopwatch.Frequency / parsed : 0.0;
            summary.BytesPerFrame = frames > 0 ? (double)frameBytes / frames : 0.0;
        }

        private struct RttRow
        {
            public int Sample;
            public int PingId;
            public double Milliseconds;
            public int FrameBytes;
        }

        private sealed class RttCollection
        {
            public readonly List<RttRow> Rows;
            public int Timeouts;

            public RttCollection(int capacity)
            {
                Rows = new List<RttRow>(capacity);
            }
        }
    }
}
