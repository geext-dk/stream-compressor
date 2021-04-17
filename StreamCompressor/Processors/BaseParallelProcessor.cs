using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using StreamCompressor.ThreadSafety;

namespace StreamCompressor.Processors
{
    public abstract class BaseParallelProcessor
    {
        protected readonly int BlockSize;
        protected readonly ILogger? Logger;
        private readonly int _numberOfThreads;
        private int _chunksProcessed;
        private readonly AutoResetEvent _chunkProcessedEvent = new(false);

        protected BaseParallelProcessor(int blockSize, int numberOfThreads, ILoggerFactory? loggerFactory = null)
        {
            BlockSize = blockSize;
            _numberOfThreads = numberOfThreads;
            Logger = loggerFactory?.CreateLogger<BaseParallelProcessor>();
        }

        /// <summary>
        /// Compress the input stream and write the result to the output stream.
        /// The input stream will be divided to blocks of blockSize size, which will be compressed independently by
        /// numberOfThreads threads.
        /// </summary>
        /// <param name="inputStream"></param>
        /// <param name="outputStream"></param>
        public void Process(Stream inputStream, Stream outputStream)
        {
            CustomConcurrentDictionary<int, Stream> resultDictionary;
            CustomBlockingCollection<(int, byte[])>? queue = null;
            List<Thread>? threads = null;
            var numberOfChunksEnqueued = 0;
            
            try
            {
                queue = new CustomBlockingCollection<(int, byte[])>(_numberOfThreads);
                resultDictionary = new CustomConcurrentDictionary<int, Stream>();

                Logger?.LogInformation("Launching {NumberOfThreads} threads", _numberOfThreads);
                threads = LaunchThreads(queue, resultDictionary).ToList();

                Logger?.LogInformation("Starting processing the stream");

                foreach (var threadBuf in SplitStream(inputStream))
                {
                    numberOfChunksEnqueued += 1;
                    queue.Enqueue((numberOfChunksEnqueued, threadBuf));
                    Logger?.LogInformation("Enqueued an array number {Number} of length {ArrayLength}",
                        numberOfChunksEnqueued, threadBuf.Length);
                    
                    if (numberOfChunksEnqueued % _numberOfThreads == 0)
                    {
                        Logger?.LogInformation("Waiting for the threads to process all the enqueued chunks");
                        while (_chunksProcessed < numberOfChunksEnqueued)
                        {
                            _chunkProcessedEvent.WaitOne();
                        }
                        
                        for (var i = numberOfChunksEnqueued - _numberOfThreads + 1; i <= numberOfChunksEnqueued; ++i)
                        {
                            using var stream = resultDictionary.Take(i);

                            stream.CopyTo(outputStream);
                        }
                    }
                }
            }
            finally
            {
                if (threads != null)
                {
                    queue!.CompleteAdding();
                    Logger?.LogInformation("Completed adding arrays to the queue");
                    
                    Logger?.LogInformation("Joining threads");
                    JoinThreads(threads);
                    Logger?.LogInformation("Threads joined");
                }
            }

            var chunksLeftToWrite = numberOfChunksEnqueued % _numberOfThreads;
            for (var i = numberOfChunksEnqueued - chunksLeftToWrite + 1; i <= numberOfChunksEnqueued; ++i)
            {
                using var stream = resultDictionary.Take(i);

                stream.CopyTo(outputStream);
            }
            
            Logger?.LogInformation("Total number of chunks compressed: {NumberOfChunks}", numberOfChunksEnqueued);
        }

        private IEnumerable<Thread> LaunchThreads(CustomBlockingCollection<(int, byte[])> queue,
            CustomConcurrentDictionary<int, Stream> resultDictionary)
        {
            for (var i = 0; i < _numberOfThreads; ++i)
            {
                var thread = new Thread(() => ThreadLoop(queue, resultDictionary));
                thread.Start();
                yield return thread;
            }
        }

        protected abstract IEnumerable<byte[]> SplitStream(Stream inputStream);

        protected abstract void PerformAction(Stream inputStream, MemoryStream outputStream);

        private void ThreadLoop(CustomBlockingCollection<(int, byte[])> queue,
            CustomConcurrentDictionary<int, Stream> resultDictionary)
        {
            while (queue.Dequeue(out var tuple))
            {
                var (order, arrayToProcess) = tuple;
                Logger.LogInformation("Starting processing the block number {Number}", order);

                using var originalMemoryStream = new MemoryStream(arrayToProcess);
                var compressedMemoryStream = new MemoryStream();

                PerformAction(originalMemoryStream, compressedMemoryStream);

                compressedMemoryStream.Position = 0;

                Logger.LogInformation("Finished processing the block number {Number}", order);
                resultDictionary.Add(order, compressedMemoryStream);
                
                Interlocked.Increment(ref _chunksProcessed);
                _chunkProcessedEvent.Set();
            }
        }

        private static void JoinThreads(IEnumerable<Thread> threads)
        {
            foreach (var thread in threads)
                thread.Join();
        }
    }
}