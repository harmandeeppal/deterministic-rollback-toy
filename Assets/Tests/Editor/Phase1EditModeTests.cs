using NUnit.Framework;
using UnityEngine;
using DeterministicRollback.Core;

namespace DeterministicRollback.Tests.Editor
{
    /// <summary>
    /// Phase 1 tests - Pure unit tests for core data structures and deterministic physics.
    /// All tests are Edit Mode compatible (no scene/MonoBehaviour dependencies).
    /// </summary>
    public class Phase1EditModeTests
    {
        // ========== Step 1.2: RingBuffer Tests ==========

        [Test]
        public void RingBuffer_GhostDataProtection()
        {
            var buffer = new RingBuffer<int>(100);
            buffer[0] = 42;
            Assert.IsTrue(buffer.Contains(0));
            
            buffer[100] = 99; // Overwrites index 0 (100 % 100 = 0)
            Assert.IsFalse(buffer.Contains(0)); // Slot.tick != 0
            
            Assert.Throws<System.Exception>(() => { var _ = buffer[0]; });
        }

        [Test]
        public void RingBuffer_WriteValidation()
        {
            var buffer = new RingBuffer<int>(100);
            buffer[150] = 5; // 150 % 100 = 50
            Assert.IsTrue(buffer.Contains(150)); // Slot.tick == 150
            Assert.IsFalse(buffer.Contains(50)); // Slot.tick != 50
        }

        [Test]
        public void RingBuffer_ClearRange()
        {
            var buffer = new RingBuffer<int>(100);
            
            // Write some data
            for (uint i = 10; i <= 20; i++)
            {
                buffer[i] = (int)i * 10;
            }
            
            // Clear range
            buffer.ClearRange(12, 18);
            
            // Verify cleared ticks are invalid
            Assert.IsTrue(buffer.Contains(10));
            Assert.IsTrue(buffer.Contains(11));
            Assert.IsFalse(buffer.Contains(12));
            Assert.IsFalse(buffer.Contains(15));
            Assert.IsFalse(buffer.Contains(18));
            Assert.IsTrue(buffer.Contains(19));
            Assert.IsTrue(buffer.Contains(20));
        }

        // ========== Step 1.3: Data Structures Tests ==========

        [Test]
        public void DataStructures_Instantiate()
        {
            var input = new InputPayload { tick = 1, inputVector = Vector2.right };
            var state = new StatePayload { tick = 1, position = Vector2.zero, velocity = Vector2.one, confirmedInputTick = 0 };
            var welcome = new WelcomePacket { startTick = 100, startState = state };
            
            Assert.AreEqual(1u, input.tick);
            Assert.AreEqual(Vector2.right, input.inputVector);
            Assert.AreEqual(100u, welcome.startTick);
            Assert.AreEqual(1u, welcome.startState.tick);
        }

        [Test]
        public void DataStructures_UnmanagedTypes()
        {
            // Verify structs are unmanaged (no managed references)
            // This test will fail to compile if structs contain managed types
            unsafe
            {
                var input = new InputPayload();
                var state = new StatePayload();
                var welcome = new WelcomePacket();
                
                // If these compile, structs are unmanaged
                var inputPtr = &input;
                var statePtr = &state;
                var welcomePtr = &welcome;
                
                Assert.IsTrue(inputPtr != null);
                Assert.IsTrue(statePtr != null);
                Assert.IsTrue(welcomePtr != null);
            }
        }

        // ========== Step 1.4: Deterministic Integrate Tests ==========

        [Test]
        public void Integrate_IsDeterministic()
        {
            var state1 = new StatePayload { position = Vector2.zero, velocity = Vector2.zero, tick = 1 };
            var state2 = new StatePayload { position = Vector2.zero, velocity = Vector2.zero, tick = 1 };
            var input = new InputPayload { tick = 1, inputVector = Vector2.right };
            
            SimulationMath.Integrate(ref state1, ref input);
            SimulationMath.Integrate(ref state2, ref input);
            
            Assert.AreEqual(state1.position.x, state2.position.x, 0.0001f);
            Assert.AreEqual(state1.position.y, state2.position.y, 0.0001f);
            Assert.AreEqual(state1.velocity.x, state2.velocity.x, 0.0001f);
            Assert.AreEqual(state1.velocity.y, state2.velocity.y, 0.0001f);
            Assert.AreEqual(state1.tick, state2.tick);
        }

        [Test]
        public void Integrate_MultipleFrames()
        {
            var state = new StatePayload { position = Vector2.zero, velocity = Vector2.zero, tick = 0 };
            
            // Simulate 60 ticks at 60Hz (1 second) moving right
            for (uint i = 1; i <= 60; i++)
            {
                var input = new InputPayload { tick = i, inputVector = Vector2.right };
                SimulationMath.Integrate(ref state, ref input);
            }
            
            // After 1 second with force=10, should have moved right
            // velocity += inputVector * 10 * dt => velocity += (1,0) * 10 * (1/60) = (0.1667, 0) per tick
            // After 60 ticks: velocity = 10 m/s
            // position += velocity * dt integrated over 60 ticks
            Assert.Greater(state.position.x, 2f, "Should have moved at least 2 units right");
            Assert.Less(state.position.x, 6f, "Should not have moved more than 6 units right");
            Assert.AreEqual(0f, state.position.y, 0.0001f, "Should not have moved vertically");
            Assert.AreEqual(60u, state.tick, "Tick should match last input tick");
        }

        [Test]
        public void Integrate_ZeroInput()
        {
            var state = new StatePayload { position = Vector2.zero, velocity = Vector2.one, tick = 0 };
            var input = new InputPayload { tick = 1, inputVector = Vector2.zero };
            
            SimulationMath.Integrate(ref state, ref input);
            
            // With zero input, velocity should decay/stay same, position should still update
            Assert.Greater(state.position.x, 0f, "Position should update based on existing velocity");
            Assert.AreEqual(1u, state.tick);
        }

        [Test]
        public void Integrate_TickUpdate()
        {
            var state = new StatePayload { position = Vector2.zero, velocity = Vector2.zero, tick = 5 };
            var input = new InputPayload { tick = 10, inputVector = Vector2.zero };
            
            SimulationMath.Integrate(ref state, ref input);
            
            // State tick should match input tick after integration
            Assert.AreEqual(10u, state.tick, "State tick should match input tick");
        }

        [Test]
        public void RingBuffer_WithStatePayload()
        {
            var buffer = new RingBuffer<StatePayload>(4096);
            
            var spawnState = new StatePayload
            {
                tick = 0,
                position = Vector2.zero,
                velocity = Vector2.zero,
                confirmedInputTick = 0
            };
            
            buffer[0] = spawnState;
            
            Assert.IsTrue(buffer.Contains(0));
            var retrieved = buffer[0];
            Assert.AreEqual(0u, retrieved.tick);
            Assert.AreEqual(Vector2.zero, retrieved.position);
        }

        [Test]
        public void RingBuffer_WithInputPayload()
        {
            var buffer = new RingBuffer<InputPayload>(4096);
            
            var input = new InputPayload
            {
                tick = 42,
                inputVector = new Vector2(0.5f, 0.3f)
            };
            
            buffer[42] = input;
            
            Assert.IsTrue(buffer.Contains(42));
            var retrieved = buffer[42];
            Assert.AreEqual(42u, retrieved.tick);
            Assert.AreEqual(0.5f, retrieved.inputVector.x, 0.0001f);
            Assert.AreEqual(0.3f, retrieved.inputVector.y, 0.0001f);
        }
    }
}
