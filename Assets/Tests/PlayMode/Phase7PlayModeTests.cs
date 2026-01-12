using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using DeterministicRollback.Entities;
using DeterministicRollback.Rendering;
using DeterministicRollback.Core;

namespace DeterministicRollback.Tests.PlayMode
{
    public class Phase7PlayModeTests
    {
        [UnityTest]
        public IEnumerator Interpolation_PlayMode_UI_Toggle_Extrapolation()
        {
            var client = new ClientEntity();
            client.Initialize();

            // Only have renderTick state
            var s0 = new DeterministicRollback.Core.StatePayload { tick = 2u, position = new Vector2(0f, 0f), velocity = new Vector2(1f, 0f), confirmedInputTick = 0 };
            client.StateBuffer[2] = s0;
            client.DebugSetCurrentTick(3);
            client.DebugSetTimer(ClientEntity.FIXED_DELTA_TIME * 0.5f);

            // Setup InterpolatedEntity on a GameObject
            var go = new GameObject("PlayInterp");
            var interp = go.AddComponent<InterpolatedEntity>();
            interp.DebugSetClient(client);

            // With extrapolation enabled, LateUpdate should move the object forward
            interp.OnExtrapolationToggleChanged(true);
            interp.LateUpdate();
            float withExtrap = interp.transform.position.x;
            Assert.Greater(withExtrap, 0f, "Position should move when extrapolation is enabled");

            // Now disable via the public method and verify it snaps to the available state (no velocity applied)
            interp.OnExtrapolationToggleChanged(false);
            interp.transform.position = Vector3.zero;
            client.DebugSetTimer(ClientEntity.FIXED_DELTA_TIME * 0.5f);
            interp.LateUpdate();

            Assert.AreEqual(0f, interp.transform.position.x, 0.0001f, "Position should snap to available state when extrapolation is disabled");

            UnityEngine.Object.DestroyImmediate(go);

            yield return null;
        }
    }
}