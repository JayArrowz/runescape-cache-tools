using System.Collections.Generic;
using System.Linq;
using Villermen.RuneScapeCacheTools.Cache.Downloader;
using Villermen.RuneScapeCacheTools.Exception;
using Villermen.RuneScapeCacheTools.File;
using Villermen.RuneScapeCacheTools.Model;
using Villermen.RuneScapeCacheTools.Utility;

namespace Villermen.RuneScapeCacheTools.Cache
{
    /// <summary>
    /// Downloads current cache files from RuneScape's content servers. This class is thread-safe to quickly process
    /// files in parallel.
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

        public DownloaderCache() : base(new RuneTek5CacheFileDecoder())
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

        public override byte[] GetFileData(CacheIndex index, int fileId, CacheFileInfo? info)
        {
            if (DownloaderCache.HttpInterfaceIndexes.Contains(index))
            {
                // HTTP downloader requires file info in advance.
                var fileInfo = this.GetFileInfo(index, fileId);
                return this._httpFileDownloader.DownloadFileData(index, fileId, fileInfo);
            }

            return this._tcpFileDownloader.DownloadFileData(index, fileId);
        }

        protected override void PutFileData(CacheIndex index, int fileId, byte[] data, CacheFileInfo? info)
        {
            throw new CacheException("If only what you were trying to do made sense...");
        }

        public override void Dispose()
        {
            this._tcpFileDownloader?.Dispose();
        }
    }
}
