using UnityEngine;
using DeterministicRollback.Entities;

namespace DeterministicRollback.Behaviours
{
    /// <summary>
    /// MonoBehaviour wrapper for ClientEntity. Supplies input and mirrors inspector-configurable network parameters.
    /// Provides deterministic Auto-Move mode for tests and demo scenarios.
    /// </summary>
    public class ClientEntityBehaviour : MonoBehaviour
    {
        /// <summary>
        /// One-way latency in milliseconds used for FakeNetworkPipe.SendInput()
        /// </summary>
        public float latencyMs = 0f;
        [Range(0f, 1f)] public float lossChance = 0f;
        public bool autoMove = false;
        private ClientEntity _client;

        /// <summary>
        /// Initialize the underlying ClientEntity and set up input provider.
        /// </summary>
        void Start()
        {
            _client = new ClientEntity();
            _client.latencyMs = latencyMs;
            _client.lossChance = lossChance;
            _client.InputProvider = ReadInput;
        }

        /// <summary>
        /// Forward Unity Update() to the testable ClientEntity core.
        /// Keeps runtime-configurable parameters in sync.
        /// </summary>
        void Update()
        {
            // Mirror inspector values if changed at runtime
            _client.latencyMs = latencyMs;
            _client.lossChance = lossChance;
            _client.Update();
        }

        private Vector2 ReadInput()
        {
            if (autoMove)
            {
                // Deterministic Auto-Move based on current tick
                float time = _client.CurrentTick * ClientEntity.FIXED_DELTA_TIME;
                float angle = 36f * time * Mathf.Deg2Rad;
                return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            }

            Vector2 v = Vector2.zero;
            if (UnityEngine.Input.GetKey(KeyCode.W)) v.y += 1f;
            if (UnityEngine.Input.GetKey(KeyCode.S)) v.y -= 1f;
            if (UnityEngine.Input.GetKey(KeyCode.A)) v.x -= 1f;
            if (UnityEngine.Input.GetKey(KeyCode.D)) v.x += 1f;
            return v;
        }
    }
}