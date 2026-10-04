namespace PhysicalDigital.Communication
{
    public sealed class SequenceTracker
    {
        private const int SequenceMask = 0xFFFF;
        private const int MaxPlausibleGap = 1000;

        private bool hasPrevious;
        private ushort previous;

        public long LostPackets { get; private set; }

        public void Track(ushort seq)
        {
            if (hasPrevious)
            {
                int gap = (seq - previous - 1) & SequenceMask;
                if (gap > 0 && gap < MaxPlausibleGap)
                {
                    LostPackets += gap;
                }
            }
            previous = seq;
            hasPrevious = true;
        }

        public void Resync()
        {
            hasPrevious = false;
        }

        public void Reset()
        {
            hasPrevious = false;
            LostPackets = 0;
        }
    }
}
