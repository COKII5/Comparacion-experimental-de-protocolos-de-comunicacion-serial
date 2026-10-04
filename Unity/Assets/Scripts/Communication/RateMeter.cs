using System;

namespace PhysicalDigital.Communication
{
    public sealed class RateMeter
    {
        private const float WindowSeconds = 1f;

        private float elapsed;
        private long lastFrames;
        private long lastBytes;

        public float FramesPerSecond { get; private set; }
        public float BytesPerSecond { get; private set; }

        public bool Advance(float deltaSeconds)
        {
            elapsed += deltaSeconds;
            return elapsed >= WindowSeconds;
        }

        public void Sample(long frames, long bytes)
        {
            FramesPerSecond = Math.Max(0L, frames - lastFrames) / elapsed;
            BytesPerSecond = Math.Max(0L, bytes - lastBytes) / elapsed;
            lastFrames = frames;
            lastBytes = bytes;
            elapsed = 0f;
        }

        public void Restart()
        {
            lastFrames = 0;
            lastBytes = 0;
        }

        public void Clear()
        {
            FramesPerSecond = 0f;
            BytesPerSecond = 0f;
        }
    }
}
