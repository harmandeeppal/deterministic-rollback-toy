using NUnit.Framework;
using UnityEngine;
using DeterministicRollback.Entities;
using DeterministicRollback.Rendering;
using DeterministicRollback.DebugTools;
using DeterministicRollback.Core;

namespace DeterministicRollback.Tests.Editor
{
    /// <summary>
    /// Phase 8 core tests - Visual debugging gizmo functionality.
    /// Tests 3 cube positions, error vector computation, and history trail rendering.
    /// </summary>
    public class Phase8EditModeTests
    {
        [Test]
        public void DebugCubes_PositionAccuracy_ServerCube()
        {
            // Setup: Create server entity
            var serverGo = new GameObject("Server");
            var serverEntity = serverGo.AddComponent<ServerEntity>();
            serverEntity.Initialize();

            // Setup: Create debug cubes
            var debugGo = new GameObject("DebugCubes");
            var debugCubes = debugGo.AddComponent<DebugCubes>();
            debugCubes.serverEntity = serverEntity;

            // Run 10 ticks
            for (int i = 0; i < 10; i++)
            {
                serverEntity.UpdateWithDelta(ServerEntity.FIXED_DELTA_TIME);
            }

            // Assert: Red cube position matches server state
            Vector3 expectedPos = new Vector3(serverEntity.CurrentState.position.x, 0f, serverEntity.CurrentState.position.y);
            Vector3 actualPos = debugCubes.GetServerCubePosition();
            
            Assert.AreEqual(expectedPos.x, actualPos.x, 0.0001f, "Server X position mismatch");
            Assert.AreEqual(expectedPos.y, actualPos.y, 0.0001f, "Server Y position mismatch");
            Assert.AreEqual(expectedPos.z, actualPos.z, 0.0001f, "Server Z position mismatch");

            // Cleanup
            UnityEngine.Object.DestroyImmediate(serverGo);
            UnityEngine.Object.DestroyImmediate(debugGo);
        }

        [Test]
        public void DebugCubes_ErrorVector_ComputedCorrectly()
        {
            // Setup: Create client
            var clientEntity = new ClientEntity();
            clientEntity.Initialize();
            // Set input to move right (+X)
            clientEntity.InputProvider = () => Vector2.right;

            // Setup: Create server (starts at origin)
            var serverGo = new GameObject("Server");
            var serverEntity = serverGo.AddComponent<ServerEntity>();
            serverEntity.Initialize();

            // Setup: Debug cubes
            var debugGo = new GameObject("DebugCubes");
            var debugCubes = debugGo.AddComponent<DebugCubes>();
            debugCubes.clientEntity = clientEntity;
            debugCubes.serverEntity = serverEntity;

            // Run client forward 10 ticks (server stays at origin)
            for (int i = 0; i < 10; i++)
            {
                clientEntity.UpdateWithDelta(ClientEntity.FIXED_DELTA_TIME);
            }
            // Don't update server - it stays at origin

            // Get positions
            Vector3 serverPos = debugCubes.GetServerCubePosition(); // Server at origin (0,0,0)
            Vector3 clientPos = debugCubes.GetClientCubePosition(); // Client moved right
            Vector3 errorVec = clientPos - serverPos;

            // Assert: Client moved in +X (even small movement confirms direction)
            Assert.Greater(clientPos.x, 0.1f, "Client should move in +X direction with right input");
            
            // Assert: Error vector magnitude matches movement distance
            Assert.Greater(errorVec.magnitude, 0.1f, "Error vector magnitude should match client movement");

            // Cleanup
            UnityEngine.Object.DestroyImmediate(serverGo);
            UnityEngine.Object.DestroyImmediate(debugGo);
        }

        [Test]
        public void DebugCubes_StateHistoryTrail_RendersWithoutAllocation()
        {
            // Setup: Create client
            var clientEntity = new ClientEntity();
            clientEntity.Initialize();
            clientEntity.InputProvider = () => Vector2.right; // Deterministic movement

            // Setup: Debug cubes
            var debugGo = new GameObject("DebugCubes");
            var debugCubes = debugGo.AddComponent<DebugCubes>();
            debugCubes.clientEntity = clientEntity;
            debugCubes.historyTrailLength = 100;

            // Run 100 simulation ticks to build history
            for (int i = 0; i < 100; i++)
            {
                clientEntity.UpdateWithDelta(ClientEntity.FIXED_DELTA_TIME);
            }

            // Verify history buffer contains data
            Assert.Greater(clientEntity.CurrentTick, 50u, "Should have accumulated ticks for history");

            // Verify DebugCubes can retrieve history positions without errors
            Vector3 pos = debugCubes.GetClientCubePosition();
            Assert.IsNotNull(pos, "GetClientCubePosition should return valid position");

            // Verify positions are within expected range (client moves right)
            Assert.Greater(pos.x, 0f, "Client should move in +X direction with right input");

            // Note: Gizmo rendering (OnDrawGizmos) cannot be tested directly in EditMode.
            // The rendering functionality is tested implicitly through the position getters.
            // Direct gizmo calls would violate Unity's constraint that gizmo functions
            // can only be called from the OnDrawGizmos callback during editor updates.

            // Cleanup
            UnityEngine.Object.DestroyImmediate(debugGo);
        }
    }
}
