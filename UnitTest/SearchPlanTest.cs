using Microsoft.Toolkit.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NiconicoToolkit;
using NiconicoToolkit.SnapshotSearch;
using NiconicoToolkit.SnapshotSearch.Filters;
using NiconicoToolkit.SnapshotSearch.JsonFilters;
using NicoVideoSnapshotSearchAssistanceTools.Models.Domain;
using NicoVideoSnapshotSearchAssistanceTools.Models.Domain.BigSearchPlan;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnitTest
{
    [TestClass]
    public sealed class SearchPlanTest
    {
        private NiconicoContext _niconicoContext;
        private SearchPlanFactory _searchPlanFactory;

        [TestInitialize]
        public void Initialize()
        {
            _niconicoContext = new NiconicoToolkit.NiconicoContext("https://github.com/tor4kichi");
            _searchPlanFactory = new SearchPlanFactory(_niconicoContext.VideoSnapshotSearch, nameof(NicoVideoSnapshotSearchAssistanceTools));
        }

        [TestMethod]
        public void GetSeachPlanOrigin()
        {
            DateTimeOffset startTime = DateTimeOffset.Now;
            DateTimeOffset endTime = DateTimeOffset.Now - TimeSpan.FromDays(30);
            CompositeSearchFilter filter = new CompositeSearchFilter()
            {
                Filters = {
                    new CompareSimpleSearchFilter<DateTimeOffset>(SearchFieldType.StartTime, startTime, SimpleFilterComparison.GreaterThan),
                    new CompareSimpleSearchFilter<DateTimeOffset>(SearchFieldType.StartTime, endTime, SimpleFilterComparison.LessThan),
                }
            };
            SearchQueryMock searchQuery = new SearchQueryMock()
            {
                Filters = filter,
                Sort = new SearchSort(SearchFieldType.StartTime, SearchSortOrder.Desc),
                Targets = SearchFieldTypeExtensions.TargetFieldTypes.ToArray(),
                Fields = new[] { SearchFieldType.StartTime },
            };

            var originalPlan = _searchPlanFactory.GetSearchPlanOrigin(new() { }, searchQuery);

            Guard.IsEqualTo(startTime, originalPlan.StartTime, nameof(startTime));
            Guard.IsEqualTo(endTime, originalPlan.EndTime, nameof(endTime));
        }

        [TestMethod]
        public async Task SimpleFilter_SplitSearchPlanAsync()
        {
            DateTimeOffset startTime = NiconicoToolkit.Video.VideoConstants.MostOldestVideoPostedAt;
            DateTimeOffset endTime = DateTimeOffset.Now;
            CompositeSearchFilter filter = new CompositeSearchFilter()
            {
                Filters = {
                    new CompareSimpleSearchFilter<DateTimeOffset>(SearchFieldType.StartTime, startTime, SimpleFilterComparison.GreaterThan),
                    new CompareSimpleSearchFilter<DateTimeOffset>(SearchFieldType.StartTime, endTime, SimpleFilterComparison.LessThan),
                }
            };
            SearchQueryMock searchQuery = new SearchQueryMock()
            {
                Filters = filter,
                Sort = new SearchSort(SearchFieldType.StartTime, SearchSortOrder.Desc),
                Targets = SearchFieldTypeExtensions.TargetFieldTypes.ToArray(),
                Fields = new[] { SearchFieldType.StartTime },
            };


            var result = await _niconicoContext.VideoSnapshotSearch.GetVideoSnapshotSearchAsync(searchQuery.Keyword, searchQuery.Targets, searchQuery.Sort, "", fields: searchQuery.Fields, filter: searchQuery.Filters);
            Guard.IsTrue(result.IsSuccess, nameof(result.IsSuccess));

            var meta = new SearchQueryResultMeta() { TotalCount = result.Meta.TotalCount };

            var originalPlan = _searchPlanFactory.GetSearchPlanOrigin(meta, searchQuery);
            var splited = await _searchPlanFactory.MakeSplitSearchPlanAsync(meta, searchQuery);


        }


        [TestMethod]
        public async Task JsonFilter_SplitSearchPlanAsync()
        {
            DateTimeOffset startTime = NiconicoToolkit.Video.VideoConstants.MostOldestVideoPostedAt;
            DateTimeOffset endTime = DateTimeOffset.Now;

            AndJsonFilter andJsonFilter = new AndJsonFilter(
                new[] { new RangeJsonFilter(SearchFieldType.StartTime, startTime, endTime) }
                );

            SearchQueryMock searchQuery = new SearchQueryMock()
            {
                Filters = andJsonFilter,
                Sort = new SearchSort(SearchFieldType.StartTime, SearchSortOrder.Desc),
                Targets = SearchFieldTypeExtensions.TargetFieldTypes.ToArray(),
                Fields = new[] { SearchFieldType.StartTime },
            };


            var result = await _niconicoContext.VideoSnapshotSearch.GetVideoSnapshotSearchAsync(searchQuery.Keyword, searchQuery.Targets, searchQuery.Sort, "", fields: searchQuery.Fields, filter: searchQuery.Filters);
            Guard.IsTrue(result.IsSuccess, nameof(result.IsSuccess));

            var meta = new SearchQueryResultMeta() { TotalCount = result.Meta.TotalCount };

            var originalPlan = _searchPlanFactory.GetSearchPlanOrigin(meta, searchQuery);
            var splited = await _searchPlanFactory.MakeSplitSearchPlanAsync(meta, searchQuery);


        }
    }

    class SearchQueryMock : ISearchQuery
    {
        public SearchFieldType[] Fields { get; set; }
        public ISearchFilter Filters { get; set; }
        public string Keyword { get; set; }
        public SearchSort Sort { get; set; }
        public SearchFieldType[] Targets { get; set; }
    }
}
