using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Logging;
using StreamCompressor.Util;

namespace StreamCompressor.Gzip
{
    public sealed class BufferedStreamReader : IDisposable
    {
        private readonly byte[] _operationBuffer;
        public int OperationBufferLength { get; }
        private int _operationBufferActualLength;
        
        private byte[] _buffer = Array.Empty<byte>();
        
        private readonly Stream _stream;
        private readonly ILogger? _logger;

        public BufferedStreamReader(Stream stream, int operationBufferLength, ILoggerFactory? loggerFactory = null)
        {
            OperationBufferLength = operationBufferLength;
            _operationBuffer = new byte[operationBufferLength];
            _stream = stream;
            _logger = loggerFactory?.CreateLogger<BufferedStreamReader>();
        }
        
        /// <summary>
        /// Is the underlying stream contains no bytes to read
        /// </summary>
        public bool IsEof { get; private set; }
        
        /// <summary>
        /// Is the buffer is empty
        /// </summary>
        public bool IsEmpty => _buffer.Length == 0;
        
        /// <summary>
        /// Buffer where all the read bytes are stored
        /// </summary>
        public IReadOnlyList<byte> Buffer => _buffer;
        
        /// <summary>
        /// Read the next OperationBufferLength number of bytes from the stream. If the last time 
        /// </summary>
        /// <returns></returns>
        public void ReadAndStoreNextChunk()
        {
            _operationBufferActualLength = _stream.Read(_operationBuffer, 0, OperationBufferLength);

            if (_operationBufferActualLength != 0)
            {
                _buffer =
                    ArrayUtil.Concatenate(_buffer, _buffer.Length, _operationBuffer, _operationBufferActualLength);
            }
            else
            {
                IsEof = true;
                _logger?.LogInformation("EOF reached");
            }
        }

        /// <summary>
        /// Cut the array at the given index, return the left part and keep the right part
        /// </summary>
        /// <param name="index"></param>
        /// <returns>Left part of the array</returns>
        public byte[] CutLeftByIndex(int index)
        {
            var (left, right) = ArrayUtil.SplitByIndex(_buffer, index);
            _buffer = right;
            return left;
        }

        /// <summary>
        /// Empty the underlying buffer and return all the bytes from it
        /// </summary>
        /// <returns></returns>
        public byte[] TakeAll()
        {
            var buf = _buffer;
            _buffer = Array.Empty<byte>();
            return buf;
        }

        public void Dispose()
        {
            _stream.Dispose();
        }
    }
}