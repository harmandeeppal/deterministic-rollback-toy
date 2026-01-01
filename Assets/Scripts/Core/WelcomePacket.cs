namespace DeterministicRollback.Core
{
    /// <summary>
    /// Server handshake packet for client synchronization.
    /// </summary>
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct WelcomePacket
    {
        public uint startTick;
        public StatePayload startState;
    }
}
