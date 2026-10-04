using System;
using System.Collections;
using System.Globalization;
using System.Text;
using PhysicalDigital.Communication;
using PhysicalDigital.Protocols;
using UnityEngine;

namespace PhysicalDigital.Measurements
{
    public sealed class ValueTest
    {
        private const float TimeoutSeconds = 1.5f;
        private const string ReportDateFormat = "yyyy-MM-dd HH:mm:ss";

        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        private readonly SerialLink link;
        private readonly MeasurementStorage storage;
        private readonly MeasurementProgress progress;

        public ValueTest(SerialLink link, MeasurementStorage storage, MeasurementProgress progress)
        {
            this.link = link;
            this.storage = storage;
            this.progress = progress;
        }

        public IEnumerator Run(MeasurementSummary summary)
        {
            progress.SetTask("Value test: the Arduino sends the test value (command T)");
            TestOutcome outcome = new TestOutcome();
            yield return WaitForTestFrame(outcome);
            summary.ValueTestOk = outcome.Received;
            string report = BuildReport(outcome);
            storage.TryWrite(MeasurementStorage.TimestampedName("value_test", link.ProtocolTag, "txt"), false,
                writer => writer.Write(report));
            summary.ValueTestDetail = outcome.Received ? ToHex(outcome.Frame.Raw) : "no response";
        }

        private IEnumerator WaitForTestFrame(TestOutcome outcome)
        {
            link.BeginTestCapture();
            link.SendCommand("T");
            float waited = 0f;
            while (waited < TimeoutSeconds)
            {
                if (link.TryGetTestResult(out TestFrame frame))
                {
                    outcome.Received = true;
                    outcome.Frame = frame;
                    yield break;
                }
                waited += Time.unscaledDeltaTime;
                progress.SetFraction(waited / TimeoutSeconds);
                yield return null;
            }
        }

        private string BuildReport(TestOutcome outcome)
        {
            StringBuilder report = new StringBuilder();
            report.AppendLine($"Protocol: {link.ProtocolLabel}");
            report.AppendLine($"Date: {DateTime.Now.ToString(ReportDateFormat, Invariant)}");
            report.AppendLine($"Expected: {ControllerState.TestValue}");
            if (!outcome.Received)
            {
                report.AppendLine($"Result: FAIL (no valid frame with the test value within {TimeoutSeconds} s)");
                return report.ToString();
            }
            TestFrame frame = outcome.Frame;
            report.AppendLine($"Decoded:  {frame.State}");
            report.AppendLine("Result: OK (values match)");
            report.AppendLine($"Bytes on the wire: {frame.Raw.Length}");
            report.AppendLine("Hex:  " + ToHex(frame.Raw));
            if (link.Protocol != ProtocolKind.Binary)
            {
                report.AppendLine("Text: " + Encoding.ASCII.GetString(frame.Raw).TrimEnd('\n'));
            }
            return report.ToString();
        }

        private static string ToHex(byte[] bytes)
        {
            return BitConverter.ToString(bytes).Replace('-', ' ');
        }

        private sealed class TestOutcome
        {
            public bool Received;
            public TestFrame Frame;
        }
    }
}
