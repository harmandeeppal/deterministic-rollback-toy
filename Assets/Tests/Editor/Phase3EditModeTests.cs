using System;
using NUnit.Framework;
using UnityEngine;
using DeterministicRollback.Entities;
using DeterministicRollback.Networking;

namespace DeterministicRollback.Tests.Editor
{
    /// <summary>
    /// Phase 3 tests - Client entity simulation with time accumulator and redundant input.
    /// </summary>
    public class Phase3EditModeTests
    {
        [SetUp]
        public void SetUp()
        {
            // Ensure deterministic starting conditions
            FakeNetworkPipe.Clear();
            Time.timeScale = 1f;
        }

        [Test]
        public void ClientEntity_TickRate()
        {
            var client = new ClientEntity();
            client.Initialize();

            // Run 6000 frames (100 seconds at 60Hz)
            for (int i = 0; i < 6000; i++)
            {
                client.UpdateWithDelta(1f / 60f);
            }

            // currentTick should be 6000 (0 is spawn, 1-6000 simulated)
            Assert.AreEqual(6000u, client.CurrentTick);
        }

        [Test]
        public void ClientEntity_DeterministicPath()
        {
            var client1 = new ClientEntity();
            var client2 = new ClientEntity();

            // Provide deterministic identical input
            client1.InputProvider = () => Vector2.right;
            client2.InputProvider = () => Vector2.right;

            int ticks = 300; // 5 seconds
            for (int i = 0; i < ticks; i++)
            {
                client1.UpdateWithDelta(1f / 60f);
                client2.UpdateWithDelta(1f / 60f);
            }

            // Verify both clients have identical StateBuffer contents
            for (uint t = 0; t <= (uint)ticks; t++)
            {
                var s1 = client1.GetState(t);
                var s2 = client2.GetState(t);

                Assert.AreEqual(s1.position.x, s2.position.x, 0.0001f);
                Assert.AreEqual(s1.position.y, s2.position.y, 0.0001f);
                Assert.AreEqual(s1.velocity.x, s2.velocity.x, 0.0001f);
                Assert.AreEqual(s1.velocity.y, s2.velocity.y, 0.0001f);
            }
        }

        [Test]
        public void ClientEntity_SendsBatchesToNetwork()
        {
            var client = new ClientEntity();
            int received = 0;
            FakeNetworkPipe.Clear();
            FakeNetworkPipe.OnInputBatchReceived += (batch) => received++;

            // Run a few ticks
            for (int i = 0; i < 10; i++)
            {
                client.UpdateWithDelta(1f / 60f);
                // Process pending packets immediately (simulated network)
                FakeNetworkPipe.ProcessPackets();
            }

            Assert.Greater(received, 0);
        }

        // Negative tests moved to `Assets/Tests/Editor/Phase3EditModeNegativeTests.cs`
        // See: ClientEntity_SpiralGuard_PreservesAccumulator (boundary/backlog) and RingBuffer_Wraparound_ThrowsGhostDataException (ghost-data protection) for negative test coverage.

        // Negative tests moved to `Assets/Tests/Editor/Phase3EditModeNegativeTests.cs`
        // See: RingBuffer_Wraparound_ThrowsGhostDataException (ghost-data protection) and related tests.

#if UNITY_EDITOR
        [Test]
        public void ClientEntity_ZeroGC_PerTick()
        {
            // Verify per-tick Update does not allocate memory in hot path
            long allocBefore = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();

            var client = new ClientEntity();
            client.UpdateWithDelta(1f / 60f);

            long allocAfter = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
            Assert.AreEqual(0L, allocAfter - allocBefore, "Per-tick Update allocated GC memory");
        }
#endif
    }
}