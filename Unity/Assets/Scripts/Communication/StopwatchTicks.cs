using System.Diagnostics;

namespace PhysicalDigital.Communication
{
    public static class StopwatchTicks
    {
        private const double MillisecondsPerSecond = 1000.0;

        public static double ToMilliseconds(long ticks)
        {
            return ticks * MillisecondsPerSecond / Stopwatch.Frequency;
        }

        public static double ToSeconds(long ticks)
        {
            return (double)ticks / Stopwatch.Frequency;
        }
    }
}
