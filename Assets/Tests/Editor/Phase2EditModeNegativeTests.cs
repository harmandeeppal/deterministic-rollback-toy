using NUnit.Framework;
using UnityEngine;
using DeterministicRollback.Core;

namespace DeterministicRollback.Tests.Editor
{
    /// <summary>
    /// Phase 2 negative/boundary tests for deterministic simulation math.
    /// </summary>
    public class Phase2EditModeNegativeTests
    {
        [Test]
        public void SimulationMath_Integrate_ExtremeInput_DoesNotProduceNaN()
        {
            var state = new StatePayload { position = Vector2.zero, velocity = Vector2.zero, tick = 0, confirmedInputTick = 0 };
            var input = new InputPayload { tick = 1, inputVector = new Vector2(1e30f, -1e30f) };

            SimulationMath.Integrate(ref state, ref input);

            // Ensure resulting components are finite (not NaN or Infinity)
            Assert.IsFalse(float.IsNaN(state.position.x) || float.IsInfinity(state.position.x));
            Assert.IsFalse(float.IsNaN(state.position.y) || float.IsInfinity(state.position.y));
            Assert.IsFalse(float.IsNaN(state.velocity.x) || float.IsInfinity(state.velocity.x));
            Assert.IsFalse(float.IsNaN(state.velocity.y) || float.IsInfinity(state.velocity.y));
        }

        [Test]
        public void SimulationMath_Integrate_PreservesConfirmedInputTick()
        {
            var state = new StatePayload { position = Vector2.zero, velocity = Vector2.zero, tick = 0, confirmedInputTick = 123 };
            var input = new InputPayload { tick = 10, inputVector = Vector2.right };

            SimulationMath.Integrate(ref state, ref input);

            // confirmedInputTick should remain unchanged by pure Integrate
            Assert.AreEqual(123u, state.confirmedInputTick);
        }
    }
}