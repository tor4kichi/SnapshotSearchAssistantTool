using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Toolkit.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NiconicoToolkit.Channels;
using NiconicoToolkit.SnapshotSearch;
using NiconicoToolkit.Video;
using NicoVideoSnapshotSearchAssistanceTools.Models.Domain;
using Windows.Storage;

namespace UnitTest
{
    [TestClass]
    public class UnitTest1
    {

        [TestCleanup]
        public async Task Cleanup()
        {
            var files = await ApplicationData.Current.LocalFolder.GetFilesAsync();
            foreach (var file in files)
            {
                await file.DeleteAsync();
            }

            var folders = await ApplicationData.Current.LocalFolder.GetFoldersAsync();
            foreach (var folder in folders)
            {
                await folder.DeleteAsync();
            }
        }

        [TestMethod]
        public async Task SnapshotResultMetaSaveAndLoadTest()
        {
            var meta = new SearchQueryResultMeta() { Fields = NiconicoToolkit.SnapshotSearch.SearchFieldTypeExtensions.FieldTypes.ToArray(), SearchQueryId = Guid.NewGuid(), SnapshotVersion = DateTime.Now, TotalCount = 100 };
            await SnapshotResultFileHelper.SaveSearchQueryResultMetaAsync(meta);

            var loadmetas = await SnapshotResultFileHelper.GetSearchQueryResultMetaItemsAsync(meta.SearchQueryId);
            var loadMeta = loadmetas[0];
            Guard.IsEqualTo(meta.SnapshotVersion, loadMeta.SnapshotVersion, nameof(loadMeta.SnapshotVersion));
            Guard.IsEqualTo(meta.TotalCount, loadMeta.TotalCount, nameof(loadMeta.TotalCount));
            Guard.IsEqualTo(meta.CsvFormat, loadMeta.CsvFormat, nameof(loadMeta.CsvFormat));
            for (int i = 0; i < meta.Fields.Length; i++)
            {
                Guard.IsTrue(meta.Fields[i] == loadMeta.Fields[i], nameof(loadMeta.Fields));
            }
        }


        [TestMethod]
        public async Task SnapshotResultSaveTemporaryAndIntegrationTest()
        {
            var meta = new SearchQueryResultMeta() { Fields = NiconicoToolkit.SnapshotSearch.SearchFieldTypeExtensions.FieldTypes.ToArray(), SearchQueryId = Guid.NewGuid(), SnapshotVersion = DateTime.Now, TotalCount = 100 };

            int oneTimeItemsCount = 100;
            foreach (var i in Enumerable.Range(0, 5))
            {
                var items = MakeSnapshotResultItems(oneTimeItemsCount, meta.Fields);
                await SnapshotResultFileHelper.Temporary.SaveTemporarySearchResultRangeItemsAsync(meta, oneTimeItemsCount * i, items.ToList());
            }

            await SnapshotResultFileHelper.Temporary.IntegrationTemporarySearchResultItemsAsync(meta);

            var resultItems = SnapshotResultFileHelper.GetSearchResultItemsAsync(meta);

            var count = await resultItems.CountAsync();
            Guard.IsEqualTo(500, count, nameof(count));


        }

        [TestMethod]
        public async Task SnapshotResultEmptyFieldsSaveTemporaryAndIntegrationTest()
        {
            var meta = new SearchQueryResultMeta() 
            { 
                Fields = new SearchFieldType[0], SearchQueryId = Guid.NewGuid(), SnapshotVersion = DateTime.Now, TotalCount = 100 
            };

            int oneTimeItemsCount = 100;
            foreach (var i in Enumerable.Range(0, 5))
            {
                var items = MakeSnapshotResultItems(oneTimeItemsCount, meta.Fields);
                await SnapshotResultFileHelper.Temporary.SaveTemporarySearchResultRangeItemsAsync(meta, oneTimeItemsCount * i, items.ToList());
            }

            await SnapshotResultFileHelper.Temporary.IntegrationTemporarySearchResultItemsAsync(meta);

            var resultItems = SnapshotResultFileHelper.GetSearchResultItemsAsync(meta);

            var count = await resultItems.CountAsync();
            Guard.IsEqualTo(500, count, nameof(count));


        }

        private IEnumerable<SnapshotVideoItem> MakeSnapshotResultItems(int count, SearchFieldType[] fields)
        {
            var fieldsHashSet = fields.ToHashSet();
            return Enumerable.Range(0, count).Select(x => 
            {
                var item = new SnapshotVideoItem()
                {
                    ContentId = fieldsHashSet.Contains(SearchFieldType.ContentId) ? new VideoId($"sm{x}") : null,
                    ChannelId = fieldsHashSet.Contains(SearchFieldType.ChannelId) ? new ChannelId($"ch{x}") : null,
                    CategoryTags = fieldsHashSet.Contains(SearchFieldType.CategoryTags) ? $"sm{x}" : null,
                    CommentCounter = fieldsHashSet.Contains(SearchFieldType.CommentCounter) ? 12345 : null,
                    Description = fieldsHashSet.Contains(SearchFieldType.Description) ? $"sm{x}" : null,
                    Genre = fieldsHashSet.Contains(SearchFieldType.Genre) ? $"sm{x}" : null,
                    LastCommentTime = fieldsHashSet.Contains(SearchFieldType.LastCommentTime) ? DateTime.Now : null,
                    LastResBody = fieldsHashSet.Contains(SearchFieldType.LastResBody) ? $"aaaa" : null,
                    LengthSeconds = fieldsHashSet.Contains(SearchFieldType.LengthSeconds) ? 120 : null,
                    LikeCounter = fieldsHashSet.Contains(SearchFieldType.LikeCounter) ? 12345 : null,
                    MylistCounter = fieldsHashSet.Contains(SearchFieldType.MylistCounter) ? 12345 : null,
                    UserId = fieldsHashSet.Contains(SearchFieldType.UserId) ? 12345 : null,
                    StartTime = fieldsHashSet.Contains(SearchFieldType.StartTime) ? DateTime.Now: null,
                    Tags = fieldsHashSet.Contains(SearchFieldType.Tags) ? "AAAA BBBB CCCC" : null,
                    ThumbnailUrl = fieldsHashSet.Contains(SearchFieldType.ThumbnailUrl) ? new Uri("https://site.nicovideo.jp/search-api-docs/snapshot") : null,
                    Title = fieldsHashSet.Contains(SearchFieldType.Title) ? $"タイトル {x}" : null,
                    ViewCounter = fieldsHashSet.Contains(SearchFieldType.ViewCounter) ? 12345 : null,
                };
                return item;
            });
        }
    }
}
