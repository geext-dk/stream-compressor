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
        private readonly object _chunksProcessedLock = new();
        private readonly int _numberOfThreads;
        protected readonly int BlockSize;
        private readonly ILogger? _logger;
        private volatile int _chunksProcessed;

        protected BaseParallelProcessor(int blockSize, int numberOfThreads, ILoggerFactory? loggerFactory = null)
        {
            BlockSize = blockSize;
            _numberOfThreads = numberOfThreads;
            _logger = loggerFactory?.CreateLogger(GetType());
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
            CustomBlockingCollection<(int, byte[])>? queue = null;
            CustomBlockingCollection<(byte[], int)>? resultByteArrays = null;
            List<Thread>? processingThreads = null;
            Thread? writingToFileThread = null;
            var numberOfChunksEnqueued = 0;

            try
            {
                queue = new CustomBlockingCollection<(int, byte[])>(_numberOfThreads);
                resultByteArrays = new CustomBlockingCollection<(byte[], int)>(_numberOfThreads);

                _logger?.LogInformation("Launching {NumberOfThreads} threads", _numberOfThreads);
                processingThreads = LaunchProcessingThreads(queue, resultByteArrays).ToList();

                writingToFileThread = LaunchWritingToFileThread(resultByteArrays, outputStream);

                _logger?.LogInformation("Starting processing the stream");

                foreach (var threadBuf in SplitStream(inputStream))
                {
                    numberOfChunksEnqueued += 1;
                    queue.Enqueue((numberOfChunksEnqueued, threadBuf));
                    _logger?.LogInformation("Enqueued an array number {Number} of length {ArrayLength}",
                        numberOfChunksEnqueued, threadBuf.Length);
                }
            }
            finally
            {
                if (processingThreads != null)
                {
                    queue!.CompleteAdding();
                    _logger?.LogInformation("Completed adding arrays to the queue");

                    _logger?.LogInformation("Joining threads");
                    JoinThreads(processingThreads);

                    if (writingToFileThread != null)
                    {
                        resultByteArrays!.CompleteAdding();
                        JoinThreads(new[]
                        {
                            writingToFileThread
                        });
                    }

                    _logger?.LogInformation("Threads joined");
                }
            }

            _logger?.LogInformation("Total number of chunks compressed: {NumberOfChunks}", numberOfChunksEnqueued);
        }

        private IEnumerable<Thread> LaunchProcessingThreads(CustomBlockingCollection<(int, byte[])> queue,
            CustomBlockingCollection<(byte[], int)> resultByteArrays)
        {
            for (var i = 0; i < _numberOfThreads; ++i)
            {
                var thread = new Thread(() => ProcessingThreadLoop(queue, resultByteArrays));
                thread.Start();
                yield return thread;
            }
        }

        private Thread LaunchWritingToFileThread(CustomBlockingCollection<(byte[], int)> resultByteArrays,
            Stream outputStream)
        {
            var thread = new Thread(() => WriteToFileThreadLoop(resultByteArrays, outputStream));
            thread.Start();

            return thread;
        }

        protected abstract IEnumerable<byte[]> SplitStream(Stream inputStream);

        protected abstract void PerformAction(Stream inputStream, MemoryStream outputStream);

        private void ProcessingThreadLoop(CustomBlockingCollection<(int, byte[])> queue,
            CustomBlockingCollection<(byte[], int)> resultByteArrays)
        {
            while (queue.Dequeue(out var tuple))
            {
                var (order, arrayToProcess) = tuple;
                _logger.LogInformation("Starting processing the block number {Number}", order);

                using var originalMemoryStream = new MemoryStream(arrayToProcess);
                using var compressedMemoryStream = new MemoryStream();

                PerformAction(originalMemoryStream, compressedMemoryStream);

                _logger?.LogInformation("Finished processing the block number {Number}", order);

                // wait for all the previous chunks to be added to the dictionary
                // this way the chunks will always appear sequentially in the dictionary
                lock (_chunksProcessedLock)
                {
                    while (_chunksProcessed + 1 < order)
                        Monitor.Wait(_chunksProcessedLock);

                    resultByteArrays.Enqueue((compressedMemoryStream.GetBuffer(), (int) compressedMemoryStream.Length));
                    Interlocked.Increment(ref _chunksProcessed);
                    Monitor.PulseAll(_chunksProcessedLock);
                }

                _logger?.LogInformation("Enqueued the block number {Number} to the result queue", order);
            }
        }

        protected virtual void WriteToFileThreadLoop(CustomBlockingCollection<(byte[], int)> resultByteArrays,
            Stream outputStream)
        {
            while (resultByteArrays.Dequeue(out var byteArrayAndLength))
            {
                var (byteArray, length) = byteArrayAndLength;
                outputStream.Write(byteArray, 0, length);
            }
        }

        private static void JoinThreads(IEnumerable<Thread> threads)
        {
            foreach (var thread in threads)
                thread.Join();
        }
    }
}