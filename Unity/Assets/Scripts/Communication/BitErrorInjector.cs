using System;
using PhysicalDigital.Protocols;

namespace PhysicalDigital.Communication
{
    public struct RobustnessCounters
    {
        public long Accepted;
        public long Undetected;
        public long BitsFlipped;
        public long BytesSeen;
    }

    public sealed class BitErrorInjector
    {
        private const int BitsPerByte = 8;

        private readonly Random random = new Random();
        private double bitErrorRate;
        private RobustnessCounters counters;

        public bool IsActive { get; private set; }

        public void Begin(double rate)
        {
            counters = default;
            bitErrorRate = rate;
            IsActive = true;
        }

        public RobustnessCounters End()
        {
            IsActive = false;
            bitErrorRate = 0.0;
            return counters;
        }

        public byte Apply(byte value)
        {
            if (bitErrorRate <= 0.0)
            {
                return value;
            }
            counters.BytesSeen++;
            for (int bit = 0; bit < BitsPerByte; bit++)
            {
                if (random.NextDouble() < bitErrorRate)
                {
                    value ^= (byte)(1 << bit);
                    counters.BitsFlipped++;
                }
            }
            return value;
        }

        public void CountAccepted(in ControllerState state)
        {
            counters.Accepted++;
            if (!state.SameValues(ControllerState.TestValue))
            {
                counters.Undetected++;
            }
        }

        public void Reset()
        {
            IsActive = false;
            bitErrorRate = 0.0;
            counters = default;
        }
    }
}
