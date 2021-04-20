using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using Microsoft.Extensions.Logging;

namespace StreamCompressor.Processors
{
    public class CustomFormatParallelDecompressor : BaseParallelProcessor
    {
        public CustomFormatParallelDecompressor(int blockSize, int numberOfThreads,
            ILoggerFactory? loggerFactory = null)
            : base(blockSize, numberOfThreads, loggerFactory) { }

        protected override IEnumerable<byte[]> SplitStream(Stream inputStream)
        {
            if (!CustomArchiveFormatHelpers.TryReadHeader(inputStream, out var header))
                throw new InvalidOperationException("The header does not conform the the specification.");

            inputStream.Seek((int) header.HeaderSize, SeekOrigin.Begin);
            foreach (var blockSize in header.BlockSizes)
            {
                var array = new byte[blockSize];
                if (inputStream.Read(array) != blockSize)
                    throw new InvalidOperationException("Could not read all of the block bytes");

                yield return array;
            }
        }

        protected override void PerformAction(Stream inputStream, MemoryStream outputStream)
        {
            using var gzipStream = new GZipStream(inputStream, CompressionMode.Decompress, true);
            gzipStream.CopyTo(outputStream);
        }
    }
}