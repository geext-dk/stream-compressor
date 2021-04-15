using System.IO;
using System.Linq;
using System.Text;
using StreamCompressor.Gzip;
using Xunit;

namespace StreamCompressor.Tests
{
    public class BufferedStreamReaderTests
    {
        [Fact]
        public void Constructor_PassAnyStream_CreatesWithValidState()
        {
            // Arrange
            var stream = new MemoryStream();
            const int operationBufferLength = 10;
            
            // Act
            using var bufferedStream = new BufferedStreamReader(stream, operationBufferLength);
            
            // Assert
            Assert.False(bufferedStream.IsEof);
            Assert.True(bufferedStream.IsEmpty);
            Assert.Empty(bufferedStream.Buffer);
            Assert.Equal(bufferedStream.OperationBufferLength, operationBufferLength);
        }
        
        [Fact]
        public void Dispose_DisposeWithOpenedStream_DisposesStream()
        {
            // Arrange
            var stream = new MemoryStream(Encoding.UTF8.GetBytes("Hello, world!"));

            // Act
            using (var _ = new BufferedStreamReader(stream, 1)) { }

            // Assert
            Assert.False(stream.CanRead);
        }

        [Fact]
        public void ReadAndStoreNextChunk_StreamIsEmpty_SetsEofTrue()
        {
            // Arrange
            var stream = new MemoryStream();

            using var bufferedStream = new BufferedStreamReader(stream, 10);
            
            // Act
            bufferedStream.ReadAndStoreNextChunk();
            
            // Assert
            Assert.True(bufferedStream.IsEof);
            Assert.True(bufferedStream.IsEmpty);
            Assert.Empty(bufferedStream.Buffer);
        }

        [Fact]
        public void ReadAndStoreNextChunk_AskedAmountOfDataIsAvailable_SuccessfullyAdvancesStream()
        {
            // Arrange
            var bytes = Encoding.UTF8.GetBytes("Hello, World!");
            var stream = new MemoryStream(bytes);

            using var bufferedStream = new BufferedStreamReader(stream, 5);
            
            // Act
            bufferedStream.ReadAndStoreNextChunk();
            
            // Assert
            Assert.False(bufferedStream.IsEof);
            Assert.False(bufferedStream.IsEmpty);
            Assert.Equal(bytes.Take(5).ToArray(), bufferedStream.Buffer);
        }

        [Fact]
        public void ReadAndStoreNextChunk_AskedMoreThanItCanReturn_SuccessfullyAdvancesStream()
        {
            // Arrange
            var bytes = Encoding.UTF8.GetBytes("Hello!");
            var stream = new MemoryStream(bytes);

            using var bufferedStream = new BufferedStreamReader(stream, 10);
            
            // Act
            bufferedStream.ReadAndStoreNextChunk();
            
            // Assert
            Assert.False(bufferedStream.IsEof);
            Assert.False(bufferedStream.IsEmpty);
            Assert.Equal(bytes, bufferedStream.Buffer);
        }

        [Fact]
        public void ReadAndStoreNextChunk_AskedMoreThanItCanReturnThenAdvance_SuccessfullyAdvancesStreamAndReturnsEof()
        {
            // Arrange
            var bytes = Encoding.UTF8.GetBytes("Hello!");
            var stream = new MemoryStream(bytes);

            using var bufferedStream = new BufferedStreamReader(stream, 10);
            
            // Act
            bufferedStream.ReadAndStoreNextChunk();
            bufferedStream.ReadAndStoreNextChunk();
            
            // Assert
            Assert.True(bufferedStream.IsEof);
            Assert.False(bufferedStream.IsEmpty);
            Assert.Equal(bytes, bufferedStream.Buffer);
        }
    }
}