using System;
using StreamCompressor.Gzip;
using Xunit;

namespace StreamCompressor.Tests
{
    public class GZipFormatHelperTests
    {
        [Fact]
        public void CheckForHeaderAt_SpecifiedInvalidIndex_ReturnsFalse()
        {
            // Arrange
            var array = new byte[10];
            var index = 10000;
            
            // Act
            var isHeader = GZipFormatHelper.CheckForHeaderAt(array, index);
            
            // Assert
            Assert.False(isHeader);
        }
        
        [Fact]
        public void CheckForHeaderAt_PassEmptyArray_ReturnsFalse()
        {
            // Arrange
            var array = new byte[0];
            var index = 0;
            
            // Act
            var isHeader = GZipFormatHelper.CheckForHeaderAt(array, index);
            
            // Assert
            Assert.False(isHeader);
        }
        
        [Fact]
        public void CheckForHeaderAt_PassNullArray_ThrowsArgumentNullException()
        {
            // Arrange
            const int index = 0;
            
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => GZipFormatHelper.CheckForHeaderAt(null!, index));
        }
        
        [Fact]
        public void CheckForHeaderAt_PassArrayWithHeaderAtGivenIndex_ReturnsTrue()
        {
            // Arrange
            var array = new byte[]
            {
                0x1F,
                0x8B,
                0x08,
                0x00,
                0x00,
                0x00,
                0x00,
                0x00,
                0x00,
                0x00
            };
            
            const int index = 0;
            
            // Act & Assert
            Assert.True(GZipFormatHelper.CheckForHeaderAt(array, index));
        }
    }
}