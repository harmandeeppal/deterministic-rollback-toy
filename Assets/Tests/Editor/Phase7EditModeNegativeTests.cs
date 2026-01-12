using NUnit.Framework;
using UnityEngine;
using DeterministicRollback.Entities;
using DeterministicRollback.Rendering;
using DeterministicRollback.Core;
using UnityEngine.TestTools;

namespace DeterministicRollback.Tests.Editor
{
    /// <summary>
    /// Phase 7 negative tests - Edge cases and boundary conditions for render interpolation.
    /// Tests buffer misses, wraparound, extrapolation toggle, and hard-snap scenarios.
    /// </summary>
    public class Phase7EditModeNegativeTests
    {
        [Test]
        public void Interpolation_BufferMissFallback()
        {
            var client = new ClientEntity();
            client.Initialize();

            // Only have renderTick-1 state
            var s0 = new StatePayload { tick = 0u, position = new Vector2(2f, 0f), velocity = new Vector2(1f, 0f), confirmedInputTick = 0 };
            client.StateBuffer[0] = s0;
            client.DebugSetCurrentTick(2); // renderTick = 1

            client.DebugSetTimer(ClientEntity.FIXED_DELTA_TIME * 0.5f);

            var go = new GameObject("InterpMiss");
            var interp = go.AddComponent<InterpolatedEntity>();
            interp.DebugSetClient(client);

            // Expect the one-time warning about buffer miss
            LogAssert.Expect(UnityEngine.LogType.Warning, "Interpolation buffer miss at tick 1. Using extrapolation fallback.");
            interp.LateUpdate();

            // Fallback computes s0.position + s0.velocity * (timer + FIXED_DELTA_TIME)
            float expectedX = 2f + 1f * (ClientEntity.FIXED_DELTA_TIME * 1.5f);
            Assert.AreEqual(expectedX, interp.transform.position.x, 0.0001f);

            UnityEngine.Object.DestroyImmediate(go);
        }

        [Test]
        public void Interpolation_EarlyReturn()
        {
            var client = new ClientEntity();
            client.Initialize();

            client.DebugSetCurrentTick(1); // not enough history

            var go = new GameObject("InterpEarly");
            var interp = go.AddComponent<InterpolatedEntity>();
            interp.DebugSetClient(client);

            // Should not throw
            interp.LateUpdate();

            UnityEngine.Object.DestroyImmediate(go);
        }

        [Test]
        public void Interpolation_WraparoundBufferIndices()
        {
            var client = new ClientEntity();
            client.Initialize();

            uint a = ClientEntity.BUFFER_SIZE - 1u;
            uint b = a + 1u; // wraps to buffer boundary

            var s0 = new StatePayload { tick = a, position = new Vector2(0f, 0f), velocity = Vector2.zero, confirmedInputTick = 0 };
            var s1 = new StatePayload { tick = b, position = new Vector2(20f, 0f), velocity = Vector2.zero, confirmedInputTick = 0 };

            client.StateBuffer[a] = s0;
            client.StateBuffer[b] = s1;

            client.DebugSetCurrentTick(b + 1u);
            client.DebugSetTimer(ClientEntity.FIXED_DELTA_TIME * 0.5f);

            var go = new GameObject("InterpWrap");
            var interp = go.AddComponent<InterpolatedEntity>();
            interp.DebugSetClient(client);

            interp.LateUpdate();

            // Expect midpoint (10)
            Assert.AreEqual(10f, interp.transform.position.x, 0.001f);

            UnityEngine.Object.DestroyImmediate(go);
        }

        [Test]
        public void Interpolation_ExtrapolationToggle()
        {
            var client = new ClientEntity();
            client.Initialize();

            var s0 = new StatePayload { tick = 2u, position = new Vector2(0f, 0f), velocity = new Vector2(1f, 0f), confirmedInputTick = 0 };
            client.StateBuffer[2] = s0;
            client.DebugSetCurrentTick(3); // renderTick = 2

            client.DebugSetTimer(ClientEntity.FIXED_DELTA_TIME * 0.5f);

            var go = new GameObject("InterpExtrap");
            var interp = go.AddComponent<InterpolatedEntity>();
            interp.DebugSetClient(client);

            // Default: extrapolation allowed
            interp.allowExtrapolation = true;
            interp.LateUpdate();
            float movedX = interp.transform.position.x;
            Assert.Greater(movedX, 0f);

            // Disable extrapolation: should snap to available state's position
            interp.transform.position = Vector3.zero;
            interp.allowExtrapolation = false;
            client.DebugSetTimer(ClientEntity.FIXED_DELTA_TIME * 0.5f);
            interp.LateUpdate();
            Assert.AreEqual(0f, interp.transform.position.x, 0.0001f);

            UnityEngine.Object.DestroyImmediate(go);
        }

        [Test]
        public void Interpolation_HardSnapImmediate()
        {
            var client = new ClientEntity();
            client.Initialize();

            // Simulate some progressed tick
            client.DebugSetCurrentTick(10);

            // Create authoritative server state for an older tick with a distinct position
            var serverState = new StatePayload { tick = 2u, position = new Vector2(99f, 0f), velocity = Vector2.zero, confirmedInputTick = 0 };

            // Inject - will trigger missing-history hard-snap and set CurrentTick = serverState.tick + 1
            client.DebugInjectServerState(serverState);

            Assert.AreEqual(3u, client.CurrentTick);

            client.DebugSetTimer(0f);

            var go = new GameObject("InterpSnap");
            var interp = go.AddComponent<InterpolatedEntity>();
            interp.DebugSetClient(client);

            interp.LateUpdate();

            // Expect immediate authoritative position
            Assert.AreEqual(99f, interp.transform.position.x, 0.0001f);

            UnityEngine.Object.DestroyImmediate(go);
        }
    }
}
