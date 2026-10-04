using System.Collections.Generic;
using PhysicalDigital.Protocols;

namespace PhysicalDigital.Communication
{
    public struct RttSample
    {
        public double Milliseconds;
        public int FrameBytes;
    }

    public sealed class PingTracker
    {
        private const int MaxPingId = 65535;
        private const int FirstPingId = 1;
        private const int NoPendingPing = -1;

        private readonly Dictionary<int, RttSample> results = new Dictionary<int, RttSample>();
        private int pendingId = NoPendingPing;
        private long sentTicks;
        private int nextId = FirstPingId;

        public int Begin(long timestamp)
        {
            int id = nextId;
            nextId = nextId >= MaxPingId ? FirstPingId : nextId + 1;
            results.Remove(id);
            pendingId = id;
            sentTicks = timestamp;
            return id;
        }

        public void Match(in ControllerState state)
        {
            if (pendingId == NoPendingPing || state.Echo != pendingId)
            {
                return;
            }
            results[pendingId] = new RttSample
            {
                Milliseconds = StopwatchTicks.ToMilliseconds(state.RxTimestamp - sentTicks),
                FrameBytes = state.FrameBytes,
            };
            pendingId = NoPendingPing;
        }

        public bool TryTake(int id, out RttSample sample)
        {
            if (results.TryGetValue(id, out sample))
            {
                results.Remove(id);
                return true;
            }
            sample = default;
            return false;
        }

        public void Reset()
        {
            pendingId = NoPendingPing;
            results.Clear();
        }
    }
}
