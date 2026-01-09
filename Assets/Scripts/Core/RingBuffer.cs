using System;

namespace DeterministicRollback.Core
{
    /// <summary>
    /// Pre-allocated circular buffer with ghost data protection via Slot wrapper.
    /// CRITICAL: Indexer validates stored tick matches requested tick.
    /// Throws exception on tick mismatch to prevent silent data corruption.
    /// </summary>
    public class RingBuffer<T> where T : struct
    {
        private readonly Slot<T>[] _array;
        private readonly uint _capacity;

        public RingBuffer(uint capacity)
        {
            _capacity = capacity;
            _array = new Slot<T>[capacity];
        }

        /// <summary>
        /// Indexer with ghost data protection.
        /// CRITICAL: Validates slot.tick == tick before returning data.
        /// </summary>
        public T this[uint tick]
        {
            get
            {
                Slot<T> slot = _array[tick % _capacity];
                if (!slot.isValid || slot.tick != tick)
                {
                    throw new Exception($"Ghost data detected: requested tick {tick}, found tick {slot.tick} (valid={slot.isValid})");
                }
                return slot.data;
            }
            set
            {
                _array[tick % _capacity] = new Slot<T>
                {
                    tick = tick,
                    data = value,
                    isValid = true
                };
            }
        }

        /// <summary>
        /// Check if buffer contains valid data for given tick.
        /// Returns true only if stored tick matches requested tick.
        /// </summary>
        public bool Contains(uint tick)
        {
            Slot<T> slot = _array[tick % _capacity];
            return slot.isValid && slot.tick == tick;
        }

        /// <summary>
        /// Expose capacity for validation and bounds checks.
        /// </summary>
        public uint Capacity => _capacity;

        /// <summary>
        /// Invalidate range of ticks (inclusive).
        /// Used during hard snap to clear diverged speculative data.
        /// </summary>
        public void ClearRange(uint startTick, uint endTick)
        {
            for (uint t = startTick; t <= endTick; t++)
            {
                uint index = t % _capacity;
                _array[index].isValid = false;
            }
        }
    }
}
