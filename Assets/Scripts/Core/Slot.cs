namespace DeterministicRollback.Core
{
    /// <summary>
    /// Slot wrapper to prevent ghost data bug.
    /// CRITICAL: Without tick validation, buffer[tick % size] always returns data.
    /// When buffer wraps (Tick 200 overwrites Tick 100 at same index),
    /// requesting Tick 100 would incorrectly return Tick 200 data,
    /// causing catastrophic physics explosions during reconciliation.
    /// </summary>
    public struct Slot<T> where T : struct
    {
        public uint tick;
        public T data;
        public bool isValid;
    }
}
