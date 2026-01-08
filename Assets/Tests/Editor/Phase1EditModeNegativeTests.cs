using NUnit.Framework;
using DeterministicRollback.Core;

namespace DeterministicRollback.Tests.Editor
{
    /// <summary>
    /// Phase 1 negative/boundary tests for core data structures.
    /// </summary>
    public class Phase1EditModeNegativeTests
    {
        [Test]
        public void RingBuffer_Write_OverwriteThenReadOldTick_ThrowsGhostDataException()
        {
            var buffer = new RingBuffer<int>(4);

            // Fill capacity
            for (uint t = 0; t < 4; t++) buffer[t] = (int)t;

            // Overwrite index 0 by writing tick 4 (wrap-around)
            buffer[4] = 4;

            // Contains should report false for the old tick
            Assert.IsFalse(buffer.Contains(0));

            // Accessing old tick 0 should throw ghost-data exception
            Assert.Throws<System.Exception>(() => { var v = buffer[0]; });
        }

        [Test]
        public void RingBuffer_Contains_ReturnsFalseAfterWrap()
        {
            var buffer = new RingBuffer<int>(8);

            // Write two ticks spaced by buffer size to force wrap
            buffer[5] = 123;
            Assert.IsTrue(buffer.Contains(5));

            buffer[5 + 8] = 456; // Overwrites slot for tick 5
            Assert.IsFalse(buffer.Contains(5));
        }
    }
}