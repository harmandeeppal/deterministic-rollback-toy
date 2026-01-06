using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.Collections;
using DeterministicRollback.Core;
using DeterministicRollback.Networking;

namespace DeterministicRollback.Tests.PlayMode
{
    /// <summary>
    /// Phase 2 Play Mode tests - FakeNetworkPipe integration tests with coroutines.
    /// These tests require Play Mode runtime for timing/event simulation.
    /// </summary>
    public class Phase2PlayModeTests
    {
        private float _testStartTime;

        [SetUp]
        public void Setup()
        {
            _testStartTime = Time.realtimeSinceStartup;
            Time.timeScale = 1f;
        }

        // ========== Step 2.2: FakeNetworkPipe Runtime Tests ==========

        [UnityTest]
        public IEnumerator FakeNetworkPipe_SendAndReceiveInput()
        {
            FakeNetworkPipe.Clear();
            
            InputBatch receivedBatch = default;
            bool received = false;
            FakeNetworkPipe.OnInputBatchReceived += (batch) => 
            { 
                receivedBatch = batch; 
                received = true; 
            };

            var testBatch = new InputBatch
            {
                i0 = new InputPayload { tick = 42, inputVector = Vector2.right },
                count = 1
            };

            // Send with 50ms latency, 0% loss
            FakeNetworkPipe.SendInput(testBatch, 50f, 0f);

            // Wait for delivery (use WaitForSecondsRealtime for EditMode compatibility)
            yield return new WaitForSecondsRealtime(0.1f);
            
            // Manually process packets (EditMode doesn't auto-call Update)
            FakeNetworkPipe.ProcessPackets();

            Assert.IsTrue(received, "Packet should have been received");
            Assert.AreEqual(42u, receivedBatch.Get(0).tick);
        }

        [UnityTest]
        public IEnumerator FakeNetworkPipe_SendAndReceiveState()
        {
            FakeNetworkPipe.Clear();
            
            StatePayload receivedState = default;
            bool received = false;
            FakeNetworkPipe.OnStateReceived += (state) => 
            { 
                receivedState = state; 
                received = true; 
            };

            var testState = new StatePayload
            {
                tick = 100,
                position = new Vector2(5f, 3f),
                velocity = Vector2.up,
                confirmedInputTick = 95
            };

            // Send with 50ms latency, 0% loss
            FakeNetworkPipe.SendState(testState, 50f, 0f);

            // Wait for delivery (use WaitForSecondsRealtime for EditMode compatibility)
            yield return new WaitForSecondsRealtime(0.1f);
            
            // Manually process packets (EditMode doesn't auto-call Update)
            FakeNetworkPipe.ProcessPackets();

            Assert.IsTrue(received, "Packet should have been received");
            Assert.AreEqual(100u, receivedState.tick);
            Assert.AreEqual(5f, receivedState.position.x, 0.001f);
        }

        [UnityTest]
        public IEnumerator FakeNetworkPipe_PacketLoss()
        {
            FakeNetworkPipe.Clear();
            
            int receivedCount = 0;
            FakeNetworkPipe.OnInputBatchReceived += (batch) => receivedCount++;

            var testBatch = new InputBatch { count = 1 };

            // Send 100 packets with 100% loss
            for (int i = 0; i < 100; i++)
            {
                FakeNetworkPipe.SendInput(testBatch, 10f, 1.0f);
            }

            // Manually process packets
            FakeNetworkPipe.ProcessPackets();

            yield return new WaitForSeconds(0.1f);

            // All packets should be dropped
            Assert.AreEqual(0, receivedCount, "All packets should be lost with 100% loss rate");
        }

        [UnityTest]
        public IEnumerator FakeNetworkPipe_TimeOrderedDelivery()
        {
            FakeNetworkPipe.Clear();
            
            var receivedTicks = new System.Collections.Generic.List<uint>();
            FakeNetworkPipe.OnStateReceived += (state) => receivedTicks.Add(state.tick);

            // Send packet 1 with HIGH latency (200ms)
            var state1 = new StatePayload { tick = 1, confirmedInputTick = 0 };
            FakeNetworkPipe.SendState(state1, 200f, 0f);

            // Send packet 2 with LOW latency (50ms)
            var state2 = new StatePayload { tick = 2, confirmedInputTick = 0 };
            FakeNetworkPipe.SendState(state2, 50f, 0f);
            
            // Wait for low-latency packet to be ready (use WaitForSecondsRealtime for EditMode compatibility)
            yield return new WaitForSecondsRealtime(0.06f);
            
            // Manually process packets
            FakeNetworkPipe.ProcessPackets();

            // Packet 2 should arrive first (time-ordered delivery)
            Assert.AreEqual(1, receivedTicks.Count, "First packet should have arrived");
            Assert.AreEqual(2u, receivedTicks[0], "Low-latency packet should arrive first");

            // Wait for high-latency packet (use WaitForSecondsRealtime for EditMode compatibility)
            yield return new WaitForSecondsRealtime(0.15f);
            
            // Manually process packets again
            FakeNetworkPipe.ProcessPackets();

            Assert.AreEqual(2, receivedTicks.Count, "Second packet should have arrived");
            Assert.AreEqual(1u, receivedTicks[1], "High-latency packet should arrive second");
        }

        [UnityTest]
        public IEnumerator FakeNetworkPipe_PauseHandling()
        {
            FakeNetworkPipe.Clear();
            
            int receivedCount = 0;
            FakeNetworkPipe.OnInputBatchReceived += (batch) => receivedCount++;

            var testBatch = new InputBatch
            {
                i0 = new InputPayload { tick = 1 },
                count = 1
            };

            // Pause simulation
            float originalTimeScale = Time.timeScale;
            Time.timeScale = 0f;

            // Send packet with minimal latency
            FakeNetworkPipe.SendInput(testBatch, 10f, 0f);

            // Wait (in real time, but simulation is paused)
            yield return new WaitForSecondsRealtime(0.1f);
            
            // Try to process packets (should be blocked by pause)
            FakeNetworkPipe.ProcessPackets();

            // Packet should NOT be delivered while paused
            Assert.AreEqual(0, receivedCount, "No packets should be delivered while paused");

            // Unpause
            Time.timeScale = originalTimeScale;

            // Wait for delivery
            yield return new WaitForSeconds(0.05f);
            
            // Manually process packets after unpause
            FakeNetworkPipe.ProcessPackets();

            // Now packet should be delivered
            Assert.AreEqual(1, receivedCount, "Packet should be delivered after unpause");
        }

        [UnityTest]
        public IEnumerator FakeNetworkPipe_MultiplePacketsSameFrame()
        {
            FakeNetworkPipe.Clear();
            
            // Manually process packets
            FakeNetworkPipe.ProcessPackets();
            
            var receivedTicks = new System.Collections.Generic.List<uint>();
            FakeNetworkPipe.OnStateReceived += (state) => receivedTicks.Add(state.tick);

            // Send 5 packets with same minimal latency (should all arrive same frame)
            for (uint i = 0; i < 5; i++)
            {
                var state = new StatePayload { tick = i, confirmedInputTick = 0 };
                FakeNetworkPipe.SendState(state, 10f, 0f);
            }

            // Wait for delivery (use WaitForSecondsRealtime for EditMode compatibility)
            yield return new WaitForSecondsRealtime(0.05f);
            
            // Manually process packets
            FakeNetworkPipe.ProcessPackets();

            // All packets should be delivered
            Assert.AreEqual(5, receivedTicks.Count, "All 5 packets should be delivered");
        }

        // Cleanup after tests
        [TearDown]
        public void TearDown()
        {
            FakeNetworkPipe.Clear();
            Time.timeScale = 1f; // Reset time scale
            
            // Clear event subscriptions
            var eventField = typeof(FakeNetworkPipe).GetField("OnInputBatchReceived", 
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (eventField != null)
            {
                eventField.SetValue(null, null);
            }

            eventField = typeof(FakeNetworkPipe).GetField("OnStateReceived", 
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (eventField != null)
            {
                eventField.SetValue(null, null);
            }
        }
    }
}
