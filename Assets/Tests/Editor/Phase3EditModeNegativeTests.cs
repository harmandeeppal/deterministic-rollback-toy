using System;
using NUnit.Framework;
using UnityEngine;
using DeterministicRollback.Entities;
using DeterministicRollback.Networking;

namespace DeterministicRollback.Tests.Editor
{
    /// <summary>
    /// Phase 3 negative and boundary tests.
    /// These tests focus on failure modes and edge cases (ghost data, accumulator backlog).
    /// </summary>
    public class Phase3EditModeNegativeTests
    {
        [SetUp]
        public void SetUp()
        {
            // Ensure deterministic starting conditions
            FakeNetworkPipe.Clear();
            Time.timeScale = 1f;
        }

        [Test]
        public void ClientEntity_SpiralGuard_PreservesAccumulator()
        {
            var client = new ClientEntity();
            client.Initialize();

            // Simulate 20 ticks worth of deltaTime in one frame
            client.UpdateWithDelta(20f / 60f);

            // Should clamp to MAX_TICKS_PER_FRAME (10)
            Assert.AreEqual(10u, client.CurrentTick);

            // Next call with no additional time should process the backlog (remaining 10 ticks)
            client.UpdateWithDelta(0f);
            Assert.AreEqual(20u, client.CurrentTick);
        }

        [Test]
        public void RingBuffer_Wraparound_ThrowsGhostDataException()
        {
            var buffer = new DeterministicRollback.Core.RingBuffer<int>(4);

            // Fill capacity
            for (uint t = 0; t < 4; t++) buffer[t] = (int)t;

            // Overwrite index 0 by writing tick 4 (wrap-around)
            buffer[4] = 4;

            // Accessing old tick 0 should throw ghost-data exception
            Assert.Throws<Exception>(() => { var v = buffer[0]; });
        }
    }
}
