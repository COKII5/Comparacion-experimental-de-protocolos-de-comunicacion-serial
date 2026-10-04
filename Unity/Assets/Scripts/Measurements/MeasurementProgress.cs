namespace PhysicalDigital.Measurements
{
    public sealed class MeasurementProgress
    {
        public string CurrentTask { get; private set; } = "";
        public float Fraction { get; private set; }

        public void SetTask(string task)
        {
            CurrentTask = task;
        }

        public void SetFraction(float fraction)
        {
            Fraction = fraction;
        }

        public void Clear()
        {
            CurrentTask = "";
            Fraction = 0f;
        }
    }
}
