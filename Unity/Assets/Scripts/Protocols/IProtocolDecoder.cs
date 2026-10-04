namespace PhysicalDigital.Protocols
{
    public interface IProtocolDecoder
    {
        string Name { get; }
        DecoderStats Stats { get; }

        bool Feed(byte value, out ControllerState state);

        byte[] GetLastFrame();
    }
}
