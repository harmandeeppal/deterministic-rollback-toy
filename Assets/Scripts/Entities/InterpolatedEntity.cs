using UnityEngine;
using DeterministicRollback.Entities;
using DeterministicRollback.Behaviours;

namespace DeterministicRollback.Rendering
{
    /// <summary>
    /// Render interpolation MonoBehaviour (Blue Cube).
    /// Interpolates between two completed states using the client's accumulator timer.
    /// Designed to be zero-GC in LateUpdate (no allocations).
    /// </summary>
    public class InterpolatedEntity : MonoBehaviour
    {
        [Tooltip("Reference to the ClientEntityBehaviour that owns the simulation client.")]
        public ClientEntityBehaviour clientBehaviour;

        // Allows tests to inject a ClientEntity directly (non-serialized)
        private ClientEntity _testClient = null;

        // One-time warning flag for buffer misses
        private bool _loggedBufferMiss = false;

        /// <summary>
        /// If false, interpolation fallback will NOT apply velocity-based extrapolation on buffer misses.
        /// Default true to preserve existing behaviour; tests may toggle this for negative-case verification.
        /// </summary>
        public bool allowExtrapolation = true;

        /// <summary>
        /// For tests: inject a ClientEntity directly.
        /// </summary>
        public void DebugSetClient(ClientEntity client)
        {
            _testClient = client;
        }

        /// <summary>

        /// <summary>
        /// Called by UI Toggle or tests to change the extrapolation behaviour.
        /// </summary>
        /// <param name="enabled">True to allow velocity-based extrapolation on buffer misses.</param>
        public void OnExtrapolationToggleChanged(bool enabled)
        {
            allowExtrapolation = enabled;
        }

        private ClientEntity Client => _testClient ?? clientBehaviour?.Client;

        // Made public for deterministic unit testing (call directly from tests)
        public void LateUpdate()
        {
            var client = Client;
            if (client == null) return;

            if (client.CurrentTick <= 1)
            {
                // Need at least two completed ticks to interpolate
                return;
            }

            uint renderTick = client.CurrentTick - 1; // last fully completed tick

            // Buffer guard: require both ticks to exist
            if (!client.StateBuffer.Contains(renderTick) || !client.StateBuffer.Contains(renderTick - 1))
            {
                // Fallback behaviour: when extrapolation is allowed, apply velocity-based extrapolation
                if (allowExtrapolation)
                {
                    // Use available tick (renderTick) plus velocity extrapolation if present
                    if (client.StateBuffer.Contains(renderTick))
                    {
                        var s = client.StateBuffer[renderTick];
                        Vector2 fallback = s.position + s.velocity * client.Timer;
                        transform.position = new Vector3(fallback.x, 0f, fallback.y);
                    }
                    else if (client.StateBuffer.Contains(renderTick - 1))
                    {
                        var s = client.StateBuffer[renderTick - 1];
                        Vector2 fallback = s.position + s.velocity * (client.Timer + ClientEntity.FIXED_DELTA_TIME);
                        transform.position = new Vector3(fallback.x, 0f, fallback.y);
                    }
                }
                else
                {
                    // Extrapolation disabled: snap to available state's position without applying velocity
                    if (client.StateBuffer.Contains(renderTick))
                    {
                        var s = client.StateBuffer[renderTick];
                        transform.position = new Vector3(s.position.x, 0f, s.position.y);
                    }
                    else if (client.StateBuffer.Contains(renderTick - 1))
                    {
                        var s = client.StateBuffer[renderTick - 1];
                        transform.position = new Vector3(s.position.x, 0f, s.position.y);
                    }
                }

                if (!_loggedBufferMiss)
                {
                    Debug.LogWarning($"Interpolation buffer miss at tick {renderTick}. Using extrapolation fallback.");
                    _loggedBufferMiss = true;
                }
                return;
            }

            var prev = client.StateBuffer[renderTick - 1];
            var curr = client.StateBuffer[renderTick];

            float alpha = Mathf.Clamp01(client.Timer / ClientEntity.FIXED_DELTA_TIME);

            Vector2 renderPos = Vector2.Lerp(prev.position, curr.position, alpha);
            transform.position = new Vector3(renderPos.x, 0f, renderPos.y);
        }
    }
}
