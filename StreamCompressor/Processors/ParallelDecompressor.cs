using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using Microsoft.Extensions.Logging;
using StreamCompressor.Gzip;

namespace StreamCompressor.Processors
{
    public sealed class ParallelDecompressor : BaseParallelProcessor
    {
        private readonly ILoggerFactory? _loggerFactory;

        public ParallelDecompressor(int blockSize, int numberOfThreads, ILoggerFactory? loggerFactory = null)
            : base(blockSize, numberOfThreads, loggerFactory)
        {
            _loggerFactory = loggerFactory;
        }

        protected override IEnumerable<byte[]> SplitStream(Stream inputStream)
        {
            var bufferedStream = new BufferedStreamReader(inputStream, BlockSize, _loggerFactory);
            return new GZipMembersReader(bufferedStream, _loggerFactory);
        }

        protected override void PerformAction(Stream inputStream, MemoryStream outputStream)
        {
            using var gzipStream = new GZipStream(inputStream, CompressionMode.Decompress, true);
            gzipStream.CopyTo(outputStream);
        }
    }
}