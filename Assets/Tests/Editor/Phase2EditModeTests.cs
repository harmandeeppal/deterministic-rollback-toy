using NUnit.Framework;
using UnityEngine;
using DeterministicRollback.Core;
using DeterministicRollback.Networking;

namespace DeterministicRollback.Tests.Editor
{
    /// <summary>
    /// Phase 2 Edit Mode tests - Pure value-type tests (Packet, InputBatch).
    /// These tests have no timing/coroutine dependencies and run fast in Edit Mode.
    /// </summary>
    public class Phase2EditModeTests
    {
        // ========== Step 2.1: Packet & InputBatch Tests ==========

        [Test]
        public void Packet_ValueTypeSemantics()
        {
            var packet = new Packet<InputPayload>
            {
                payload = new InputPayload { tick = 10, inputVector = Vector2.right },
                deliveryTime = 1.5f
            };

            Assert.AreEqual(10u, packet.payload.tick);
            Assert.AreEqual(1.5f, packet.deliveryTime);
        }

        [Test]
        public void InputBatch_Get()
        {
            var batch = new InputBatch
            {
                i0 = new InputPayload { tick = 1, inputVector = Vector2.right },
                i1 = new InputPayload { tick = 2, inputVector = Vector2.up },
                i2 = new InputPayload { tick = 3, inputVector = Vector2.left },
                count = 3
            };

            Assert.AreEqual(1u, batch.Get(0).tick);
            Assert.AreEqual(2u, batch.Get(1).tick);
            Assert.AreEqual(3u, batch.Get(2).tick);
            Assert.AreEqual(Vector2.right, batch.Get(0).inputVector);
            Assert.AreEqual(Vector2.up, batch.Get(1).inputVector);
            Assert.AreEqual(Vector2.left, batch.Get(2).inputVector);
        }

        [Test]
        public void InputBatch_GetOutOfRange()
        {
            var batch = new InputBatch { count = 1 };
            var result = batch.Get(5); // Out of range
            Assert.AreEqual(default(InputPayload).tick, result.tick);
        }

        [Test]
        public void InputBatch_ValueTypeCopy()
        {
            var batch1 = new InputBatch
            {
                i0 = new InputPayload { tick = 100, inputVector = Vector2.one },
                count = 1
            };

            // Copy by value
            var batch2 = batch1;
            batch2.i0 = new InputPayload { tick = 200, inputVector = Vector2.zero };

            // Original should be unchanged (value semantics)
            Assert.AreEqual(100u, batch1.Get(0).tick);
            Assert.AreEqual(200u, batch2.Get(0).tick);
        }

        [Test]
        public void FakeNetworkPipe_ClearMethod()
        {
            FakeNetworkPipe.Clear();
            
            var testBatch = new InputBatch { count = 1 };
            var testState = new StatePayload { tick = 1, confirmedInputTick = 0 };

            // Send packets
            FakeNetworkPipe.SendInput(testBatch, 1000f, 0f); // Long latency
            FakeNetworkPipe.SendState(testState, 1000f, 0f);

            // Verify packets are pending
            Assert.Greater(FakeNetworkPipe.GetPendingInputCount(), 0);
            Assert.Greater(FakeNetworkPipe.GetPendingStateCount(), 0);

            // Clear
            FakeNetworkPipe.Clear();

            // Verify cleared
            Assert.AreEqual(0, FakeNetworkPipe.GetPendingInputCount());
            Assert.AreEqual(0, FakeNetworkPipe.GetPendingStateCount());
        }
    }
}
