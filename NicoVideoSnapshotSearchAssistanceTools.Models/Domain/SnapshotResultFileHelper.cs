using CsvHelper;
using CsvHelper.Configuration;
using LiteDB;
using Microsoft.Toolkit.Diagnostics;
using NiconicoToolkit.SnapshotSearch;
using NicoVideoSnapshotSearchAssistanceTools.Models.Domain.SnapshotResult_V0;
using NicoVideoSnapshotSearchAssistanceTools.Models.Infrastructure;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Unity.Attributes;
using Windows.Storage;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace NicoVideoSnapshotSearchAssistanceTools.Models.Domain
{
    public static class SnapshotResultFileHelper
    {
        private const int CurrentCsvVersion = 0;
        private static readonly Type CurrentCsvItemType = typeof(SnapshotSearchItem_V0);

        private static readonly CsvConfiguration _csvConfiguration_ForTemp = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = false,
            MissingFieldFound = null,
            HeaderValidated = null,
            PrepareHeaderForMatch = null,
        };

        private static readonly CsvConfiguration _csvConfiguration_ForFinal = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            MissingFieldFound = null,
            HeaderValidated = null,
        };


        // SearchResultフォルダにSearchQuery.Idの名前でサブフォルダを作成

        private static readonly StorageFolder _baseFolder = ApplicationData.Current.LocalFolder;



        private static string MakeTempSearchResultFileName(SearchQueryResultMeta meta, int start)
        {
            int digit = 0;
            long current = meta.TotalCount;
            while (current != 0)
            {
                current /= 10;
                digit++;
            }

            return $"{start.ToString("D" + digit)}.csv";
        }


        private static string MakeMetaFileName(SearchQueryResultMeta meta)
        {
            return $"{meta.SnapshotVersion:yyyy-MM-dd}.meta.json";
        }

        private static string MakeMetaFileName(DateTimeOffset snapshotVersion)
        {
            return $"{snapshotVersion:yyyy-MM-dd}.meta.json";
        }


        private static string MakeSearchResultFinalFileName(SearchQueryResultMeta meta)
        {
            return $"{meta.SnapshotVersion:yyyy-MM-dd}.csv";
        }

        private static string MakeSearchResultTemporaryFolderName(SearchQueryResultMeta meta)
        {
            return $"{meta.SnapshotVersion:yyyy-MM-dd}";
        }
        private static async Task<StorageFolder> GetSearchQueryFolderAsync(string searchQueryId)
        {
            try
            {
                return await _baseFolder.GetFolderAsync(ToHashString(searchQueryId));
            }
            catch (FileNotFoundException)
            {
                return null;
            }
        }

        static readonly SHA256CryptoServiceProvider hashProvider = new SHA256CryptoServiceProvider();
        public static string GetSHA256HashedString(string value)
            => string.Join("", hashProvider.ComputeHash(Encoding.UTF8.GetBytes(value)).Select(x => $"{x:x2}"));

        readonly static Dictionary<string, string> _hashMap = new Dictionary<string, string>();
        private static string ToHashString(string searchQueryId)
        {
            if (_hashMap.TryGetValue(searchQueryId, out string hash)) { return hash; }

            var hashed = GetSHA256HashedString(searchQueryId);
            _hashMap.Add(searchQueryId, hashed);
            return hashed;
        }

        public static async IAsyncEnumerable<(SearchQueryResultMeta, StorageFile)> GetAllQueryResultMetaAsync([EnumeratorCancellation] CancellationToken ct = default)
        {
            var fileQueryOptions = new Windows.Storage.Search.QueryOptions(Windows.Storage.Search.CommonFileQuery.DefaultQuery, new string[] { ".json" });
            fileQueryOptions.FolderDepth = Windows.Storage.Search.FolderDepth.Deep;
            var fileQuery = _baseFolder.CreateFileQueryWithOptions(fileQueryOptions);
            uint count = await fileQuery.GetItemCountAsync();
            const int oneTimeItemsCount = 10;
            foreach (uint index in Enumerable.Range(0, (int)count / oneTimeItemsCount + 1))
            {
                var files = await fileQuery.GetFilesAsync(index * oneTimeItemsCount, oneTimeItemsCount).AsTask(ct);
                foreach (var file in files)
                {
                    using (var stream = await file.OpenStreamForReadAsync())
                    {
                        yield return (await JsonSerializer.DeserializeAsync<SearchQueryResultMeta>(stream, cancellationToken: ct), file);
                    }
                }
            }
        }


        private static async Task<StorageFolder> CreateSearchQueryFolderIfNotExistAsync(string searchQueryId)
        {
            return await _baseFolder.CreateFolderAsync(ToHashString(searchQueryId), CreationCollisionOption.OpenIfExists);
        }


        public static async Task<bool> IsExistQueryResultItemsAsync(SearchQueryResultMeta meta)
        {
            try
            {
                var folder = await GetSearchQueryFolderAsync(meta.SearchQueryId);
                Guard.IsNotNull(folder, nameof(folder));

                var file = await folder.TryGetItemAsync(MakeSearchResultFinalFileName(meta));
                return file != null;
            }
            catch
            {
                return false;
            }
        }

        public static async Task<List<SearchQueryResultMeta>> GetSearchQueryResultMetaItemsAsync(string searchQueryId)
        {
            var folder = await GetSearchQueryFolderAsync(searchQueryId);
            if (folder == null)
            {
                return new List<SearchQueryResultMeta>();
            }

            var metaFilesQuery = folder.CreateFileQueryWithOptions(new Windows.Storage.Search.QueryOptions() { FileTypeFilter = { ".json" } });
            var files = await metaFilesQuery.GetFilesAsync();

            List<SearchQueryResultMeta> metaItems = new();
            foreach (var file in files)
            {
                using (var stream = await file.OpenStreamForReadAsync())
                {
                     var meta = await JsonSerializer.DeserializeAsync<SearchQueryResultMeta>(stream);
                    metaItems.Add(meta);
                }
            }

            return metaItems;
        }


        static async Task<StorageFile> GetSearchQueryResultMetaFileAsync(string query, DateTimeOffset version)
        {
            try
            {
                var folder = await GetSearchQueryFolderAsync(query);
                if (folder == null)
                {
                    return null;
                }

                return await folder.GetFileAsync(MakeMetaFileName(version));
            }
            catch (FileNotFoundException)
            {
                return null;
            }
            catch (JsonException jsonEx)
            {
                throw;
            }
        }


        public static async Task<bool> DeleteSearchQueryResultMetaFileAsync(string query, DateTimeOffset version)
        {
            if (await GetSearchQueryResultMetaFileAsync(query, version) is not null and var file)
            {
                try
                {
                    await file.DeleteAsync(StorageDeleteOption.PermanentDelete);
                    return true;
                }
                catch { return false; }
            }
            else { return false; }
        }

        public static async Task<SearchQueryResultMeta> GetSearchQueryResultMetaAsync(string query, DateTimeOffset version)
        {
            try
            {
                var folder = await GetSearchQueryFolderAsync(query);
                if (folder == null)
                {
                    return null;
                }

                var file = await folder.GetFileAsync(MakeMetaFileName(version));
                using (var stream = await file.OpenStreamForReadAsync())
                {
                    return await JsonSerializer.DeserializeAsync<SearchQueryResultMeta>(stream);
                }
            }
            catch (FileNotFoundException)
            {
                return null;
            }
            catch (JsonException jsonEx)
            {
                throw;
            }
        }

        public static async Task<SearchQueryResultMeta> CreateSearchQueryResultMetaAsync(string searchQueryId, DateTimeOffset snapshotApiVersion, long totalCount)
        {
            var meta = new SearchQueryResultMeta() 
            {
                CsvFormat = CurrentCsvVersion,
                SearchQueryId = searchQueryId,
                SnapshotVersion = snapshotApiVersion,
                TotalCount = totalCount,
            };

            var folder = await CreateSearchQueryFolderIfNotExistAsync(meta.SearchQueryId);
            if (folder == null)
            {
                throw new InvalidOperationException();
            }

            var file = await folder.CreateFileAsync(MakeMetaFileName(meta), CreationCollisionOption.ReplaceExisting);
            using (var stream = await file.OpenStreamForWriteAsync())
            {
                await JsonSerializer.SerializeAsync(stream, meta);
            }

            return meta;
        }

        static async Task<StorageFile> GetSearchResultItemsFileAsync(SearchQueryResultMeta meta)
        {
            var folder = await GetSearchQueryFolderAsync(meta.SearchQueryId);
            Guard.IsNotNull(folder, nameof(folder));

            var file = await folder.GetFileAsync(MakeSearchResultFinalFileName(meta));
            Guard.IsNotNull(file, nameof(file));

            return file;
        }

        public static async IAsyncEnumerable<SnapshotVideoItem> GetSearchResultItemsAsync(SearchQueryResultMeta meta, [EnumeratorCancellation] CancellationToken ct = default)
        {
            var file = await GetSearchResultItemsFileAsync(meta);
            await foreach (var item in LoadCsvAsync(meta, file, _csvConfiguration_ForFinal, ct))
            {
                yield return item;
            }
        }

        public static async Task<bool> DeleteSearchResultItemsAsync(SearchQueryResultMeta meta)
        {
            try
            {
                var file = await GetSearchResultItemsFileAsync(meta);
                await file.DeleteAsync(StorageDeleteOption.PermanentDelete);
                return true;
            }
            catch { return false; }
        }

        public static IAsyncEnumerable<SnapshotVideoItem> LoadTemporaryCsvAsync(SearchQueryResultMeta meta, StorageFile csvFile, CancellationToken ct = default)
        {
            return LoadCsvAsync(meta, csvFile, _csvConfiguration_ForTemp, ct);
        }

        public static IAsyncEnumerable<SnapshotVideoItem> LoadFinalCsvAsync(SearchQueryResultMeta meta, StorageFile csvFile, CancellationToken ct = default)
        {
            return LoadCsvAsync(meta, csvFile, _csvConfiguration_ForFinal, ct);
        }

        private static async IAsyncEnumerable<SnapshotVideoItem> LoadCsvAsync(SearchQueryResultMeta meta, StorageFile csvFile, CsvConfiguration csvConfiguration, [EnumeratorCancellation] CancellationToken ct = default)
        {
            using (var reader = await csvFile.OpenStreamForReadAsync())
            using (var textReader = new StreamReader(reader))
            using (var csv = new CsvReader(textReader, csvConfiguration))
            {
                if (meta.CsvFormat == 0)
                {
                    await foreach (var item in csv.GetRecordsAsync<SnapshotSearchItem_V0>(ct))
                    {
                        yield return SnapshotResultItemConverter_V0.ConvertBack(item);
                    }
                }
                else
                {
                    throw new InvalidOperationException();
                }
            }
        }



        public static class Temporary
        {
            public static async Task<StorageFile> IntegrationTemporarySearchResultItemsAsync(SearchQueryResultMeta meta, CancellationToken ct = default)
            {
                var folder = await GetSearchQueryFolderAsync(meta.SearchQueryId);
                Guard.IsNotNull(folder, nameof(folder));

                var tempFolder = await folder.GetFolderAsync(MakeSearchResultTemporaryFolderName(meta));
                Guard.IsNotNull(tempFolder, nameof(tempFolder));

                var outputFile = await folder.CreateFileAsync(MakeSearchResultFinalFileName(meta), CreationCollisionOption.ReplaceExisting).AsTask(ct);
                using (var outputStream = await outputFile.OpenStreamForWriteAsync())
                {
                    using (var outputStreamTextWriter = new StreamWriter(outputStream))
                    using (var csv = new CsvWriter(outputStreamTextWriter, _csvConfiguration_ForTemp))
                    {
                        csv.WriteHeader(CurrentCsvItemType);
                        csv.NextRecord();
                    }
                }

                using (var outputStream = await outputFile.OpenStreamForWriteAsync())
                {
                    outputStream.Seek(0, SeekOrigin.End);
                    var allTempFiles = await GetAllTemporarySearchResultFilesAsync(meta, ct);
                    foreach (var file in allTempFiles.OrderBy(x => x.Name))
                    {
                        using (var fileStream = await file.OpenStreamForReadAsync())
                        {
                            fileStream.CopyTo(outputStream);
                        }
                    }
                }

                await tempFolder.DeleteAsync(StorageDeleteOption.PermanentDelete);

                return outputFile;
            }

            public static async Task<StorageFile> SaveTemporarySearchResultRangeItemsAsync(SearchQueryResultMeta meta, int page, IEnumerable<SnapshotVideoItem> items, CancellationToken ct = default)
            {
                var folder = await CreateSearchQueryFolderIfNotExistAsync(meta.SearchQueryId);
                Guard.IsNotNull(folder, nameof(folder));

                var tempFolder = await folder.CreateFolderAsync($"{meta.SnapshotVersion:yyyy-MM-dd}", CreationCollisionOption.OpenIfExists);
                Guard.IsNotNull(tempFolder, nameof(tempFolder));

                var file = await tempFolder.CreateFileAsync(MakeTempSearchResultFileName(meta, page), CreationCollisionOption.ReplaceExisting);
                using (var reader = await file.OpenStreamForWriteAsync())
                using (var textReader = new StreamWriter(reader))
                using (var csv = new CsvWriter(textReader, _csvConfiguration_ForTemp))
                {
                    await csv.WriteRecordsAsync<SnapshotSearchItem_V0>(items.Select(SnapshotResultItemConverter_V0.Convert));
                }

                return file;
            }



            public static async IAsyncEnumerable<SnapshotVideoItem> GetTemporarySearchResultRangeItemsAsync(SearchQueryResultMeta meta, int start, [EnumeratorCancellation] CancellationToken ct = default)
            {
                var folder = await GetSearchQueryFolderAsync(meta.SearchQueryId);
                Guard.IsNotNull(folder, nameof(folder));

                var tempFolder = await folder.CreateFolderAsync(MakeSearchResultTemporaryFolderName(meta));
                Guard.IsNotNull(tempFolder, nameof(tempFolder));

                var file = await folder.GetFileAsync(MakeTempSearchResultFileName(meta, start));
                Guard.IsNotNull(file, nameof(file));

                await foreach (var item in LoadCsvAsync(meta, file, _csvConfiguration_ForTemp, ct))
                {
                    yield return item;
                }
            }


            public static async Task<IReadOnlyList<StorageFile>> GetAllTemporarySearchResultFilesAsync(SearchQueryResultMeta meta, CancellationToken ct = default)
            {
                var folder = await GetSearchQueryFolderAsync(meta.SearchQueryId);
                Guard.IsNotNull(folder, nameof(folder));

                var tempFolder = await folder.CreateFolderAsync(MakeSearchResultTemporaryFolderName(meta), CreationCollisionOption.OpenIfExists);
                Guard.IsNotNull(tempFolder, nameof(tempFolder));

                return await tempFolder.GetFilesAsync();
            }

            public static async Task DeleteTemporarySearchResultFilesAsync(SearchQueryResultMeta meta, CancellationToken ct = default)
            {
                var folder = await GetSearchQueryFolderAsync(meta.SearchQueryId);
                if (folder is null) { return; }

                var tempFolder = await folder.CreateFolderAsync(MakeSearchResultTemporaryFolderName(meta), CreationCollisionOption.OpenIfExists);
                if (tempFolder is null) { return; }

                await tempFolder.DeleteAsync();
            }
        }
    }


    public sealed class SearchQueryResultItem
    {
        public long Id { get; set; }
    }


    public sealed class SearchQueryResultMeta
    {
        public int CsvFormat { get; init; }
        
        public string SearchQueryId { get; init; }

        public DateTimeOffset SnapshotVersion { get; init; }

        public long TotalCount { get; init; }
    }

}
