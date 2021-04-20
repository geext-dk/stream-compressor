using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;

namespace StreamCompressor
{
    public static class CustomArchiveFormatHelpers
    {
        public const string MagicBytesString = "DeKa";
        public const ulong ArchiveHeaderSize = 1024 * 1024; // 1MB
        public static IEnumerable<byte> MagicBytes => Encoding.ASCII.GetBytes(MagicBytesString);

        public static void WriteHeader(Stream stream, IReadOnlyCollection<uint> blockSizes)
        {
            // 1. Custom 4 magic bytes (0x44 0x65 0x4B 0x61, or DeKa in chars, stands for Denis Karpovskiy. Yes.)
            // 2. number of blocks, 4 bytes (max blocks 2^32 - 1)
            // 3. header size, 8 bytes (max header size 2^64 - 1)
            // 4 and so on: sizes of every block 4 bytes each
            var header = MagicBytes
                .Concat(BitConverter.GetBytes((uint) blockSizes.Count))
                .Concat(BitConverter.GetBytes(ArchiveHeaderSize))
                .Concat(blockSizes.SelectMany(BitConverter.GetBytes))
                .Concat(Enumerable.Repeat((byte) 0, (int) ArchiveHeaderSize - 16 - blockSizes.Count * 4))
                .ToArray();

            stream.Write(header);
        }

        public static bool TryReadHeader(Stream stream, [MaybeNullWhen(false)] out CustomArchiveFormatHeader header)
        {
            header = null;
            try
            {
                var buf = new byte[4];
                if (stream.Read(buf) != 4)
                    return false;

                if (Encoding.ASCII.GetString(buf) != MagicBytesString)
                    return false;

                if (stream.Read(buf) != 4)
                    return false;

                var numberOfBlocks = BitConverter.ToUInt32(buf);

                var bigBuf = new byte[8];
                if (stream.Read(bigBuf) != 8)
                    return false;

                var headerSize = BitConverter.ToUInt64(bigBuf);

                var blockSizes = new uint[numberOfBlocks];

                for (var i = 0; i < numberOfBlocks; ++i)
                {
                    if (stream.Read(buf) != 4)
                        return false;

                    blockSizes[i] = BitConverter.ToUInt32(buf);
                }

                header = new CustomArchiveFormatHeader(headerSize, blockSizes);

                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    public class CustomArchiveFormatHeader
    {
        public CustomArchiveFormatHeader(ulong headerSize, uint[] blockSizes)
        {
            HeaderSize = headerSize;
            BlockSizes = blockSizes;
        }

        public ulong HeaderSize { get; }
        public uint[] BlockSizes { get; }
    }
}