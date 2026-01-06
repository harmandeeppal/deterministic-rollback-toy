using System;
using System.Collections.Generic;
using UnityEngine;
using DeterministicRollback.Core;

namespace DeterministicRollback.Networking
{
    /// <summary>
    /// Fake network simulation with latency, packet loss, and time-ordered delivery.
    /// CRITICAL: Uses List (not Queue) to allow out-of-order delivery based on deliveryTime.
    /// UDP allows packets with low latency to overtake high-latency packets (no head-of-line blocking).
    /// </summary>
    public class FakeNetworkPipe : MonoBehaviour
    {
        // Singleton instance
        private static FakeNetworkPipe _instance;
        public static FakeNetworkPipe Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("FakeNetworkPipe");
                    _instance = go.AddComponent<FakeNetworkPipe>();
                    
                    // Only use DontDestroyOnLoad in PlayMode
                    if (Application.isPlaying)
                    {
                        DontDestroyOnLoad(go);
                    }
                }
                return _instance;
            }
        }

        // Two channels: Input and State
        private static List<Packet<InputBatch>> _inputList = new List<Packet<InputBatch>>(1024);
        private static List<Packet<StatePayload>> _stateList = new List<Packet<StatePayload>>(1024);

        // Static events for subscribers
        public static event Action<InputBatch> OnInputBatchReceived;
        public static event Action<StatePayload> OnStateReceived;

        /// <summary>
        /// Get current time for packet delivery calculations.
        /// DESIGN NOTE: Production systems should use ITimeProvider abstraction for testability.
        /// This implementation uses direct Unity API for simplicity in a learning/reference context.
        /// See README Time Abstraction Trade-off section.
        /// </summary>
        /// <returns>Time.unscaledTime in PlayMode (production), Time.realtimeSinceStartup in EditMode (tests)</returns>
        private static float GetCurrentTime() => Application.isPlaying ? Time.unscaledTime : Time.realtimeSinceStartup;

        /// <summary>
        /// Send input batch with simulated latency and packet loss.
        /// CRITICAL: InputBatch is copied by value into List (snapshot semantics).
        /// </summary>
        public static void SendInput(InputBatch batch, float latencyMs, float lossChance)
        {
            // Packet loss simulation
            if (UnityEngine.Random.value < lossChance)
            {
                return; // Packet dropped
            }

            // Calculate delivery time
            float deliveryTime = GetCurrentTime() + (latencyMs / 1000f);

            // Add to list (batch is copied by value)
            _inputList.Add(new Packet<InputBatch>
            {
                payload = batch,
                deliveryTime = deliveryTime
            });
        }

        /// <summary>
        /// Send state with simulated latency and packet loss.
        /// </summary>
        public static void SendState(StatePayload state, float latencyMs, float lossChance)
        {
            // Packet loss simulation
            if (UnityEngine.Random.value < lossChance)
            {
                return; // Packet dropped
            }

            // Calculate delivery time
            float deliveryTime = GetCurrentTime() + (latencyMs / 1000f);

            // Add to list
            _stateList.Add(new Packet<StatePayload>
            {
                payload = state,
                deliveryTime = deliveryTime
            });
        }

        /// <summary>
        /// Process packets in time-order (not insertion-order).
        /// CRITICAL: Pause handling prevents packet accumulation during debugging.
        /// </summary>
        private void Update()
        {
            ProcessPackets();
        }

        /// <summary>
        /// Process packets manually (for tests and Update()).
        /// </summary>
        public static void ProcessPackets()
        {
            // PAUSE HANDLING: Skip packet processing when paused
            // Prevents packet accumulation during Time.timeScale = 0 debugging sessions
            if (Time.timeScale == 0f)
            {
                return;
            }

            // Get current time for delivery checks
            float currentTime = GetCurrentTime();

            // Process InputChannel - iterate from end to start for safe removal
            for (int i = _inputList.Count - 1; i >= 0; i--)
            {
                if (_inputList[i].deliveryTime <= currentTime)
                {
                    // Packet ready for delivery
                    OnInputBatchReceived?.Invoke(_inputList[i].payload);

                    // Swap with last element and remove (O(1) removal)
                    int lastIndex = _inputList.Count - 1;
                    if (i != lastIndex)
                    {
                        _inputList[i] = _inputList[lastIndex];
                    }
                    _inputList.RemoveAt(lastIndex);
                }
            }

            // Process StateChannel - same pattern
            for (int i = _stateList.Count - 1; i >= 0; i--)
            {
                if (_stateList[i].deliveryTime <= currentTime)
                {
                    // Packet ready for delivery
                    OnStateReceived?.Invoke(_stateList[i].payload);

                    // Swap with last element and remove (O(1) removal)
                    int lastIndex = _stateList.Count - 1;
                    if (i != lastIndex)
                    {
                        _stateList[i] = _stateList[lastIndex];
                    }
                    _stateList.RemoveAt(lastIndex);
                }
            }
        }

        /// <summary>
        /// Clear all pending packets (for testing).
        /// </summary>
        public static void Clear()
        {
            _inputList.Clear();
            _stateList.Clear();
            
            // Clear event handlers to prevent cross-test contamination
            OnInputBatchReceived = null;
            OnStateReceived = null;
        }

        /// <summary>
        /// Get pending packet counts (for debugging/testing).
        /// </summary>
        public static int GetPendingInputCount() => _inputList.Count;
        public static int GetPendingStateCount() => _stateList.Count;
    }
}
