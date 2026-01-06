namespace DeterministicRollback.Networking
{
    /// <summary>
    /// Generic packet container for network simulation.
    /// IMMUTABILITY: Packet fields are written once at creation and never modified.
    /// deliveryTime is compared against Time.unscaledTime, not aged every frame.
    /// </summary>
    public struct Packet<T> where T : struct
    {
        public T payload;
        public float deliveryTime;
    }
}
