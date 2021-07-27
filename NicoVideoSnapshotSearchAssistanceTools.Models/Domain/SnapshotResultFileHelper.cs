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
using System.Text;
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


        private static string MakeSearchResultFinalFileName(SearchQueryResultMeta meta)
        {
            return $"{meta.SnapshotVersion:yyyy-MM-dd}.csv";
        }

        private static string MakeSearchResultTemporaryFolderName(SearchQueryResultMeta meta)
        {
            return $"{meta.SnapshotVersion:yyyy-MM-dd}";
        }
        private static async Task<StorageFolder> GetSearchQueryFolderAsync(Guid id)
        {
            return await _baseFolder.GetFolderAsync(id.ToString());
        }

        private static async Task<StorageFolder> CreateSearchQueryFolderIfNotExistAsync(Guid id)
        {
            return await _baseFolder.CreateFolderAsync(id.ToString(), CreationCollisionOption.OpenIfExists);
        }

        public static async Task<List<SearchQueryResultMeta>> GetSearchQueryResultMetaItems(Guid searchQueryId)
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


        public static async Task SaveSearchQueryResultMetaAsync(SearchQueryResultMeta meta)
        {
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
        }

        public static async IAsyncEnumerable<SnapshotVideoItem> GetSearchResultItemsAsync(SearchQueryResultMeta meta, [EnumeratorCancellation] CancellationToken ct = default)
        {
            var folder = await GetSearchQueryFolderAsync(meta.SearchQueryId);
            Guard.IsNotNull(folder, nameof(folder));

            var file = await folder.GetFileAsync(MakeSearchResultFinalFileName(meta));
            Guard.IsNotNull(file, nameof(file));
#if DEBUG
            Debug.WriteLine(await FileIO.ReadTextAsync(file));
#endif

            await foreach (var item in LoadCsvAsync(meta, file, _csvConfiguration_ForFinal, ct))
            {
                yield return item;
            }
        }

        public static async IAsyncEnumerable<SnapshotVideoItem> LoadCsvAsync(SearchQueryResultMeta meta, StorageFile csvFile, CsvConfiguration csvConfiguration, [EnumeratorCancellation] CancellationToken ct = default)
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
            public static async Task IntegrationTemporarySearchResultItemsAsync(SearchQueryResultMeta meta, CancellationToken ct = default)
            {
                var folder = await GetSearchQueryFolderAsync(meta.SearchQueryId);
                Guard.IsNotNull(folder, nameof(folder));

                var tempFolder = await folder.GetFolderAsync(MakeSearchResultTemporaryFolderName(meta));
                Guard.IsNotNull(tempFolder, nameof(tempFolder));

                var outputFile = await folder.CreateFileAsync(MakeSearchResultFinalFileName(meta)).AsTask(ct);
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
            }

            public static async Task SaveTemporarySearchResultRangeItemsAsync(SearchQueryResultMeta meta, int start, List<SnapshotVideoItem> items, CancellationToken ct = default)
            {
                var folder = await CreateSearchQueryFolderIfNotExistAsync(meta.SearchQueryId);
                Guard.IsNotNull(folder, nameof(folder));

                var tempFolder = await folder.CreateFolderAsync($"{meta.SnapshotVersion:yyyy-MM-dd}", CreationCollisionOption.OpenIfExists);
                Guard.IsNotNull(tempFolder, nameof(tempFolder));

                var file = await tempFolder.CreateFileAsync(MakeTempSearchResultFileName(meta, start));
                using (var reader = await file.OpenStreamForWriteAsync())
                using (var textReader = new StreamWriter(reader))
                using (var csv = new CsvWriter(textReader, _csvConfiguration_ForTemp))
                {
                    await csv.WriteRecordsAsync<SnapshotSearchItem_V0>(items.Select(SnapshotResultItemConverter_V0.Convert));
                }
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
        }
    }


    public sealed class SearchQueryResultItem
    {
        public long Id { get; set; }
    }


    public sealed class SearchQueryResultMeta
    {
        public int CsvFormat { get; init; }
        public Guid SearchQueryId { get; init; }

        public DateTime SnapshotVersion { get; init; }

        public SearchFieldType[] Fields { get; init; }

        public long TotalCount { get; init; }
    }

}
