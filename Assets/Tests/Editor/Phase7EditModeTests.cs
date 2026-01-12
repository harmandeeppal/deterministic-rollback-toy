using NUnit.Framework;
using UnityEngine;
using DeterministicRollback.Entities;
using DeterministicRollback.Rendering;
using DeterministicRollback.Core;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Logging;
using System;

namespace DeterministicRollback.Tests.Editor
{
    /// <summary>
    /// Phase 7 tests - Core interpolation functionality.
    /// Tests smooth interpolation, alpha bounds, and zero-GC validation.
    /// </summary>
    public class Phase7EditModeTests
    {
        [Test]
        public void Interpolation_Smooth()
        {
            var client = new ClientEntity();
            client.Initialize();

            // Write two sequential states
            var s0 = new StatePayload { tick = 0u, position = new Vector2(0f, 0f), velocity = Vector2.zero, confirmedInputTick = 0 };
            var s1 = new StatePayload { tick = 1u, position = new Vector2(10f, 0f), velocity = Vector2.zero, confirmedInputTick = 0 };

            client.StateBuffer[0] = s0;
            client.StateBuffer[1] = s1;
            client.DebugSetCurrentTick(2); // next to simulate

            // Set timer to half tick
            client.DebugSetTimer(ClientEntity.FIXED_DELTA_TIME * 0.5f);

            var go = new GameObject("Interp");
            var interp = go.AddComponent<InterpolatedEntity>();
            interp.DebugSetClient(client);

            interp.LateUpdate();

            Assert.AreEqual(5f, interp.transform.position.x, 0.001f);

            UnityEngine.Object.DestroyImmediate(go);
        }

        [Test]
        public void Interpolation_AlphaBounds()
        {
            var client = new ClientEntity();
            client.Initialize();

            var s0 = new StatePayload { tick = 0u, position = new Vector2(0f, 0f), velocity = Vector2.zero, confirmedInputTick = 0 };
            var s1 = new StatePayload { tick = 1u, position = new Vector2(10f, 0f), velocity = Vector2.zero, confirmedInputTick = 0 };

            client.StateBuffer[0] = s0;
            client.StateBuffer[1] = s1;
            client.DebugSetCurrentTick(2);

            var go = new GameObject("InterpAlpha");
            var interp = go.AddComponent<InterpolatedEntity>();
            interp.DebugSetClient(client);

            // alpha = 0
            client.DebugSetTimer(0f);
            interp.LateUpdate();
            Assert.AreEqual(0f, interp.transform.position.x, 0.001f);

            // alpha = 1
            client.DebugSetTimer(ClientEntity.FIXED_DELTA_TIME);
            interp.LateUpdate();
            Assert.AreEqual(10f, interp.transform.position.x, 0.001f);

            UnityEngine.Object.DestroyImmediate(go);
        }

        [Test]
        public void Interpolation_ZeroGCLateUpdate()
        {
            var client = new ClientEntity();
            client.Initialize();

            var s0 = new StatePayload { tick = 0u, position = new Vector2(0f, 0f), velocity = Vector2.zero, confirmedInputTick = 0 };
            var s1 = new StatePayload { tick = 1u, position = new Vector2(10f, 0f), velocity = Vector2.zero, confirmedInputTick = 0 };

            client.StateBuffer[0] = s0;
            client.StateBuffer[1] = s1;
            client.DebugSetCurrentTick(2);
            client.DebugSetTimer(ClientEntity.FIXED_DELTA_TIME * 0.5f);

            var go = new GameObject("InterpGC");
            var interp = go.AddComponent<InterpolatedEntity>();
            interp.DebugSetClient(client);

            // Warm up
            for (int i = 0; i < 10; i++) interp.LateUpdate();

            GC.Collect();
            long before = GC.GetTotalMemory(true);

            for (int i = 0; i < 1000; i++) interp.LateUpdate();

            long after = GC.GetTotalMemory(true);

            // Allow small jitter (1KB) but expect no substantial allocations in LateUpdate
            Assert.LessOrEqual(after - before, 1024);

            UnityEngine.Object.DestroyImmediate(go);
        }
    }
}
