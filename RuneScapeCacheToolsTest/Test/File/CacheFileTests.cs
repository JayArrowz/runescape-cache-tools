using Villermen.RuneScapeCacheTools.Model;
using Xunit;

namespace Villermen.RuneScapeCacheTools.Test.File
{
    public class CacheFileTests : BaseTests
    {
        [Theory]
        [InlineData(CompressionType.None)]
        [InlineData(CompressionType.Bzip2)]
        [InlineData(CompressionType.Gzip)]
        // TODO: [InlineData(CompressionType.Lzma)]
        public void TestCompression(CompressionType compressionType)
        {
            var data = new byte[] { 0x41, 0x20, 0x71, 0x20, 0x70, 0x0A, 0x2E, 0x20, 0x20, 0x20, 0x20, 0x77 };

            var file = new RuneScapeCacheTools.File.CacheFile(data);
            file.Info.CompressionType = compressionType;

            var encodedData = file.Encode();

            var decodedFile = RuneScapeCacheTools.File.CacheFile.Decode(encodedData, new CacheFileInfo());

            Assert.Equal(data, decodedFile.Data);
        }
    }
}
