using System;
using UnityEngine;
using DeterministicRollback.Core;
using DeterministicRollback.Networking;

namespace DeterministicRollback.Entities
{
    /// <summary>
    /// Non-Mono-client simulation core. Designed to be testable in EditMode (constructed with `new`).
    /// Runs a fixed-step simulation at 60Hz using a time accumulator pattern.
    /// </summary>
    /// <summary>
    /// Testable client-side simulation core. Runs a fixed-step 60Hz simulation using an accumulator.
    /// Handles input capture, batch sending, deterministic integration and StateBuffer writes.
    /// </summary>
    public class ClientEntity
    {
        public const int MAX_TICKS_PER_FRAME = 10;
        public const float FIXED_DELTA_TIME = 1f / 60f;

        // Tick counter: number of ticks simulated so far. Starts at 0 (spawn state at tick 0).
        public uint CurrentTick { get; private set; }

        // Buffers (pre-allocated ring buffers)
        public RingBuffer<InputPayload> InputBuffer { get; private set; }
        public RingBuffer<StatePayload> StateBuffer { get; private set; }

        // Batch container reused every tick (value-type snapshot when sent)
        private InputBatch _batchContainer;

        private float _timer = 0f;
        public int ticksProcessed = 0;

        // Network parameters (configurable from wrapper or tests)
        public float latencyMs = 0f;
        public float lossChance = 0f; // 0.0 .. 1.0

        // Input provider (in tests or wrapper set to supply input vector): returns Vector2 for current tick
        public Func<Vector2> InputProvider = () => Vector2.zero;

        public ClientEntity()
        {
            Initialize();
        }

        /// <summary>
        /// Initialize buffers and spawn state. Safe to call multiple times to reset client state.
        /// </summary>
        public void Initialize()
        {
            InputBuffer = new RingBuffer<InputPayload>(4096);
            StateBuffer = new RingBuffer<StatePayload>(4096);

            var spawnState = new StatePayload
            {
                tick = 0,
                position = Vector2.zero,
                velocity = Vector2.zero,
                confirmedInputTick = 0
            };

            StateBuffer[0] = spawnState;
            CurrentTick = 0; // nothing simulated yet; tick 0 is spawn
            _batchContainer = new InputBatch();
            _timer = 0f;
            ticksProcessed = 0;
        }

        /// <summary>
        /// Main update to be called every frame. Accumulates Time.deltaTime and simulates fixed ticks.
        /// Designed to be called from both tests (EditMode) and a MonoBehaviour wrapper in PlayMode.
        /// </summary>
        /// <summary>
        /// Advance simulation by consuming accumulated time (call every frame).
        /// Runs up to <see cref="MAX_TICKS_PER_FRAME"/> ticks to prevent spiral-of-death.
        /// This method is a hot path and must not allocate GC memory per tick.
        /// </summary>
        /// <summary>
        /// Advance simulation by consuming accumulated time (call every frame).
        /// Runs up to <see cref="MAX_TICKS_PER_FRAME"/> ticks to prevent spiral-of-death.
        /// This method is a hot path and must not allocate GC memory per tick.
        /// </summary>
        public void Update()
        {
            UpdateWithDelta(Time.deltaTime);
        }

        /// <summary>
        /// Advance simulation using an explicit deltaTime. Useful for deterministic EditMode tests.
        /// </summary>
        public void UpdateWithDelta(float deltaTime)
        {
            _timer += deltaTime;

            // Compute available whole ticks deterministically using floor on double to avoid
            // round-off errors that can drop one tick in edge cases. Use a small absolute
            // epsilon large enough to cover float division underflow without affecting
            // normal fractional behavior.
            const double EPS = 1e-6; // 1 microsecond
            int availableTicks = (int)Math.Floor(((double)_timer + EPS) / FIXED_DELTA_TIME);
            int toProcess = Math.Min(availableTicks, MAX_TICKS_PER_FRAME);

            ticksProcessed = 0;

            for (int i = 0; i < toProcess; i++)
            {
                uint nextTick = CurrentTick + 1; // tick to simulate

                // Capture input
                var input = new InputPayload
                {
                    tick = nextTick,
                    inputVector = InputProvider()
                };

                // Always write input for this tick (even if zero)
                InputBuffer[nextTick] = input;

                // Build redundant batch (1-3 inputs)
                int batchSize = (int)Math.Min(3, (float)nextTick);
                _batchContainer.count = batchSize;
                if (batchSize >= 1) _batchContainer.i0 = InputBuffer[nextTick];
                if (batchSize >= 2) _batchContainer.i1 = InputBuffer[nextTick - 1];
                if (batchSize >= 3) _batchContainer.i2 = InputBuffer[nextTick - 2];

                // Send batch (copied by value into list - zero allocations here)
                FakeNetworkPipe.SendInput(_batchContainer, latencyMs, lossChance);

                // Retrieve previous state (tick 0 for first simulation)
                StatePayload prev = StateBuffer[nextTick - 1];

                // Integrate - deterministic, pure function
                SimulationMath.Integrate(ref prev, ref input);

                // Store post-integration state
                StateBuffer[nextTick] = prev;

                // Advance
                CurrentTick = nextTick;
                ticksProcessed++;
            }

            // Subtract the total processed time in one operation to avoid cumulative float
            // subtract-rounding artifacts and preserve residual accumulator precisely.
            _timer -= toProcess * FIXED_DELTA_TIME;

            // If residual is extremely close to zero (due to float rounding), snap to zero so
            // the next floor calculation does not undercount by one tick.
            if (_timer < (float)EPS) _timer = 0f;

            if (availableTicks > MAX_TICKS_PER_FRAME)
            {
                Debug.LogWarning($"Client simulation spiral: clamped to {MAX_TICKS_PER_FRAME} ticks. Backlog remaining: {_timer:F3}s");
            }
        }

        /// <summary>
        /// Helper to access a stored state for assertions in tests.
        /// Returns default StatePayload if not present.
        /// </summary>
        /// <summary>
        /// Retrieve recorded post-integration StatePayload for a given tick.
        /// Returns default(StatePayload) if history does not contain that tick.
        /// </summary>
        public StatePayload GetState(uint tick)
        {
            return StateBuffer.Contains(tick) ? StateBuffer[tick] : default;
        }

        // Handshake support (Phase 5)
        public uint lastServerConfirmedInputTick { get; private set; } = 0;
        public const uint INPUT_BUFFER_HEADROOM = 2;

        /// <summary>
        /// Handle a WelcomePacket from the server and synchronize the client's timeline.
        /// oneWayMs: configured one-way latency in milliseconds (UI slider value).
        /// </summary>
        public void HandleWelcomePacket(WelcomePacket welcome, float oneWayMs)
        {
            // RTT ticks calculation: ceil( (oneWayMs * 2) / tickMs ), minimum 3 ticks.
            // Use double precision and subtract a tiny epsilon before Ceiling to avoid
            // floating-point rounding causing exact multiples to round up.
            double rttMs = oneWayMs * 2.0;
            double tickMs = 1000.0 / 60.0;
            uint rttTicks = (uint)Math.Ceiling(rttMs / tickMs - 1e-9);
            if (rttTicks < 3u) rttTicks = 3u;

            uint clientStartTick = welcome.startTick + rttTicks + INPUT_BUFFER_HEADROOM;

            // Warp server state into client's timeline at clientStartTick - 1
            var warped = welcome.startState;
            warped.tick = clientStartTick - 1;
            StateBuffer[clientStartTick - 1] = warped;

            // Sync confirmed input tick
            lastServerConfirmedInputTick = welcome.startState.confirmedInputTick;

            // Set current tick to the clientStartTick (last simulated tick)
            CurrentTick = clientStartTick;
        }
    }
}