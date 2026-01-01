namespace DeterministicRollback.Core
{
    /// <summary>
    /// Deterministic simulation engine using pure Newtonian physics.
    /// CRITICAL: This is a stateless pure function - no global tick state.
    /// </summary>
    public static class SimulationMath
    {
        public const float FIXED_DELTA_TIME = 1.0f / 60.0f;
        private const float FORCE = 10.0f;

        /// <summary>
        /// Integrate physics for one tick.
        /// CRITICAL: Pure math only. Does NOT increment global tick.
        /// Returns identical output for identical inputs (deterministic).
        /// </summary>
        public static void Integrate(ref StatePayload current, ref InputPayload input)
        {
            // Apply input force
            current.velocity += (input.inputVector * FORCE) * FIXED_DELTA_TIME;
            
            // Integrate velocity into position
            current.position += current.velocity * FIXED_DELTA_TIME;
            
            // Update tick to match input
            current.tick = input.tick;
        }
    }
}
