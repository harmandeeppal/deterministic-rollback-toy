using UnityEngine;
using DeterministicRollback.Entities;
using DeterministicRollback.Rendering;

namespace DeterministicRollback.DebugTools
{
    /// <summary>
    /// Visual debugging gizmos for inspecting simulation state.
    /// Draws 3 cubes: Red (server truth), Green (client prediction), Blue (render interpolation).
    /// Shows error vectors and state history trails.
    /// </summary>
    public class DebugCubes : MonoBehaviour
    {
        [Tooltip("Reference to ClientEntity (green cube) for prediction visualization")]
        public ClientEntity clientEntity;

        [Tooltip("Reference to ServerEntity (red cube) for server truth visualization")]
        public ServerEntity serverEntity;

        [Tooltip("Reference to InterpolatedEntity (blue cube) for render position")]
        public InterpolatedEntity interpolatedEntity;

        [Tooltip("Draw error vector when magnitude exceeds this threshold")]
        public float errorVectorThreshold = 0.01f;

        [Tooltip("Number of recent ticks to show in history trail")]
        public int historyTrailLength = 120; // 2 seconds at 60Hz

        [Tooltip("Cube size for gizmo visualization")]
        public float cubeSize = 0.5f;

        private bool _debugMode = true;

        /// <summary>
        /// Toggle gizmo rendering on/off.
        /// </summary>
        public void SetDebugMode(bool enabled)
        {
            _debugMode = enabled;
        }

        /// <summary>
        /// Get server cube (red) position for test assertions.
        /// </summary>
        public Vector3 GetServerCubePosition()
        {
            if (serverEntity == null) return Vector3.zero;
            return new Vector3(serverEntity.CurrentState.position.x, 0f, serverEntity.CurrentState.position.y);
        }

        /// <summary>
        /// Get client cube (green) position for test assertions.
        /// </summary>
        public Vector3 GetClientCubePosition()
        {
            if (clientEntity == null) return Vector3.zero;
            return new Vector3(clientEntity.GetState(clientEntity.CurrentTick).position.x, 0f,
                clientEntity.GetState(clientEntity.CurrentTick).position.y);
        }

        /// <summary>
        /// Get render cube (blue) position for test assertions.
        /// </summary>
        public Vector3 GetRenderCubePosition()
        {
            if (interpolatedEntity == null) return Vector3.zero;
            return interpolatedEntity.transform.position;
        }

        public void OnDrawGizmos()
        {
            if (!_debugMode) return;

            // Draw Red Cube (Server Truth)
            if (serverEntity != null)
            {
                DrawServerCube();
            }

            // Draw Green Cube (Client Prediction)
            if (clientEntity != null)
            {
                DrawClientCube();
            }

            // Draw Blue Cube (Render Interpolation)
            if (interpolatedEntity != null)
            {
                DrawRenderCube();
            }

            // Draw Error Vector (Red → Green line)
            if (serverEntity != null && clientEntity != null)
            {
                DrawErrorVector();
            }

            // Draw State History Trail
            if (clientEntity != null)
            {
                DrawStateHistoryTrail();
            }
        }

        private void DrawServerCube()
        {
            Vector3 pos = GetServerCubePosition();
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(pos, Vector3.one * cubeSize);
        }

        private void DrawClientCube()
        {
            Vector3 pos = GetClientCubePosition();
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(pos, Vector3.one * cubeSize);
        }

        private void DrawRenderCube()
        {
            Vector3 pos = GetRenderCubePosition();
            Gizmos.color = Color.blue;
            Gizmos.DrawWireCube(pos, Vector3.one * cubeSize);
        }

        private void DrawErrorVector()
        {
            Vector3 serverPos = GetServerCubePosition();
            Vector3 clientPos = GetClientCubePosition();
            Vector3 errorVec = clientPos - serverPos;
            float magnitude = errorVec.magnitude;

            if (magnitude > errorVectorThreshold)
            {
                Gizmos.color = new Color(1f, 0f, 0f, 0.7f); // Red with alpha
                Gizmos.DrawLine(serverPos, clientPos);
                
                // Draw arrowhead at client end
                Vector3 direction = errorVec.normalized;
                Vector3 arrowLeft = clientPos - direction * 0.1f + Vector3.Cross(direction, Vector3.up) * 0.1f;
                Vector3 arrowRight = clientPos - direction * 0.1f - Vector3.Cross(direction, Vector3.up) * 0.1f;
                Gizmos.DrawLine(clientPos, arrowLeft);
                Gizmos.DrawLine(clientPos, arrowRight);
            }
        }

        private void DrawStateHistoryTrail()
        {
            if (clientEntity.CurrentTick < 2) return;

            Gizmos.color = new Color(0f, 1f, 0f, 0.3f); // Semi-transparent green

            // Draw recent history
            int startTick = (int)Mathf.Max(1, clientEntity.CurrentTick - historyTrailLength);
            Vector3 prevPos = Vector3.zero;
            bool first = true;

            for (int tick = startTick; tick < clientEntity.CurrentTick; tick += 10) // Skip every 10th for performance
            {
                if (clientEntity.StateBuffer.Contains((uint)tick))
                {
                    var state = clientEntity.StateBuffer[(uint)tick];
                    Vector3 pos = new Vector3(state.position.x, 0f, state.position.y);

                    if (!first)
                    {
                        Gizmos.DrawLine(prevPos, pos);
                    }

                    prevPos = pos;
                    first = false;
                }
            }

            // Draw to current position
            if (!first)
            {
                Vector3 currentPos = GetClientCubePosition();
                Gizmos.DrawLine(prevPos, currentPos);
            }
        }
    }
}
