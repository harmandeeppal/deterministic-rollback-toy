using UnityEngine;

namespace DeterministicRollback.Core
{
    /// <summary>
    /// Input snapshot at a specific tick.
    /// tick = Tick at moment of sampling.
    /// </summary>
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct InputPayload
    {
        public uint tick;
        public Vector2 inputVector;
    }
}
