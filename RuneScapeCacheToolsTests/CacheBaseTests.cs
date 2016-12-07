﻿using System;
using System.IO;
using RuneScapeCacheToolsTests.Fixtures;
using Villermen.RuneScapeCacheTools.Cache;
using Xunit;
using Xunit.Abstractions;

namespace RuneScapeCacheToolsTests
{
    [Collection("TestCache")]
    public class CacheBaseTests
    {
        private ITestOutputHelper Output { get; }

        private CacheFixture Fixture { get; }

        public CacheBaseTests(ITestOutputHelper output, CacheFixture fixture)
        {
            Output = output;
            Fixture = fixture;
        }

        [Theory]
        [InlineData(Index.ClientScripts, 3)]
        public void TestExtract(Index index, int fileId)
        {
            var expectedFilePath = $"output/extracted/{index}/{fileId}";

            var startTime = DateTime.UtcNow - TimeSpan.FromSeconds(1);

            Fixture.Cache.Extract(index, fileId, true);

            Assert.True(File.Exists(expectedFilePath), $"File was not extracted, or not extracted to {expectedFilePath}.");

            var modifiedTime = File.GetLastAccessTimeUtc(expectedFilePath);

            Assert.True(startTime <= modifiedTime, $"Starting time of test ({startTime}) was not earlier or equal to extracted file modified time ({modifiedTime}).");
        }

        [Fact]
        public void TestExtractWithEntries()
        {
            var expectedFilePath = $"output/extracted/{Index.Enums}/5-65";

            var startTime = DateTime.UtcNow - TimeSpan.FromSeconds(1);

            Fixture.Cache.Extract(Index.Enums, 5, true);

            Assert.True(File.Exists(expectedFilePath), $"File entry was not extracted, or not extracted to {expectedFilePath}.");

            var modifiedTime = File.GetLastAccessTimeUtc(expectedFilePath);

            Assert.True(startTime <= modifiedTime, $"Starting time of test ({startTime}) was not earlier or equal to extracted file modified time ({modifiedTime}).");
        }

        [Fact]
        public void TestExtractExtension()
        {
            // TODO: Use pre-built cache for this
            Fixture.Downloader.Extract(Index.LoadingSprites, 8501);

            // Verify that the .jpg extension was added
            Assert.True(File.Exists($"output/extracted/{Index.LoadingSprites}/8501.jpg"));
        }

        /// <summary>
        /// Test for a file that exists, an archive file that exists and a file that doesn't exist.
        /// </summary>
        [Fact]
        public void TestGetFile()
        {
            var file = Fixture.Cache.GetFile(Index.ClientScripts, 3);

            var fileData = file.Data;

            Assert.True(fileData.Length > 0, "File's data is empty.");

            var archiveFile = Fixture.Cache.GetFile(Index.Enums, 5);

            var archiveEntry = archiveFile.Entries[255];

            Assert.True(archiveEntry.Length > 0, "Archive entry's data is empty.");

            try
            {
                Fixture.Cache.GetFile(Index.Music, 30);

                Assert.True(false, "Cache returned a file that shouldn't exist.");
            }
            catch (CacheException exception)
            {
                Assert.True(exception.Message.Contains("incomplete"), "Non-existent file cache exception had the wrong message.");
            }
        }
    }
}