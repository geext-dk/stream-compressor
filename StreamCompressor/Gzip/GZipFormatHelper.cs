using System;
using System.Collections.Generic;

namespace StreamCompressor.Gzip
{
    /// <summary>
    /// Class that contains some logic for working with gzip file format
    /// </summary>
    public class GZipFormatHelper
    {
        public const int GZipHeaderLength = 10;

        /// <summary>
        /// Find the index of the first gzip member encountered. Be warned though that the found index may actually
        /// point to something different, as there must be a greater effort involved to implement this. Or if it is not
        /// possible at all, it is advisable to catch errors after that and next time ignore this particular occurence. 
        /// </summary>
        /// <param name="bytes">bytes to search amongst</param>
        /// <param name="fromIndex">the index from which to start</param>
        /// <returns>The index of a member if it is found, or -1 otherwise</returns>
        public static int FindHeader(IReadOnlyList<byte> bytes, int fromIndex)
        {
            for (var i = fromIndex; i < bytes.Count - GZipHeaderLength; ++i)
            {
                if (CheckForHeaderAt(bytes, i))
                    return i;
            }

            return -1;
        }

        /// <summary>
        /// Read more: https://tools.ietf.org/html/rfc1952#section-2.2
        /// </summary>
        /// <param name="bytes"></param>
        /// <param name="index"></param>
        /// <returns></returns>
        public static bool CheckForHeaderAt(IReadOnlyList<byte> bytes, int index)
        {
            if (bytes == null)
                throw new ArgumentNullException(nameof(bytes));
            
            if (index < 0 || index + GZipHeaderLength > bytes.Count)
                return false;
            
            return
                // magic bytes
                bytes[index] == 0x1F
                && bytes[index + 1] == 0x8B
                // compression method is (almost) always DEFLATE, 0x08
                && bytes[index + 2] == 0x08
                // flags. 5th, 6th and 7th bits must always be zero (they are reserved)
                && (bytes[index + 3] & 0xE0) == 0
                // Check that the OS byte is one of the currently defined patterns in the spec
                && (bytes[index + 9] <= 0x0D || bytes[index + 9] == 0xFF);
        }
    }
}