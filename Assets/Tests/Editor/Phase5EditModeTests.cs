using NUnit.Framework;
using UnityEngine;
using DeterministicRollback.Entities;
using DeterministicRollback.Core;
using DeterministicRollback.Networking;

namespace DeterministicRollback.Tests.Editor
{
    public class Phase5EditModeTests
    {
        const uint INPUT_BUFFER_HEADROOM = 2;

        static uint ComputeRttTicks(float oneWayMs)
        {
            // Match client logic: use double precision and subtract a tiny epsilon
            // before ceiling to avoid floating-point upward rounding.
            double rttMs = oneWayMs * 2.0;
            double tickMs = 1000.0 / 60.0; // 16.66667ms
            uint ticks = (uint)System.Math.Ceiling(rttMs / tickMs - 1e-9);
            return ticks < 3 ? 3u : ticks;
        }

        [Test]
        public void Handshake_Minimum_RTT_Enforced()
        {
            var oneWay = 0f;
            var ticks = ComputeRttTicks(oneWay);
            Assert.AreEqual(3u, ticks);
        }

        [Test]
        public void Handshake_CurrentTickCalculation_IsCorrect()
        {
            uint startTick = 100u;
            float oneWayMs = 100f; // example
            var rttTicks = ComputeRttTicks(oneWayMs);
            var expected = startTick + rttTicks + INPUT_BUFFER_HEADROOM;

            Assert.AreEqual(114u, expected);
        }

        [Test]
        public void Handshake_LastConfirmedInputTick_Synced()
        {
            // Server welcome packet carries confirmedInputTick
            var serverState = new StatePayload { tick = 200, position = Vector2.zero, velocity = Vector2.zero, confirmedInputTick = 123 };
            var welcome = new WelcomePacket { startTick = 200, startState = serverState };

            // Client entity handles welcome packet
            var client = new ClientEntity();
            client.HandleWelcomePacket(welcome, oneWayMs: 100f);

            Assert.AreEqual(123u, client.lastServerConfirmedInputTick);
        }

        [Test]
        public void Handshake_HandleWelcomePacket_SetsClientTickAndState()
        {
            var serverState = new StatePayload { tick = 200, position = new Vector2(3,4), velocity = Vector2.zero, confirmedInputTick = 77 };
            var welcome = new WelcomePacket { startTick = 200, startState = serverState };

            var client = new ClientEntity();
            client.HandleWelcomePacket(welcome, oneWayMs: 50f);

            // Compute expected
            var rttTicks = ComputeRttTicks(50f);
            uint expectedStart = welcome.startTick + rttTicks + ClientEntity.INPUT_BUFFER_HEADROOM;

            Assert.AreEqual(expectedStart, client.CurrentTick);
            var stored = client.GetState(expectedStart - 1);
            Assert.AreEqual(new Vector2(3,4), stored.position);
            Assert.AreEqual(expectedStart - 1, stored.tick);
            Assert.AreEqual(77u, client.lastServerConfirmedInputTick);
        }

        [Test]
        public void Handshake_WarpedState_AllowsRingBufferWrite()
        {
            // Simulate server start and client calculation
            uint serverStartTick = 100u;
            float oneWayMs = 50f; // RTT 100ms -> ~6 ticks
            var rttTicks = ComputeRttTicks(oneWayMs);
            var clientStartTick = serverStartTick + rttTicks + INPUT_BUFFER_HEADROOM;

            // Warped state should be written at clientStartTick - 1
            uint storeTick = clientStartTick - 1;

            var rb = new RingBuffer<StatePayload>(4096);

            var warped = new StatePayload { tick = storeTick, position = new Vector2(1, 2), velocity = Vector2.zero, confirmedInputTick = 0 };

            // This should not throw (indexer enforces tick == requested tick on read; write should set slot correctly)
            rb[storeTick] = warped;

            Assert.IsTrue(rb.Contains(storeTick));
            var read = rb[storeTick];
            Assert.AreEqual(warped.position, read.position);
            Assert.AreEqual(warped.tick, read.tick);
        }

        [Test]
        public void Phase5_ClientToServer_InputFlow()
        {
            FakeNetworkPipe.Clear();

            // Setup client and server
            var client = new ClientEntity();
            client.Initialize();
            client.latencyMs = 0f;
            client.lossChance = 0f;
            client.InputProvider = () => Vector2.right;

            var go = new GameObject("ServerEntity_TestGO");
            var server = go.AddComponent<ServerEntity>();
            server.Initialize();

            // Run a few client ticks which should enqueue input batches
            for (int i = 0; i < 5; i++) client.UpdateWithDelta(ClientEntity.FIXED_DELTA_TIME);

            // Deliver pending network packets to server
            FakeNetworkPipe.ProcessPackets();

            // Server should have received inputs for early ticks (1 and 5)
            Assert.IsTrue(server.ServerInputBuffer.Contains(1u));
            Assert.IsTrue(server.ServerInputBuffer.Contains(5u));

            UnityEngine.Object.DestroyImmediate(go);
        }

        [Test]
        public void Phase5_ServerToClient_StateFlow()
        {
            FakeNetworkPipe.Clear();

            // Setup client and ensure it subscribes to network events
            var client = new ClientEntity();
            client.Initialize();
            client.InputProvider = () => Vector2.zero;
            client.UpdateWithDelta(0f); // ensures subscription

            // Setup server and advance it several ticks so it emits states
            var go = new GameObject("ServerEntity_TestGO");
            var server = go.AddComponent<ServerEntity>();
            server.Initialize();
            for (int i = 0; i < 10; i++) server.UpdateWithDelta(ServerEntity.FIXED_DELTA_TIME);

            // Prepare an authoritative state from the server (use current state but set explicit tick)
            uint targetTick = server.ServerTick - 1u;
            var authoritative = server.CurrentState;
            authoritative.tick = targetTick;
            authoritative.position = new Vector2(9f, 0f);

            // Inject authoritative state directly for deterministic verification
            client.DebugInjectServerState(authoritative);

            // No time advancement required; the injection performs reconciliation immediately
            var stored = client.GetState(targetTick);
            Assert.AreEqual(authoritative.position.x, stored.position.x, 0.0001f);

            UnityEngine.Object.DestroyImmediate(go);
        }
    }
}
