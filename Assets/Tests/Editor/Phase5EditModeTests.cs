using NUnit.Framework;
using UnityEngine;
using DeterministicRollback.Entities;
using DeterministicRollback.Core;

namespace DeterministicRollback.Tests
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
    }
}
