using DeterministicRollback.Core;

namespace DeterministicRollback.Networking
{
    /// <summary>
    /// Value-type batch container for 3-tick input redundancy.
    /// CRITICAL: Prevents reference trap by storing InputPayloads directly (not array reference).
    /// When added to List, entire struct is COPIED, creating snapshot.
    /// This prevents delayed packets from seeing mutated data.
    /// </summary>
    public struct InputBatch
    {
        public InputPayload i0;
        public InputPayload i1;
        public InputPayload i2;
        public int count;

        /// <summary>
        /// Get input at index using switch expression.
        /// Returns default if index out of range (0-2).
        /// </summary>
        public InputPayload Get(int index) => index switch
        {
            0 => i0,
            1 => i1,
            2 => i2,
            _ => default
        };
    }
}
