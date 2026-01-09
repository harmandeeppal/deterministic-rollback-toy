using NUnit.Framework;
using UnityEngine;
using DeterministicRollback.Entities;
using DeterministicRollback.Networking;
using DeterministicRollback.Core;

namespace DeterministicRollback.Tests.Editor
{
    public class Phase4EditModeTests
    {
        [SetUp]
        public void SetUp()
        {
            // Ensure FakeNetworkPipe is clean and time is running
            FakeNetworkPipe.Clear();
            Time.timeScale = 1f;
        }

        [Test]
        public void ServerEntity_TickRate()
        {
            var go = new GameObject("server");
            var server = go.AddComponent<ServerEntity>();
            server.Initialize();

            uint initial = server.ServerTick;
            for (int i = 0; i < 6000; i++)
            {
                server.UpdateWithDelta(1f / 60f);
            }

            Assert.AreEqual(6000u, server.ServerTick - initial);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void ServerEntity_ReceiveInputBatch()
        {
            var go = new GameObject("server");
            var server = go.AddComponent<ServerEntity>();
            server.Initialize();

            var batch = new InputBatch { i0 = new InputPayload { tick = 5, inputVector = Vector2.right }, count = 1 };
            FakeNetworkPipe.SendInput(batch, 0f, 0f);
            // Deliver immediately in EditMode tests (run twice to avoid timing flakiness)
            FakeNetworkPipe.ProcessPackets();
            FakeNetworkPipe.ProcessPackets();

            Assert.IsTrue(server.ServerInputBuffer.Contains(5));
            Object.DestroyImmediate(go);
        }

        [Test]
        public void ServerEntity_InputPrediction()
        {
            var go = new GameObject("server");
            var server = go.AddComponent<ServerEntity>();
            server.Initialize();

            var batch = new InputBatch { i0 = new InputPayload { tick = 10, inputVector = Vector2.right }, count = 1 };
            FakeNetworkPipe.SendInput(batch, 0f, 0f);
            FakeNetworkPipe.ProcessPackets();
            FakeNetworkPipe.ProcessPackets();

            // Simulate 15 ticks
            for (int i = 0; i < 15; i++) server.UpdateWithDelta(1f / 60f);

            Assert.Greater(server.ServerTick, 10u);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void ServerEntity_InputConfirmation()
        {
            var go = new GameObject("server");
            var server = go.AddComponent<ServerEntity>();
            server.Initialize();

            var batch1 = new InputBatch { i0 = new InputPayload { tick = 5, inputVector = Vector2.right }, count = 1 };
            var batch2 = new InputBatch { i0 = new InputPayload { tick = 7, inputVector = Vector2.right }, count = 1 };
            var batch3 = new InputBatch { i0 = new InputPayload { tick = 9, inputVector = Vector2.right }, count = 1 };

            FakeNetworkPipe.SendInput(batch1, 0f, 0f);
            FakeNetworkPipe.SendInput(batch2, 0f, 0f);
            FakeNetworkPipe.SendInput(batch3, 0f, 0f);
            FakeNetworkPipe.ProcessPackets();

            // Process a few ticks to let server apply confirmations
            for (int i = 0; i < 5; i++) server.UpdateWithDelta(1f / 60f);

            Assert.AreEqual(9u, server.lastConfirmedInputTick);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void ServerEntity_ZeroGC_PerTick()
        {
            var go = new GameObject("server");
            var server = go.AddComponent<ServerEntity>();
            server.Initialize();

            long before = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
            for (int i = 0; i < 100; i++) server.UpdateWithDelta(1f / 60f);
            long after = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();

            // Allow small noise, but ensure no large allocations in hot path
            Assert.IsTrue(after - before < 1024);

            Object.DestroyImmediate(go);
        }
    }
}