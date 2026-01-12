using NUnit.Framework;
using UnityEngine;
using DeterministicRollback.Core;
using DeterministicRollback.Entities;
using DeterministicRollback.Networking;

namespace DeterministicRollback.Tests.Editor
{
    public class Phase6EditModeNegativeTests
    {
        [Test]
        public void Reconciliation_BufferWrap_Safe()
        {
            // Ensure writing a correction near buffer wrap does not throw
            var rb = new RingBuffer<StatePayload>(4096);
            uint nearWrapTick = 4095u + 65536u; // large tick ensuring modulo wrap
            var corrected = new StatePayload { tick = nearWrapTick, position = Vector2.one, velocity = Vector2.zero, confirmedInputTick = 0 };

            // Write should not throw and read should return same data
            rb[nearWrapTick] = corrected;
            Assert.IsTrue(rb.Contains(nearWrapTick));
            var read = rb[nearWrapTick];
            Assert.AreEqual(corrected.position, read.position);
        }

        [Test]
        public void Reconciliation_NonApplicableCorrection_Ignored()
        {
            // If a server correction refers to a tick older than buffer history, client must hard-snap or ignore safely
            var client = new DeterministicRollback.Entities.ClientEntity();
            client.Initialize();

            // Simulate small number of ticks
            for (int i = 0; i < 10; i++) client.UpdateWithDelta(ClientEntity.FIXED_DELTA_TIME);

            // Correction for a tick far in the past (outside buffer range)
            var ancient = new StatePayload { tick = 1u, position = Vector2.zero, velocity = Vector2.zero, confirmedInputTick = 0 };

            // Should not throw; after reconciliation CurrentTick should be > 1
            // Send ancient correction via network pipe
            FakeNetworkPipe.SendState(ancient, latencyMs: 0f, lossChance: 0f);
            FakeNetworkPipe.ProcessPackets();
            client.UpdateWithDelta(0f);

            Assert.Greater(client.CurrentTick, 1u);
        }
    }
}
