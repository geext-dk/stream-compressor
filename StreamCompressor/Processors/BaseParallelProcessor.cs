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
        private volatile int _chunksProcessed;

        private readonly object _chunksProcessedLock = new();

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
            CustomBlockingCollection<Stream> resultStreamsQueue;
            CustomBlockingCollection<(int, byte[])>? queue = null;
            List<Thread>? threads = null;
            var numberOfChunksEnqueued = 0;
            
            try
            {
                queue = new CustomBlockingCollection<(int, byte[])>(_numberOfThreads);
                resultStreamsQueue = new CustomBlockingCollection<Stream>(_numberOfThreads);

                Logger?.LogInformation("Launching {NumberOfThreads} threads", _numberOfThreads);
                threads = LaunchThreads(queue, resultStreamsQueue).ToList();

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
                        lock (_chunksProcessedLock)
                        {
                            while (_chunksProcessed < numberOfChunksEnqueued)
                            {
                                Monitor.Wait(_chunksProcessedLock);
                            }
                        }
                        
                        WriteChunks(resultStreamsQueue, outputStream, _numberOfThreads);
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

            WriteChunks(resultStreamsQueue, outputStream, numberOfChunksEnqueued % _numberOfThreads);
            
            Logger?.LogInformation("Total number of chunks compressed: {NumberOfChunks}", numberOfChunksEnqueued);
        }

        private static void WriteChunks(CustomBlockingCollection<Stream> resultStreamsQueue, Stream outputStream,
            int numberOfChunks)
        {
            for (var i = 0; i < numberOfChunks; ++i)
            {
                if (!resultStreamsQueue.Dequeue(out var stream))
                    break;
                stream.CopyTo(outputStream);
            }
        }

        private IEnumerable<Thread> LaunchThreads(CustomBlockingCollection<(int, byte[])> queue,
            CustomBlockingCollection<Stream> resultStreamsQueue)
        {
            for (var i = 0; i < _numberOfThreads; ++i)
            {
                var thread = new Thread(() => ProcessThreadLoop(queue, resultStreamsQueue));
                thread.Start();
                yield return thread;
            }
        }

        protected abstract IEnumerable<byte[]> SplitStream(Stream inputStream);

        protected abstract void PerformAction(Stream inputStream, MemoryStream outputStream);
        private void ProcessThreadLoop(CustomBlockingCollection<(int, byte[])> queue,
            CustomBlockingCollection<Stream> resultStreamsQueue)
        {
            while (queue.Dequeue(out var tuple))
            {
                var (order, arrayToProcess) = tuple;
                Logger.LogInformation("Starting processing the block number {Number}", order);

                using var originalMemoryStream = new MemoryStream(arrayToProcess);
                var compressedMemoryStream = new MemoryStream();

                PerformAction(originalMemoryStream, compressedMemoryStream);

                compressedMemoryStream.Position = 0;

                Logger?.LogInformation("Finished processing the block number {Number}", order);

                // wait for all the previous chunks to be added to the dictionary
                // this way the chunks will always appear sequentially in the dictionary
                lock (_chunksProcessedLock)
                {
                    while (_chunksProcessed + 1 < order)
                        Monitor.Wait(_chunksProcessedLock);

                    resultStreamsQueue.Enqueue(compressedMemoryStream);
                    Interlocked.Increment(ref _chunksProcessed);
                    Monitor.PulseAll(_chunksProcessedLock);
                }

                Logger?.LogInformation("Enqueued the block number {Number} to the result queue", order);
            }
        }

        private static void JoinThreads(IEnumerable<Thread> threads)
        {
            foreach (var thread in threads)
                thread.Join();
        }
    }
}