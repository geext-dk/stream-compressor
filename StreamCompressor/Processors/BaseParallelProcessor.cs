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
        protected readonly int NumberOfThreads;

        protected BaseParallelProcessor(int blockSize, int numberOfThreads, ILogger? logger = null)
        {
            BlockSize = blockSize;
            NumberOfThreads = numberOfThreads;
            Logger = logger;
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
            int numberOfChunks;
            List<Thread>? threads = null;
            try
            {
                queue = new CustomBlockingCollection<(int, byte[])>(NumberOfThreads);
                resultDictionary = new CustomConcurrentDictionary<int, Stream>();

                Logger?.LogInformation("Launching {NumberOfThreads} threads", NumberOfThreads);
                threads = LaunchThreads(queue, resultDictionary).ToList();

                Logger?.LogInformation("Starting processing the stream");
                numberOfChunks = SplitAndEnqueue(inputStream, queue);
            }
            finally
            {
                if (threads != null)
                {
                    queue!.CompleteAdding();
                    JoinThreads(threads);
                }
            }


            WriteToStream(resultDictionary, numberOfChunks, outputStream);
        }

        private IEnumerable<Thread> LaunchThreads(CustomBlockingCollection<(int, byte[])> queue,
            CustomConcurrentDictionary<int, Stream> resultDictionary)
        {
            for (var i = 0; i < NumberOfThreads; ++i)
            {
                var thread = new Thread(() => ThreadLoop(queue, resultDictionary));
                thread.Start();
                yield return thread;
            }
        }

        protected abstract int SplitAndEnqueue(Stream inputStream, CustomBlockingCollection<(int, byte[])> queue);

        protected abstract void PerformAction(Stream inputStream, MemoryStream outputStream);

        private static void WriteToStream(CustomConcurrentDictionary<int, Stream> dictionary,
            int numberOfChunks, Stream outputStream)
        {
            for (var i = 0; i < numberOfChunks; ++i)
            {
                using var stream = dictionary.Take(i);

                stream.CopyTo(outputStream);
            }
        }

        private void ThreadLoop(CustomBlockingCollection<(int, byte[])> queue,
            CustomConcurrentDictionary<int, Stream> resultDictionary)
        {
            while (queue.Dequeue(out var tuple))
            {
                var (order, arrayToProcess) = tuple;

                using var originalMemoryStream = new MemoryStream(arrayToProcess);
                var compressedMemoryStream = new MemoryStream();

                PerformAction(originalMemoryStream, compressedMemoryStream);

                compressedMemoryStream.Position = 0;

                resultDictionary.Add(order, compressedMemoryStream);
            }
        }

        private static void JoinThreads(IEnumerable<Thread> threads)
        {
            foreach (var thread in threads)
                thread.Join();
        }
    }
}