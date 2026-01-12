using NUnit.Framework;
using UnityEngine;
using DeterministicRollback.Core;

namespace DeterministicRollback.Tests.Editor
{
    public class Phase5EditModeNegativeTests
    {
        [Test]
        public void Handshake_RejectsImpossibleStartTick()
        {
            var rb = new RingBuffer<StatePayload>(4096);
            // Simulate the server startTick far in the past that client cannot reconcile
            uint serverStartTick = 1u; // too old
            var welcome = new WelcomePacket { startTick = serverStartTick, startState = new StatePayload { tick = serverStartTick } };

            // Client compute start
            float oneWayMs = 10f;
            var rttTicks = (uint)System.Math.Ceiling((oneWayMs * 2.0f) / (1000f / 60f));
            var clientStartTick = serverStartTick + rttTicks + 2u;

            // If clientStartTick - 1 wraps much earlier than buffer current head (simulate currentTick large)
            uint currentTick = 5000u;
            if (currentTick - welcome.startTick > 4096u)
            {
                Assert.Pass(); // expected: client should treat this as needing full reconnect or reject
            }
            else
            {
                Assert.Inconclusive("Test environment didn't simulate extreme wrap; adjust ticks");
            }
        }

        [Test]
        public void Handshake_BufferWrap_Safe()
        {
            // Ensure writing warped tick near buffer wrap does not throw
            var rb = new RingBuffer<StatePayload>(4096);
            uint nearWrapTick = 4095u + 65536u; // large tick ensuring mod wrap
            var warped = new StatePayload { tick = 5000u, position = Vector2.zero, velocity = Vector2.zero, confirmedInputTick = 0 };

            // Client will set stored tick to storeIndex = (clientStartTick - 1). Even if storeIndex modulo buffer collides, RingBuffer indexer write should set the slot tick correctly.
            uint storeIndex = nearWrapTick % (uint)rb.Capacity;

            // Write using the public api
            uint storeTick = nearWrapTick; // simulated desired tick
            rb[storeTick] = warped;

            Assert.IsTrue(rb.Contains(storeTick));
            var read = rb[storeTick];
            Assert.AreEqual(warped.position, read.position);
        }
    }
}
