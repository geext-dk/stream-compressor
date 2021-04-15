using System;
using System.Collections.Generic;

namespace StreamCompressor.ThreadSafety
{
    /// <summary>
    /// I wasn't sure if it's allowed so I made a simplified one by myself
    /// All operations are thread-safe
    /// </summary>
    public class CustomConcurrentDictionary<TKey, TValue> where TKey: notnull
    {
        private readonly Dictionary<TKey, TValue> _inner = new();

        /// <summary>
        /// Add an item to the dictionary
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public void Add(TKey key, TValue value)
        {
            lock (_inner)
            {
                if (_inner.ContainsKey(key))
                {
                    _inner[key] = value;
                }
                else
                {
                    _inner.Add(key, value);
                }
            }
        }

        /// <summary>
        /// Take an item from the dictionary
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        /// <exception cref="IndexOutOfRangeException">An item with the given key has not been found</exception>
        public TValue Take(TKey key)
        {
            lock (_inner)
            {
                if (_inner.Remove(key, out var value))
                {
                    return value;
                }

                throw new IndexOutOfRangeException("An item with the given key has not been found.");
            }
        }
    }
}