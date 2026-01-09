using System;
using UnityEngine;
using DeterministicRollback.Core;
using DeterministicRollback.Networking;

namespace DeterministicRollback.Entities
{
    /// <summary>
    /// Authoritative server simulation core. Runs a fixed-step simulation at 60Hz using an accumulator.
    /// Testable in EditMode by calling UpdateWithDelta(float).
    /// </summary>
    public class ServerEntity : MonoBehaviour
    {
        public const int MAX_TICKS_PER_FRAME = 10;
        public const float FIXED_DELTA_TIME = 1f / 60f;

        public uint ServerTick { get; private set; }

        public RingBuffer<InputPayload> ServerInputBuffer { get; private set; }
        public StatePayload CurrentState { get; private set; }

        private float _timer = 0f;
        public int ticksProcessed = 0;

        public uint lastConfirmedInputTick = 0;

        // Network parameters (configurable from wrapper or tests)
        public float latencyMs = 0f;
        public float lossChance = 0f; // 0.0 .. 1.0

        void Awake()
        {
            Initialize();
        }

        public void Initialize()
        {
            ServerInputBuffer = new RingBuffer<InputPayload>(4096);
            CurrentState = new StatePayload { tick = 0, position = Vector2.zero, velocity = Vector2.zero, confirmedInputTick = 0 };
            ServerTick = 1;
            lastConfirmedInputTick = 0;

            // Ensure subscription is active for tests that call ProcessPackets immediately
            FakeNetworkPipe.OnInputBatchReceived -= OnInputBatchReceived;
            FakeNetworkPipe.OnInputBatchReceived += OnInputBatchReceived;
        }

        void OnEnable()
        {
            FakeNetworkPipe.OnInputBatchReceived += OnInputBatchReceived;
        }

        void OnDisable()
        {
            FakeNetworkPipe.OnInputBatchReceived -= OnInputBatchReceived;
        }

        private void OnInputBatchReceived(InputBatch batch)
        {
            for (int i = 0; i < batch.count; i++)
            {
                var input = batch.Get(i);
                // Validate tick range and avoid overwriting newer data
                if (input.tick >= ServerTick && input.tick < ServerTick + (uint)ServerInputBuffer.Capacity)
                {
                    if (!ServerInputBuffer.Contains(input.tick))
                    {
                        ServerInputBuffer[input.tick] = input;
                    }
                    lastConfirmedInputTick = Math.Max(lastConfirmedInputTick, input.tick);
                }
            }
        }

        void Update()
        {
            UpdateWithDelta(Time.deltaTime);
        }

        public void UpdateWithDelta(float deltaTime)
        {
            _timer += deltaTime;

            const double EPS = 1e-6;
            int availableTicks = (int)Math.Floor(((double)_timer + EPS) / FIXED_DELTA_TIME);
            int toProcess = Math.Min(availableTicks, MAX_TICKS_PER_FRAME);

            ticksProcessed = 0;

            for (int i = 0; i < toProcess; i++)
            {
                // Retrieve input for this tick or predict
                InputPayload input;
                if (ServerInputBuffer.Contains(ServerTick))
                {
                    input = ServerInputBuffer[ServerTick];
                }
                else if (ServerInputBuffer.Contains(ServerTick - 1))
                {
                    input = ServerInputBuffer[ServerTick - 1]; // repeat last
                    input.tick = ServerTick; // ensure tick matches current simulation
                }
                else
                {
                    input = new InputPayload { tick = ServerTick, inputVector = Vector2.zero };
                }

                // Integrate
                var prev = CurrentState;
                SimulationMath.Integrate(ref prev, ref input);

                // Update current state and piggyback confirmation
                prev.confirmedInputTick = lastConfirmedInputTick;
                CurrentState = prev;

                // Send state (unreliable)
                FakeNetworkPipe.SendState(CurrentState, latencyMs, lossChance);

                // Advance
                ServerTick++;
                ticksProcessed++;
            }

            // Subtract processed time
            _timer -= toProcess * FIXED_DELTA_TIME;

            if (ticksProcessed >= MAX_TICKS_PER_FRAME)
            {
                Debug.LogWarning($"Server simulation spiral: clamped to {MAX_TICKS_PER_FRAME} ticks. Backlog remaining: {_timer:F3}s");
            }
        }

        public WelcomePacket GenerateWelcomePacket()
        {
            return new WelcomePacket { startTick = ServerTick, startState = CurrentState };
        }

        // Helper for tests to inspect internal timer (not part of public API)
        public float GetTimer() => _timer;
    }
}