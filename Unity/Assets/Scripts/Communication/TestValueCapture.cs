using PhysicalDigital.Protocols;

namespace PhysicalDigital.Communication
{
    public struct TestFrame
    {
        public ControllerState State;
        public byte[] Raw;
        public string Protocol;
    }

    public sealed class TestValueCapture
    {
        private bool awaiting;
        private bool hasResult;
        private TestFrame result;

        public void Begin()
        {
            awaiting = true;
            hasResult = false;
        }

        public void Offer(in ControllerState state, IProtocolDecoder decoder)
        {
            if (!awaiting)
            {
                return;
            }
            result = new TestFrame { State = state, Raw = decoder.GetLastFrame(), Protocol = decoder.Name };
            hasResult = true;
            awaiting = false;
        }

        public bool TryGet(out TestFrame frame)
        {
            frame = result;
            return hasResult;
        }

        public void Reset()
        {
            awaiting = false;
            hasResult = false;
        }
    }
}
