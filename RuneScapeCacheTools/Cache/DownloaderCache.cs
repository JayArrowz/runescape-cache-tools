using System;
using System.Collections.Generic;
using System.Linq;
using Villermen.RuneScapeCacheTools.Cache.Downloader;
using Villermen.RuneScapeCacheTools.File;
using Villermen.RuneScapeCacheTools.Model;

namespace Villermen.RuneScapeCacheTools.Cache
{
    /// <summary>
    /// The <see cref="DownloaderCache" /> provides the means to download current cache files from the runescape
    /// servers. Downloading uses 2 different interfaces depending on the <see cref="CacheIndex" /> of the requested
    /// file: The original TCP based interface and a newer HTTP interface.
    /// </summary>
    public class DownloaderCache : ReferenceTableCache
    {
        private static readonly CacheIndex[] HttpInterfaceIndexes =
        {
            CacheIndex.Music,
        };

        private MasterReferenceTableFile? _cachedMasterReferenceTable;

        private readonly TcpFileDownloader _tcpFileDownloader;

        private readonly HttpFileDownloader _httpFileDownloader;

        public DownloaderCache()
        {
            this._tcpFileDownloader = new TcpFileDownloader();
            this._httpFileDownloader = new HttpFileDownloader();
        }

        public override IEnumerable<CacheIndex> GetAvailableIndexes()
        {
            return this.GetMasterReferenceTable().AvailableReferenceTables;
        }

        public MasterReferenceTableFile GetMasterReferenceTable()
        {
            if (this._cachedMasterReferenceTable != null)
            {
                return this._cachedMasterReferenceTable;
            }

            var masterReferenceTableFile = this.GetFile(CacheIndex.ReferenceTables, (int)CacheIndex.ReferenceTables);
            this._cachedMasterReferenceTable = MasterReferenceTableFile.Decode(masterReferenceTableFile.Data);
            return this._cachedMasterReferenceTable;
        }

        protected override byte[] GetFileData(CacheIndex index, int fileId)
        {
            if (DownloaderCache.HttpInterfaceIndexes.Contains(index))
            {
                // HTTP downloader requires file info in advance.
                var fileInfo = this.GetFileInfo(index, fileId);
                return this._httpFileDownloader.DownloadFileData(index, fileId, fileInfo);
            }

            return this._tcpFileDownloader.DownloadFileData(index, fileId);
        }

        protected override void PutFileData(CacheIndex index, int fileId, byte[] data)
        {
            throw new NotSupportedException("I am a downloader, stop trying to put things in me!");
        }

        public override void Dispose()
        {
            this._tcpFileDownloader?.Dispose();
        }
    }
}
