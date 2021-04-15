using System;
using System.IO;
using System.IO.Compression;
using Microsoft.Extensions.Logging;
using StreamCompressor.ThreadSafety;

namespace StreamCompressor.Processors
{
    public sealed class ParallelCompressor : BaseParallelProcessor
    {
        public ParallelCompressor(int blockSize, int numberOfThreads, ILoggerFactory? loggerFactory = null)
            : base(blockSize, numberOfThreads, loggerFactory?.CreateLogger<ParallelCompressor>()) { }

        protected override int SplitAndEnqueue(Stream inputStream, CustomBlockingCollection<(int, byte[])> queue)
        {
            byte[] buf = new byte[BlockSize];

            var numberOfChunks = 0;

            var bytesRead = inputStream.Read(buf, 0, BlockSize);
            while (bytesRead > 0)
            {
                var threadBuf = new byte[bytesRead];

                Array.Copy(buf, threadBuf, bytesRead);

                queue.Enqueue((numberOfChunks, threadBuf));
                Logger?.LogInformation("Enqueued an array of size {BufferSize}", bytesRead);

                numberOfChunks++;
                bytesRead = inputStream.Read(buf, 0, BlockSize);
            }

            queue.CompleteAdding();
            Logger?.LogInformation("Completed adding arrays to the queue");
            Logger?.LogInformation("Total number of chunks compressed: {NumberOfChunks}", numberOfChunks);

            return numberOfChunks;
        }

        protected override void PerformAction(Stream inputStream, MemoryStream outputStream)
        {
            using var gzipStream = new GZipStream(outputStream, CompressionMode.Compress, true);
            inputStream.CopyTo(gzipStream);
        }
    }
}