using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;

namespace StreamCompressor.ThreadSafety
{
    /// <summary>
    /// A collection which blocks if there is no items in the queue, and when there is no free space in the queue.
    ///
    /// All methods are thread-safe
    /// </summary>
    /// <typeparam name="T">The type of underlying items</typeparam>
    public sealed class CustomBlockingCollection<T> : IDisposable
    {
        private readonly object _addLock = new();
        private readonly Semaphore _itemsAddedSemaphore;
        private readonly AutoResetEvent _itemTakenEvent;
        private readonly int _maximumSize;

        private readonly Queue<T> _queue;
        private readonly ManualResetEvent _queueEndEvent;
        private int _currentCount;

        /// <summary>
        /// Create a blocking collection with the specified maximum size
        /// </summary>
        /// <param name="maximumSize"></param>
        public CustomBlockingCollection(int maximumSize)
        {
            _maximumSize = maximumSize;
            _currentCount = 0;
            _itemsAddedSemaphore = new Semaphore(0, _maximumSize);
            _queue = new Queue<T>(_maximumSize);
            _queueEndEvent = new ManualResetEvent(false);
            _itemTakenEvent = new AutoResetEvent(false);
        }

        public void Dispose()
        {
            _queueEndEvent.Dispose();
            _itemsAddedSemaphore.Dispose();
            _itemTakenEvent.Dispose();
        }

        /// <summary>
        /// Add an element to the collection. Blocks if the collection already contains the maximum number of elements.
        /// </summary>
        /// <param name="item"></param>
        public bool Enqueue(T item)
        {
            lock (_addLock)
            {
                if (WaitForFreeSpace())
                {
                    EnqueueItem(item);
                    return true;
                }

                return false;
            }
        }

        /// <summary>
        /// Take the first element in the queue, remove it from the queue and return. Blocks if there are no elements.
        /// Unblocks early if the queue is signaled as ended, assigns a default value and returns false.
        /// If it unblocks because an element is available to be taken out, assigns the element to the `nextItem` and
        /// returns false
        /// </summary>
        /// <param name="nextItem">A variable to which the item will be assigned</param>
        /// <returns>
        /// True if next item is taken and assigned successfully. False if the queue is signalled as ended
        /// </returns>
        public bool Dequeue([MaybeNullWhen(false)] out T nextItem)
        {
            var signaledEventIndex = WaitHandle.WaitAny(new WaitHandle[]
            {
                _itemsAddedSemaphore,
                _queueEndEvent
            });

            if (signaledEventIndex == 1)
            {
                nextItem = default;
                return false;
            }

            nextItem = DequeueItem();

            return true;
        }

        /// <summary>
        /// Signal the collection as ended. After that all currently blocked Dequeue calls will unblock and return
        /// nothing. All subsequent Dequeue calls will not block.
        /// </summary>
        public void CompleteAdding()
        {
            _queueEndEvent.Set();
        }

        private void EnqueueItem(T item)
        {
            lock (_queue)
            {
                _queue.Enqueue(item);

                _currentCount += 1;

                _itemsAddedSemaphore.Release();
            }
        }

        private T DequeueItem()
        {
            lock (_queue)
            {
                var nextItem = _queue.Dequeue();

                _currentCount -= 1;

                _itemTakenEvent.Set();

                return nextItem;
            }
        }

        private bool WaitForFreeSpace()
        {
            while (_currentCount >= _maximumSize)
            {
                var index = WaitHandle.WaitAny(new WaitHandle[]
                {
                    _itemTakenEvent,
                    _queueEndEvent
                });

                if (index == 1)
                    return false;
            }

            return true;
        }
    }
}