using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using Microsoft.Extensions.Logging;

namespace StreamCompressor.Processors
{
    public class ParallelCompressor : BaseParallelProcessor
    {
        public ParallelCompressor(int blockSize, int numberOfThreads, ILoggerFactory? loggerFactory = null)
            : base(blockSize, numberOfThreads, loggerFactory) { }

        protected override IEnumerable<byte[]> SplitStream(Stream inputStream)
        {
            byte[] buf = new byte[BlockSize];

            var bytesRead = inputStream.Read(buf, 0, BlockSize);
            while (bytesRead > 0)
            {
                var threadBuf = new byte[bytesRead];

                Array.Copy(buf, threadBuf, bytesRead);

                yield return threadBuf;

                bytesRead = inputStream.Read(buf, 0, BlockSize);
            }
        }

        protected override void PerformAction(Stream inputStream, MemoryStream outputStream)
        {
            using var gzipStream = new GZipStream(outputStream, CompressionMode.Compress, true);
            inputStream.CopyTo(gzipStream);
        }
    }
}