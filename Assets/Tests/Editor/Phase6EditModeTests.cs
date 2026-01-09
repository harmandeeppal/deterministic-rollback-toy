using NUnit.Framework;
using UnityEngine;
using DeterministicRollback.Entities;
using DeterministicRollback.Core;
using DeterministicRollback.Networking;

namespace DeterministicRollback.Tests
{
    public class Phase6EditModeTests
    {
        [Test]
        public void Reconciliation_SmallCorrection_AppliesAndResimulates()
        {
            var client = new ClientEntity();
            client.Initialize();
            client.InputProvider = () => Vector2.right; // deterministic movement

            // Simulate client 60 ticks ahead
            for (int i = 0; i < 60; i++)
            {
                client.UpdateWithDelta(ClientEntity.FIXED_DELTA_TIME);
            }

            // Sanity: client has state for tick 30
            Assert.IsTrue(client.GetState(30).tick == 30);

            // Server authoritative correction at tick 30 (different position)
            var correction = new StatePayload { tick = 30u, position = new Vector2(5f, 0f), velocity = Vector2.zero, confirmedInputTick = 0 };

            bool reconciled = false;
            client.OnReconciliationPerformed += () => reconciled = true;

            // Send authoritative state via network pipe (no latency)
            FakeNetworkPipe.SendState(correction, latencyMs: 0f, lossChance: 0f);
            FakeNetworkPipe.ProcessPackets();

            // Trigger reconciliation (no time advance needed)
            client.UpdateWithDelta(0f);

            Assert.IsTrue(reconciled, "Reconciliation should have been performed");
            var corrected = client.GetState(30u);
            Assert.AreEqual(5f, corrected.position.x, 0.0001f);
        }

        [Test]
        public void Reconciliation_HardSnap()
        {
            var client = new ClientEntity();
            client.Initialize();
            client.InputProvider = () => Vector2.right;

            // Simulate client well beyond MAX_RESIM_STEPS to force hard snap
            for (int i = 0; i < 400; i++)
            {
                client.UpdateWithDelta(ClientEntity.FIXED_DELTA_TIME);
            }

            uint oldTick = client.CurrentTick;

            // Ancient server state (too old to reconcile)
            var ancient = new StatePayload { tick = 1u, position = new Vector2(100f, 0f), velocity = Vector2.zero, confirmedInputTick = 0 };

            // Send ancient correction via network pipe
            FakeNetworkPipe.SendState(ancient, latencyMs: 0f, lossChance: 0f);

            // Process packets immediately (no latency)
            FakeNetworkPipe.ProcessPackets();

            // Trigger reconciliation
            client.UpdateWithDelta(0f);

            Assert.Greater(client.CurrentTick, 1u);
            Assert.Less(client.CurrentTick, oldTick, "Hard snap should move client back but not crash");
        }

        [Test]
        public void Reconciliation_ServerEmitCorrection_Applies()
        {
            var client = new ClientEntity();
            client.Initialize();
            client.InputProvider = () => Vector2.right;

            var go = new GameObject("ServerEntity_TestGO");
            var server = go.AddComponent<ServerEntity>();
            server.Initialize();

            // Simulate server enough ticks so its current state tick is > 30
            for (int i = 0; i < 40; i++) server.UpdateWithDelta(ServerEntity.FIXED_DELTA_TIME);
            for (int i = 0; i < 60; i++) client.UpdateWithDelta(ClientEntity.FIXED_DELTA_TIME);

            // Create a correction at tick 30
            var correction = new StatePayload { tick = 30u, position = new Vector2(7f, 0f), velocity = Vector2.zero, confirmedInputTick = 0 };

            bool reconciled = false;
            client.OnReconciliationPerformed += () => reconciled = true;

            // Server emits correction via FakeNetworkPipe
            server.EmitStateCorrection(correction, latencyMs: 0f, lossChance: 0f);

            // Process packets immediately (no latency)
            FakeNetworkPipe.ProcessPackets();

            // Trigger client reconciliation
            client.UpdateWithDelta(0f);

            Assert.IsTrue(reconciled);
            var corrected = client.GetState(30u);
            Assert.AreEqual(7f, corrected.position.x, 0.0001f);

            // Cleanup server GameObject
            Object.DestroyImmediate(go);
        }
    }
}
