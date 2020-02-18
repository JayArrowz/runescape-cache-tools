﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Villermen.RuneScapeCacheTools.Extension;
using Villermen.RuneScapeCacheTools.Model;

namespace Villermen.RuneScapeCacheTools.Utility
{
    /// <summary>
    ///     A file store holds multiple files inside a "virtual" file system made up of several index files and a single data
    ///     file.
    /// </summary>
    /// <author>Graham</author>
    /// <author>`Discardedx2</author>
    /// <author>Villermen</author>
    // TODO: See if this class can be replaced by RuneTek5Cache and a Sector class replacement that can read full files from a stream.
    public class FileStore : IDisposable
    {
        /// <summary>
        ///     Lock that is used when reading data from the streams.
        /// </summary>
        private readonly object _ioLock = new object();

        private readonly Dictionary<CacheIndex, Stream> _indexStreams = new Dictionary<CacheIndex, Stream>();

        private readonly Stream _dataStream;

        /// <summary>
        ///     Opens the file store in the specified directory.
        /// </summary>
        /// <param name="cacheDirectory">The directory containing the index and data files.</param>
        /// <param name="readOnly">No empty cache will be initialized if only reading, and writing will be disallowed.</param>
        /// <exception cref="FileDoesNotExistException">If any of the main_file_cache.* files could not be found.</exception>
        public FileStore(string cacheDirectory, bool readOnly = true)
        {
            this.CacheDirectory = PathExtensions.FixDirectory(cacheDirectory);
            this.ReadOnly = readOnly;

            if (!this.ReadOnly)
            {
                Directory.CreateDirectory(this.CacheDirectory);
            }

            var fileAccess = this.ReadOnly ? FileAccess.Read : FileAccess.ReadWrite;

            var dataFilePath = Path.Combine(this.CacheDirectory, "main_file_cache.dat2");

            if (this.ReadOnly && !System.IO.File.Exists(dataFilePath))
            {
                throw new FileNotFoundException("Cache data file does not exist.");
            }

            this._dataStream = System.IO.File.Open(dataFilePath, FileMode.OpenOrCreate, fileAccess);

            // Load in existing index files
            for (var indexId = 0; indexId <= 255; indexId++)
            {
                var indexFile = Path.Combine(this.CacheDirectory, "main_file_cache.idx" + indexId);

                if (!System.IO.File.Exists(indexFile))
                {
                    continue;
                }

                this._indexStreams.Add((CacheIndex)indexId, System.IO.File.Open(indexFile, FileMode.Open, fileAccess));
            }
        }

        public bool ReadOnly { get; private set; }

        public string CacheDirectory { get; private set; }

        /// <summary>
        ///     The loaded/existing indexes.
        /// </summary>
        public IEnumerable<CacheIndex> GetIndexes()
        {
            return this._indexStreams.Keys.Where(index => index != CacheIndex.ReferenceTables);
        }

        /// <summary>
        /// Reads the sectors
        /// </summary>
        /// <param name="cacheIndex"></param>
        /// <param name="fileId"></param>
        /// <returns></returns>
        public byte[] ReadFileData(CacheIndex cacheIndex, int fileId)
        {
            int filesize;
            return this.ReadSectors(cacheIndex, fileId, out filesize).Aggregate(new List<byte>(), (bytes, sector) =>
            {
                bytes.AddRange(sector.Data);
                return bytes;
            }).Take(filesize).ToArray();
        }

        private IEnumerable<Sector> ReadSectors(CacheIndex cacheIndex, int fileId)
        {
            int filesize;
            return this.ReadSectors(cacheIndex, fileId, out filesize);
        }

        private IEnumerable<Sector> ReadSectors(CacheIndex cacheIndex, int fileId, out int filesize)
        {
            if (!this._indexStreams.ContainsKey(cacheIndex))
            {
                throw new FileNotFoundException($"Index does not exist for {(int)cacheIndex}/{fileId}.");
            }

            var indexReader = new BinaryReader(this._indexStreams[cacheIndex]);

            var indexPosition = (long)fileId * IndexPointer.Length;

            if (indexPosition < 0 || indexPosition >= indexReader.BaseStream.Length)
            {
                throw new FileNotFoundException($"{(int)cacheIndex}/{fileId} is outside of the index file's bounds.");
            }

            var sectors = new List<Sector>();

            // Lock stream, to prevent multiple threads from calling this method at the same time
            lock (this._ioLock)
            {
                indexReader.BaseStream.Position = indexPosition;
                var indexPointer = IndexPointer.Decode(indexReader.BaseStream);

                filesize = indexPointer.Filesize;

                if (indexPointer.Filesize <= 0)
                {
                    throw new FileNotFoundException($"{cacheIndex}/{fileId} has no size, which means it is not stored in the cache.");
                }

                var chunkId = 0;
                var remaining = indexPointer.Filesize;
                var dataReader = new BinaryReader(this._dataStream);
                var dataPosition = (long)indexPointer.FirstSectorPosition * Sector.Length;

                do
                {
                    dataReader.BaseStream.Position = dataPosition;

                    var sectorBytes = dataReader.ReadBytes(Sector.Length);

                    if (sectorBytes.Length != Sector.Length)
                    {
                        throw new EndOfStreamException($"One of {cacheIndex}/{fileId}'s sectors could not be fully read.");
                    }

                    var sector = new Sector((int)(dataPosition / Sector.Length), cacheIndex, fileId, chunkId++, sectorBytes);

                    var bytesRead = Math.Min(sector.Data.Length, remaining);

                    remaining -= bytesRead;

                    dataPosition = (long)sector.NextSectorPosition * Sector.Length;

                    sectors.Add(sector);
                }
                while (remaining > 0);
            }

            return sectors;
        }

        /// <summary>
        /// If available, overwrites the space allocated to the previous file first to save space.
        /// </summary>
        /// <param name="cacheIndex"></param>
        /// <param name="fileId"></param>
        /// <param name="data"></param>
        public void WriteFileData(CacheIndex cacheIndex, int fileId, byte[] data)
        {
            if (this.ReadOnly)
            {
                throw new InvalidOperationException("Can't write data in readonly mode.");
            }

            lock (this._ioLock)
            {
                // Obtain possibly existing sector positions to overwrite
                int[] existingSectorPositions;
                try
                {
                    existingSectorPositions = this.ReadSectors(cacheIndex, fileId)
                        .Select(sector => sector.Position)
                        .ToArray();
                }
                catch (System.Exception ex) when (ex is FileNotFoundException)
                {
                    // Assume there are no existing sectors when the method fails
                    existingSectorPositions = new int[0];
                }

                var sectors = Sector.FromData(data, cacheIndex, fileId);

                var dataWriter = new BinaryWriter(this._dataStream);

                foreach (var sector in sectors)
                {
                    // Overwrite existing sector data if available, otherwise append to file
                    sector.Position = sector.ChunkId < existingSectorPositions.Length
                        ? existingSectorPositions[sector.ChunkId]
                        : (int)(dataWriter.BaseStream.Length / Sector.Length);

                    // Set position of next sector
                    sector.NextSectorPosition = sector.ChunkId + 1 < existingSectorPositions.Length
                        ? existingSectorPositions[sector.ChunkId + 1]
                        : (int)(dataWriter.BaseStream.Length / Sector.Length);

                    // Happens if both positions were based on the stream length
                    if (sector.NextSectorPosition == sector.Position)
                    {
                        sector.NextSectorPosition++;
                    }

                    // Add to index
                    if (sector.ChunkId == 0)
                    {
                        var pointer = new IndexPointer
                        {
                            FirstSectorPosition = sector.Position,
                            Filesize = data.Length
                        };

                        // Create index file if it does not exist yet
                        if (!this._indexStreams.ContainsKey(cacheIndex))
                        {
                            this._indexStreams.Add(cacheIndex, System.IO.File.Open(
                                Path.Combine(this.CacheDirectory, "main_file_cache.idx" + (int)cacheIndex),
                                FileMode.OpenOrCreate,
                                FileAccess.ReadWrite));
                        }

                        var indexWriter = new BinaryWriter(this._indexStreams[cacheIndex]);
                        var pointerPosition = fileId * IndexPointer.Length;

                        // Write zeroes up to the desired position of the index stream if it is larger than its size
                        if (indexWriter.BaseStream.Length < pointerPosition)
                        {
                            indexWriter.BaseStream.Position = indexWriter.BaseStream.Length;
                            indexWriter.Write(Enumerable.Repeat((byte)0, (int)(pointerPosition - indexWriter.BaseStream.Length)).ToArray());
                        }
                        else
                        {
                            indexWriter.BaseStream.Position = pointerPosition;
                        }

                        pointer.Encode(indexWriter.BaseStream);
                    }

                    // Write the encoded sector
                    dataWriter.BaseStream.Position = sector.Position * Sector.Length;
                    dataWriter.Write(sector.Encode());
                }
            }
        }

        public void Dispose()
        {
            this._dataStream.Dispose();

            foreach (var indexStream in this._indexStreams.Values)
            {
                indexStream.Dispose();
            }
        }
    }
}
