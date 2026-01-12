using NUnit.Framework;
using UnityEngine;
using DeterministicRollback.Entities;
using DeterministicRollback.Networking;
using DeterministicRollback.Core;

namespace DeterministicRollback.Tests.Editor
{
    public class Phase4EditModeNegativeTests
    {
        [SetUp]
        public void SetUp()
        {
            FakeNetworkPipe.Clear();
            Time.timeScale = 1f;
        }

        [Test]
        public void ServerEntity_SpiralGuard_PreservesAccumulator()
        {
            var go = new GameObject("server");
            var server = go.AddComponent<ServerEntity>();
            server.Initialize();

            // Provide a huge delta that would require > MAX_TICKS_PER_FRAME
            server.UpdateWithDelta(1.0f); // 1 second ~ 60 ticks

            // Should have processed at most MAX_TICKS_PER_FRAME ticks
            Assert.LessOrEqual(server.ticksProcessed, ServerEntity.MAX_TICKS_PER_FRAME);

            // Timer should not be reset to zero (some backlog remains)
            Assert.Greater(server.GetTimer(), 0f);

            UnityEngine.Object.DestroyImmediate(go);
        }

        [Test]
        public void ServerEntity_InputBuffer_RejectsOldTicks()
        {
            var go = new GameObject("server");
            var server = go.AddComponent<ServerEntity>();
            server.Initialize();

            // Attempt to insert an old tick (less than serverTick)
            var oldInput = new InputPayload { tick = 0, inputVector = Vector2.right };
            // Simulate receiving directly via event
            var batch = new InputBatch { i0 = oldInput, count = 1 };
            FakeNetworkPipe.SendInput(batch, 0f, 0f);
            FakeNetworkPipe.ProcessPackets();
            FakeNetworkPipe.ProcessPackets();

            Assert.IsFalse(server.ServerInputBuffer.Contains(0));
            UnityEngine.Object.DestroyImmediate(go);
        }

        [Test]
        public void ServerEntity_InputBuffer_RejectsFutureTicks()
        {
            var go = new GameObject("server");
            var server = go.AddComponent<ServerEntity>();
            server.Initialize();

            // Insert a tick far in the future (beyond buffer capacity)
            var futureInput = new InputPayload { tick = server.ServerTick + 5000, inputVector = Vector2.right };
            var batch = new InputBatch { i0 = futureInput, count = 1 };
            FakeNetworkPipe.SendInput(batch, 0f, 0f);
            FakeNetworkPipe.ProcessPackets();

            Assert.IsFalse(server.ServerInputBuffer.Contains(futureInput.tick));
            UnityEngine.Object.DestroyImmediate(go);
        }

        [Test]
        public void ServerEntity_ConfirmedInputTick_NeverDecreases()
        {
            var go = new GameObject("server");
            var server = go.AddComponent<ServerEntity>();
            server.Initialize();

            var batch1 = new InputBatch { i0 = new InputPayload { tick = 5 }, count = 1 };
            var batch2 = new InputBatch { i0 = new InputPayload { tick = 3 }, count = 1 };

            FakeNetworkPipe.SendInput(batch1, 0f, 0f);
            FakeNetworkPipe.SendInput(batch2, 0f, 0f);
            FakeNetworkPipe.ProcessPackets();

            // Process a few ticks to update lastConfirmedInputTick
            for (int i = 0; i < 3; i++) server.UpdateWithDelta(1f / 60f);

            Assert.GreaterOrEqual(server.lastConfirmedInputTick, 5u);
            UnityEngine.Object.DestroyImmediate(go);
        }
    }
}