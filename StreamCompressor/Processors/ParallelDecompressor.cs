using System.IO;
using System.IO.Compression;
using Microsoft.Extensions.Logging;
using StreamCompressor.Gzip;
using StreamCompressor.ThreadSafety;

namespace StreamCompressor.Processors
{
    public sealed class ParallelDecompressor : BaseParallelProcessor
    {
        private readonly ILoggerFactory? _loggerFactory;

        public ParallelDecompressor(int blockSize, int numberOfThreads, ILoggerFactory? loggerFactory = null)
            : base(blockSize, numberOfThreads, loggerFactory?.CreateLogger<ParallelDecompressor>())
        {
            _loggerFactory = loggerFactory;
        }

        protected override int SplitAndEnqueue(Stream inputStream, CustomBlockingCollection<(int, byte[])> queue)
        {
            using var bufferedStream = new BufferedStreamReader(inputStream, BlockSize, _loggerFactory);
            var membersReader = new GZipMembersReader(bufferedStream, _loggerFactory);

            var numberOfMembers = 0;
            foreach (var member in membersReader)
            {
                queue.Enqueue((numberOfMembers, member));
                Logger?.LogInformation("Enqueued a gzip member of size {BufferSize}", member.Length);

                numberOfMembers++;
            }

            queue.CompleteAdding();
            Logger?.LogInformation("Completed adding arrays to the queue");
            Logger?.LogInformation("Total number of members decompressed: {NumberOfMembers}", numberOfMembers);

            return numberOfMembers;
        }

        protected override void PerformAction(Stream inputStream, MemoryStream outputStream)
        {
            using var gzipStream = new GZipStream(inputStream, CompressionMode.Decompress, true);
            gzipStream.CopyTo(outputStream);
        }
    }
}