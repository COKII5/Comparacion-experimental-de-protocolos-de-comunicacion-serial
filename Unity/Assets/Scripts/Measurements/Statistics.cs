using System;

namespace PhysicalDigital.Measurements
{
    public static class Statistics
    {
        public static double Mean(double[] values)
        {
            double sum = 0.0;
            foreach (double value in values)
            {
                sum += value;
            }
            return sum / values.Length;
        }

        public static double StandardDeviation(double[] values, double mean)
        {
            double sumSquares = 0.0;
            foreach (double value in values)
            {
                sumSquares += (value - mean) * (value - mean);
            }
            return Math.Sqrt(sumSquares / Math.Max(1, values.Length - 1));
        }

        public static double Percentile(double[] sorted, double fraction)
        {
            double position = (sorted.Length - 1) * fraction;
            int low = (int)Math.Floor(position);
            int high = (int)Math.Ceiling(position);
            return sorted[low] + (sorted[high] - sorted[low]) * (position - low);
        }
    }
}
