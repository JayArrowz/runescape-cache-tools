﻿using System;
using System.Linq;
using Villermen.RuneScapeCacheTools.Test.Fixture;
using Xunit;
using Xunit.Abstractions;

namespace Villermen.RuneScapeCacheTools.Test.Utility
{
    [Collection(TestCacheCollection.Name)]
    public class SoundtrackExtractorTests
    {
        private ITestOutputHelper Output { get; }

        private TestCacheFixture Fixture { get; }

        public SoundtrackExtractorTests(ITestOutputHelper output, TestCacheFixture fixture)
        {
            this.Output = output;
            this.Fixture = fixture;
        }

        /// <summary>
        /// Soundtrack names must be retrievable.
        ///
        /// Checks if GetTrackNames returns a track with name "Soundscape".
        /// </summary>
        [Fact]
        public void TestGetTrackNames()
        {
            var trackNames = this.Fixture.SoundtrackExtractor.GetTrackNames();

            Assert.Equal(1320, trackNames.Count);

            Assert.True(
                trackNames.Any(trackNamePair => trackNamePair.Value == "Soundscape"),
                "\"Soundscape\" did not occur in the list of track names."
            );
        }

        [Fact]
        public void TestGetAllTrackNames()
        {
            var trackNames = this.Fixture.SoundtrackExtractor.GetTrackNames(true);

            Assert.Equal(1499, trackNames.Count);
            Assert.True(
                trackNames.Any(trackNamePair => trackNamePair.Value == "20386"),
                "\"20386\" did not occur in the list of track names."
            );
        }

        [Theory]
        [InlineData("Soundscape", "Soundscape.ogg", 15, false)] // OGG
        [InlineData("uNDsCa", "Soundscape.flac", 15, true)] // FLAC and partial case insensitive filter matching
        [InlineData("Black Zabeth LIVE!", "Black Zabeth LIVE!.ogg", 15, false)] // Fixing invalid filenames (Actual name is "Black Zabeth: LIVE!" which is invalid on Windows)
        public void TestExtract(string trackName, string expectedFilename, int expectedVersion, bool lossless)
        {
            var startTime = DateTime.UtcNow;
            this.Fixture.SoundtrackExtractor.Extract(true, lossless, false, trackName);

            var expectedOutputPath = $"soundtrack/{expectedFilename}";

            // Verify that Soundscape.ogg has been created
            Assert.True(System.IO.File.Exists(expectedOutputPath), $"{expectedFilename} should've been created during extraction.");

            // Verify that it has been created during this test
            var modifiedTime = System.IO.File.GetLastWriteTimeUtc(expectedOutputPath);
            Assert.True(modifiedTime >= startTime, $"{expectedFilename}'s modified time was not updated during extraction (so probably was not extracted).");

            var version = this.Fixture.SoundtrackExtractor.GetVersionFromExportedTrackFile($"soundtrack/{expectedFilename}");

            Assert.True(version == expectedVersion, $"Version of {expectedFilename} was incorrect ({version} instead of {expectedVersion}).");
        }
    }
}
