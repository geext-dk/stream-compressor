using System;
using System.Collections.Generic;
using System.IO;
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
        private Exception? _threadException;

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
            Thread[]? processingThreads = null;
            Thread? writingToFileThread = null;
            var numberOfChunksEnqueued = 0;

            try
            {
                queue = new CustomBlockingCollection<(int, byte[])>(_numberOfThreads);
                resultByteArrays = new CustomBlockingCollection<(byte[], int)>(_numberOfThreads);

                _logger?.LogInformation("Launching {NumberOfThreads} threads", _numberOfThreads);
                (processingThreads, writingToFileThread) =
                    LaunchProcessingThreads(queue, resultByteArrays, outputStream);

                _logger?.LogInformation("Starting processing the stream");

                foreach (var threadBuf in SplitStream(inputStream))
                {
                    numberOfChunksEnqueued += 1;
                    if (queue.Enqueue((numberOfChunksEnqueued, threadBuf)))
                    {
                        _logger?.LogInformation("Enqueued an array number {Number} of length {ArrayLength}",
                            numberOfChunksEnqueued, threadBuf.Length);
                    }
                    else
                    {
                        _logger?.LogInformation("The queue was closed due to an exception. Exiting");
                        throw new InvalidOperationException("One of the threads threw an exception.", _threadException);
                    }
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

        private (Thread[], Thread) LaunchProcessingThreads(CustomBlockingCollection<(int, byte[])> queue,
            CustomBlockingCollection<(byte[], int)> resultByteArrays, Stream outputStream)
        {
            void HandleException(Exception ex)
            {
                _threadException = ex;
                queue.CompleteAdding();
                resultByteArrays.CompleteAdding();
            }

            var processingThreads = new Thread[_numberOfThreads];
            for (var i = 0; i < _numberOfThreads; ++i)
            {
                processingThreads[i] = new Thread(() => ProcessingThreadLoop(queue, resultByteArrays, HandleException));
                processingThreads[i].Start();
            }

            var writingThread = new Thread(
                () => WriteToFileThreadLoop(resultByteArrays, outputStream, HandleException));
            writingThread.Start();

            return (processingThreads, writingThread);
        }

        protected abstract IEnumerable<byte[]> SplitStream(Stream inputStream);

        protected abstract void PerformAction(Stream inputStream, MemoryStream outputStream);

        private void ProcessingThreadLoop(CustomBlockingCollection<(int, byte[])> queue,
            CustomBlockingCollection<(byte[], int)> resultByteArrays,
            Action<Exception> onException)
        {
            try
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

                        var newItem = (compressedMemoryStream.GetBuffer(), (int) compressedMemoryStream.Length);
                        if (!resultByteArrays.Enqueue(newItem))
                            return;
                        
                        Interlocked.Increment(ref _chunksProcessed);
                        Monitor.PulseAll(_chunksProcessedLock);
                    }

                    _logger?.LogInformation("Enqueued the block number {Number} to the result queue", order);
                }
            }
            catch (Exception ex)
            {
                onException(ex);
            }
        }

        protected virtual void WriteToFileThreadLoop(CustomBlockingCollection<(byte[], int)> resultByteArrays,
            Stream outputStream, Action<Exception> onException)
        {
            try
            {

                while (resultByteArrays.Dequeue(out var byteArrayAndLength))
                {
                    var (byteArray, length) = byteArrayAndLength;
                    outputStream.Write(byteArray, 0, length);
                }
            }
            catch (Exception ex)
            {
                onException(ex);
            }
        }

        private static void JoinThreads(IEnumerable<Thread> threads)
        {
            foreach (var thread in threads)
                thread.Join();
        }
    }
}