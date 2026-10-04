using System;
using System.Collections;
using PhysicalDigital.Communication;
using PhysicalDigital.Game;
using PhysicalDigital.Protocols;
using UnityEngine;

namespace PhysicalDigital.Measurements
{
    public sealed class MeasurementRunner : MonoBehaviour
    {
        private const double MaxBitErrorRate = 0.5;
        private const string SummaryFileName = "summary.csv";

        [Header("Round-trip latency")]
        [SerializeField] private int pingSamples = 500;
        [SerializeField] private float pingIntervalSeconds = 0.02f;
        [SerializeField] private float pingTimeoutSeconds = 0.5f;

        [Header("Maximum throughput")]
        [SerializeField] private float throughputSeconds = 5f;

        [Header("Robustness (bit error injection)")]
        [SerializeField] private double bitErrorRate = 0.002;
        [SerializeField] private float robustnessSeconds = 15f;

        private readonly MeasurementProgress progress = new MeasurementProgress();
        private SerialLink link;
        private ValueTest valueTest;
        private LatencyTest latencyTest;
        private ThroughputTest throughputTest;
        private RobustnessTest robustnessTest;
        private Coroutine running;
        private MeasurementSummary summary;

        public int PingSamples => pingSamples;
        public float ThroughputSeconds => throughputSeconds;
        public double BitErrorRate => bitErrorRate;
        public float RobustnessSeconds => robustnessSeconds;
        public MeasurementStorage Storage { get; } = new MeasurementStorage();
        public bool IsRunning => running != null;
        public float Progress01 => progress.Fraction;
        public string CurrentTask => progress.CurrentTask;
        public string Results { get; private set; } = "No measurements yet.";

        private void Start()
        {
            link = GetComponent<SerialLink>();
            ControllerInput input = GetComponent<ControllerInput>();
            valueTest = new ValueTest(link, Storage, progress);
            latencyTest = new LatencyTest(link, input, Storage, progress);
            throughputTest = new ThroughputTest(link, Storage, progress);
            robustnessTest = new RobustnessTest(link, Storage, progress);
        }

        private void OnValidate()
        {
            pingSamples = Mathf.Max(1, pingSamples);
            pingIntervalSeconds = Mathf.Max(0f, pingIntervalSeconds);
            pingTimeoutSeconds = Mathf.Max(0f, pingTimeoutSeconds);
            throughputSeconds = Mathf.Max(0f, throughputSeconds);
            robustnessSeconds = Mathf.Max(0f, robustnessSeconds);
            bitErrorRate = Math.Min(MaxBitErrorRate, Math.Max(0.0, bitErrorRate));
        }

        public void StartValueTest()
        {
            Run(Single(RunValueTest));
        }

        public void StartLatency()
        {
            Run(Single(RunLatencyTest));
        }

        public void StartThroughput()
        {
            Run(Single(RunThroughputTest));
        }

        public void StartRobustness()
        {
            Run(Single(RunRobustnessTest));
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
            progress.SetTask("Cancelled");
            progress.SetFraction(0f);
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
            Storage.ClearWarning();
            running = StartCoroutine(Wrap(routine));
        }

        private IEnumerator Wrap(IEnumerator routine)
        {
            yield return routine;
            running = null;
            progress.Clear();
        }

        private IEnumerator Single(Func<IEnumerator> test)
        {
            summary = NewSummary();
            yield return test();
            Results = Storage.AppendWarning(summary.Describe());
        }

        private IEnumerator FullRun()
        {
            summary = NewSummary();
            yield return RunValueTest();
            yield return RunLatencyTest();
            yield return RunThroughputTest();
            yield return RunRobustnessTest();
            bool saved = AppendSummaryRow();
            string description = saved ? summary.Describe() + "\nRow appended to " + SummaryFileName : summary.Describe();
            Results = Storage.AppendWarning(description);
        }

        private IEnumerator RunValueTest()
        {
            return valueTest.Run(summary);
        }

        private IEnumerator RunLatencyTest()
        {
            return latencyTest.Run(summary, pingSamples, pingIntervalSeconds, pingTimeoutSeconds);
        }

        private IEnumerator RunThroughputTest()
        {
            return throughputTest.Run(summary, throughputSeconds);
        }

        private IEnumerator RunRobustnessTest()
        {
            return robustnessTest.Run(summary, bitErrorRate, robustnessSeconds);
        }

        private bool AppendSummaryRow()
        {
            bool newFile = !Storage.Exists(SummaryFileName);
            return Storage.TryWrite(SummaryFileName, true, writer =>
            {
                if (newFile)
                {
                    writer.WriteLine(MeasurementSummary.CsvHeader);
                }
                writer.WriteLine(summary.ToCsv());
            });
        }

        private MeasurementSummary NewSummary()
        {
            bool isJson = link.Protocol == ProtocolKind.Json;
            return new MeasurementSummary
            {
                Date = DateTime.Now,
                Protocol = isJson ? "JSON" : link.ProtocolLabel,
                Deserializer = isJson ? link.JsonBackend.ToString() : "manual",
                Port = link.PortName,
                Baud = link.BaudRate,
            };
        }
    }
}
