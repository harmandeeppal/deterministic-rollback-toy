using UnityEngine;

namespace DeterministicRollback.Core
{
    /// <summary>
    /// State snapshot after integration.
    /// tick = Tick AFTER integration (Resulting State).
    /// confirmedInputTick = Server's ACK for piggybacked confirmation.
    /// </summary>
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct StatePayload
    {
        public uint tick;
        public Vector2 position;
        public Vector2 velocity;
        public uint confirmedInputTick;
    }
}
