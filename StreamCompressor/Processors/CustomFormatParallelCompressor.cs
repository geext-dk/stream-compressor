using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Logging;
using StreamCompressor.ThreadSafety;

namespace StreamCompressor.Processors
{
    public class CustomFormatParallelCompressor : ParallelCompressor
    {
        public CustomFormatParallelCompressor(int blockSize, int numberOfThreads, ILoggerFactory? loggerFactory = null)
            : base(blockSize, numberOfThreads, loggerFactory) { }

        protected override void WriteToFileThreadLoop(CustomBlockingCollection<(byte[], int)> resultByteArrays,
            Stream outputStream)
        {
            outputStream.Seek((long) CustomArchiveFormatHelpers.ArchiveHeaderSize, SeekOrigin.Begin);
            var blockSizes = new List<uint>();
            while (resultByteArrays.Dequeue(out var byteArrayAndLength))
            {
                var (byteArray, length) = byteArrayAndLength;
                outputStream.Write(byteArray, 0, length);
                blockSizes.Add((uint) length);
            }

            outputStream.Seek(0, SeekOrigin.Begin);

            CustomArchiveFormatHelpers.WriteHeader(outputStream, blockSizes);
        }
    }
}