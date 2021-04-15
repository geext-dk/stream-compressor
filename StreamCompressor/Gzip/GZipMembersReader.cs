using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace StreamCompressor.Gzip
{
    /// <summary>
    /// gzip members reader. Helps with reading whole members from gzip files.
    /// gzip member is simply a gzip-compressed file. But every gzip-compressed file can also be a concatenation
    /// of multiple gzip-compressed files. The class helps searching these files in a single blob.
    /// </summary>
    public sealed class GZipMembersReader : IEnumerable<byte[]>
    {
        private readonly BufferedStreamReader _bufferedStreamReader;
        private readonly ILogger? _logger;

        public GZipMembersReader(BufferedStreamReader bufferedStreamReader, ILoggerFactory? loggerFactory = null)
        {
            _bufferedStreamReader = bufferedStreamReader;
            _logger = loggerFactory?.CreateLogger<GZipMembersReader>();
        }

        public IEnumerator<byte[]> GetEnumerator()
        {
            _logger?.LogInformation("Checking stream header");
            var result = CheckHeader();
            if (result != CheckGZipHeaderResult.Ok)
                _logger?.LogWarning("The header is invalid");

            switch (result)
            {
                case CheckGZipHeaderResult.EmptyStream:
                    throw new InvalidOperationException("The file is empty.");
                case CheckGZipHeaderResult.NotAGzipFile:
                    throw new InvalidOperationException("No gzip header was found. Looks like it is not a gzip file.");
            }

            var nextMember = GetNextGZipMember();
            while (nextMember != null)
            {
                _logger?.LogInformation("Returning next member");
                yield return nextMember;
                nextMember = GetNextGZipMember();
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>
        /// Check the file header for validity. It reads a single block from the stream and then checks the first bytes
        /// for a gzip header.
        /// </summary>
        /// <returns></returns>
        public CheckGZipHeaderResult CheckHeader()
        {
            if (_bufferedStreamReader.IsEmpty && !_bufferedStreamReader.IsEof)
                _bufferedStreamReader.ReadAndStoreNextChunk();

            if (_bufferedStreamReader.IsEmpty && _bufferedStreamReader.IsEof)
                return CheckGZipHeaderResult.EmptyStream;

            return GZipFormatHelper.CheckForHeaderAt(_bufferedStreamReader.Buffer, 0)
                ? CheckGZipHeaderResult.Ok
                : CheckGZipHeaderResult.NotAGzipFile;
        }

        private byte[]? GetNextGZipMember()
        {
            if (_bufferedStreamReader.IsEof)
                return null;

            var gZipMemberIndex =
                GZipFormatHelper.FindHeader(_bufferedStreamReader.Buffer, GZipFormatHelper.GZipHeaderLength);

            while (!_bufferedStreamReader.IsEof && gZipMemberIndex == -1)
            {
                _bufferedStreamReader.ReadAndStoreNextChunk();

                gZipMemberIndex =
                    GZipFormatHelper.FindHeader(_bufferedStreamReader.Buffer, GZipFormatHelper.GZipHeaderLength);
            }

            if (gZipMemberIndex != -1)
                return _bufferedStreamReader.CutLeftByIndex(gZipMemberIndex);

            return _bufferedStreamReader.Buffer.Count == 0 ? null : _bufferedStreamReader.TakeAll();
        }
    }
}