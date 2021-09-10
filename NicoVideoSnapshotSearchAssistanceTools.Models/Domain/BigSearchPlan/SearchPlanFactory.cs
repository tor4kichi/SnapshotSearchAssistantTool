using Microsoft.Toolkit.Diagnostics;
using NiconicoToolkit.SnapshotSearch;
using NiconicoToolkit.SnapshotSearch.Filters;
using NiconicoToolkit.SnapshotSearch.JsonFilters;
using NiconicoToolkit.Video;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NicoVideoSnapshotSearchAssistanceTools.Models.Domain.BigSearchPlan
{
    public sealed class SearchPlanFactory
    {
        public SearchPlanFactory(
            NiconicoToolkit.SnapshotSearch.VideoSnapshotSearchClient snapshotSearchClient,
            string queryContextParameter
            )
        {
            _snapshotSearchClient = snapshotSearchClient;
            _queryContextParameter = queryContextParameter;
        }

        public static readonly long MaxSearchOffset = 100_000;
        private readonly VideoSnapshotSearchClient _snapshotSearchClient;
        private readonly string _queryContextParameter;

        public async Task<SplitSearchPlanData> MakeSplitSearchPlanAsync(            
            SearchQueryResultMeta meta, 
            ISearchQuery searchQueryVM,
            CancellationToken cancellationToken
            )
        {
            return await Task.Run(async () => 
            {
                var originalPlan = GetSearchPlanOrigin(meta, searchQueryVM);
                var plans = await SplitTimeAsync(searchQueryVM, meta.TotalCount, originalPlan.StartTime, originalPlan.EndTime, originalPlan.IncludeStartTime, originalPlan.IncludeEndTime, cancellationToken);
                return new SplitSearchPlanData() { Plans = plans.ToArray() };
            }, cancellationToken);
        }

        public SplitSearchPlan GetSearchPlanOrigin(
            SearchQueryResultMeta meta,
            ISearchQuery searchQueryVM
            )
        {
            DateTimeOffset? startTime = null;
            DateTimeOffset? endTime = null;
            bool includeStartTime = true;
            bool includeEndTime = true;

            var filter = searchQueryVM.Filters;
            if (filter is CompositeSearchFilter simpleFilters)
            {
                var timefilters = simpleFilters.Filters.Where(x => x is CompareSimpleSearchFilter<DateTimeOffset> timeFilter && timeFilter.FilterType is SearchFieldType.StartTime).Cast<CompareSimpleSearchFilter<DateTimeOffset>>();

                Guard.IsLessThanOrEqualTo(timefilters.Count(), 2, nameof(timefilters));
                foreach (var timeFilter in timefilters)
                {
                    Guard.IsTrue(timeFilter.Condition != SimpleFilterComparison.Equal, nameof(timeFilter.Condition));
                }

                var startTimeFilter = timefilters.FirstOrDefault(x => x.Condition is SimpleFilterComparison.GreaterThan or SimpleFilterComparison.GreaterThanOrEqual);
                var endTimeFilter = timefilters.FirstOrDefault(x => x.Condition is SimpleFilterComparison.LessThan or SimpleFilterComparison.LessThenOrEqual);

                if (startTimeFilter is not null && endTimeFilter is not null)
                {
                    startTime = startTimeFilter.Value;
                    endTime = endTimeFilter.Value;
                }
                else if (startTimeFilter is not null)
                {
                    startTime = startTimeFilter.Value;
                }
                else if (endTimeFilter is not null)
                {
                    endTime = endTimeFilter.Value;
                }
                else
                {
                    throw new InvalidOperationException();
                }
            }
            else if (filter is IJsonSearchFilter jsonSearchFilter)
            {
                // RangeJsonFilterのStartTimeを含むものだけを抽出
                // たった一つだけのRangeJsonFilterを許容し、orやandで複数結合しているケースは非対応とする（それだけで99%のケースに対応できるはず）
                static IEnumerable<RangeJsonFilter> RecursiveExtractStartTimeRangeJsonFilter(IJsonSearchFilter filter)
                {
                    if (filter is AndJsonFilter andFilter)
                    {
                        foreach (var childFilter in andFilter.Filters)
                        {
                            foreach (var rangeFilter in RecursiveExtractStartTimeRangeJsonFilter(childFilter))
                            {
                                yield return rangeFilter;
                            }
                        }
                    }
                    else if (filter is OrJsonFilter orFilter)
                    {
                        foreach (var childFilter in orFilter.Filters)
                        {
                            foreach (var rangeFilter in RecursiveExtractStartTimeRangeJsonFilter(childFilter))
                            {
                                throw new NotSupportedException();
                            }
                        }
                    }
                    else if (filter is RangeJsonFilter rangeFilter && rangeFilter.FieldType == SearchFieldType.StartTime)
                    {
                        yield return rangeFilter;
                    }
                }

                var rangeFilter = RecursiveExtractStartTimeRangeJsonFilter(jsonSearchFilter).Single();
                startTime = (DateTimeOffset?)rangeFilter.From;
                endTime = (DateTimeOffset?)rangeFilter.To;
                includeStartTime = rangeFilter.IncludeLower;
                includeEndTime = rangeFilter.IncludeUpper;
            }

            if (startTime.HasValue is false)
            {
                startTime = VideoConstants.MostOldestVideoPostedAt;
            }

            if (endTime.HasValue is false)
            {
                endTime = meta.SnapshotVersion;
            }

            return new SplitSearchPlan() { RangeItemsCount = meta.TotalCount, EndTime = endTime.Value, StartTime = startTime.Value, IncludeEndTime = includeEndTime, IncludeStartTime = includeStartTime };
        }


        public async Task<List<SplitSearchPlan>> SplitTimeAsync(ISearchQuery searchQuery, long count, DateTimeOffset startTime, DateTimeOffset endTime, bool includeStartTime, bool includeEndTime, CancellationToken cancellationToken)
        {
            int divide = Math.Max(2, (int)Math.Ceiling(count / (double)MaxSearchOffset));
            TimeSpan dividedTime = (endTime - startTime) / divide;
            List<SplitSearchPlan> plans = new();
            foreach (var i in Enumerable.Range(0, divide))
            {
                SplitSearchPlan plan;
                if (i == 0)
                {
                    plan = new SplitSearchPlan() { StartTime = startTime, EndTime = startTime + dividedTime * (i + 1), IncludeStartTime = includeStartTime, IncludeEndTime = true };
                }
                else if (i == divide)
                {
                    plan = new SplitSearchPlan() { StartTime = startTime + dividedTime * i, EndTime = endTime, IncludeStartTime = false, IncludeEndTime = includeEndTime };
                }
                else
                {
                    plan = new SplitSearchPlan() { StartTime = startTime + dividedTime * i, EndTime = startTime + dividedTime * (i + 1), IncludeStartTime = false, IncludeEndTime = true };
                }

                var cloneFilter = CloneSearchFilter(searchQuery.Filters);
                var changedFilter = ChangeStartTimeSearchFilterWithPlan(cloneFilter, plan);

                var result = await GetSnapshotSearchResultOnCurrentConditionAsync(_snapshotSearchClient, searchQuery, _queryContextParameter, 0, 10, changedFilter, cancellationToken);
                
                Guard.IsTrue(result.IsSuccess, nameof(result.IsSuccess));

                if (result.Meta.TotalCount > MaxSearchOffset)
                {
                    var splitTimes = await SplitTimeAsync(searchQuery, result.Meta.TotalCount, plan.StartTime, plan.EndTime, plan.IncludeStartTime, plan.IncludeEndTime, cancellationToken);
                    plans.AddRange(splitTimes);
                }
                else
                {
                    plan.RangeItemsCount = result.Meta.TotalCount;
                    plans.Add(plan);
                }
            }

            return plans;
        }


        public static ISearchFilter CloneSearchFilter(ISearchFilter searchFilter)
        {
            var s = SearchQuarySerializeHelper.SeriaizeParameters("", new SearchFieldType[0], new SearchSort(SearchFieldType.StartTime, SearchSortOrder.Asc), new SearchFieldType[0], searchFilter, "");
            var (_, _, _, _, filter) = SearchQuarySerializeHelper.ParseQueryParameters(s);
            return filter;
        }

        public static ISearchFilter ChangeStartTimeSearchFilterWithPlan(ISearchFilter searchFilter, SplitSearchPlan plan)
        {
            if (searchFilter is CompositeSearchFilter simpleSearchFilter)
            {
                var timeFilters = simpleSearchFilter.Filters.Where(x => x is CompareSimpleSearchFilter<DateTimeOffset> timeFilter && timeFilter.FilterType == SearchFieldType.StartTime);
                foreach (var timeFilter in timeFilters.ToArray())
                {
                    simpleSearchFilter.Filters.Remove(timeFilter);
                }

                simpleSearchFilter.Filters.Add(new CompareSimpleSearchFilter<DateTimeOffset>(SearchFieldType.StartTime, plan.StartTime, plan.IncludeStartTime ? SimpleFilterComparison.GreaterThanOrEqual : SimpleFilterComparison.GreaterThan));
                simpleSearchFilter.Filters.Add(new CompareSimpleSearchFilter<DateTimeOffset>(SearchFieldType.StartTime, plan.EndTime, plan.IncludeEndTime ? SimpleFilterComparison.LessThenOrEqual : SimpleFilterComparison.LessThan));
            }
            else if (searchFilter is IJsonSearchFilter jsonSearchFilter)
            {
                if (jsonSearchFilter is AndJsonFilter andJsonFilter)
                {
                    if (andJsonFilter.Filters.FirstOrDefault(x => x is RangeJsonFilter rangeJsonFilter && rangeJsonFilter.FieldType == SearchFieldType.StartTime) is not null and var timeFilter)
                    {
                        andJsonFilter.Filters.Remove(timeFilter);
                    }

                    andJsonFilter.Filters.Add(new RangeJsonFilter(SearchFieldType.StartTime, plan.StartTime, plan.EndTime, plan.IncludeStartTime, plan.IncludeEndTime));
                }
                else
                {
                    throw new NotSupportedException();
                }
            }
            else
            {
                CompositeSearchFilter compositeSearchFilter = new CompositeSearchFilter();
                compositeSearchFilter.Filters.Add(new CompareSimpleSearchFilter<DateTimeOffset>(SearchFieldType.StartTime, plan.StartTime, plan.IncludeStartTime ? SimpleFilterComparison.GreaterThanOrEqual : SimpleFilterComparison.GreaterThan));
                compositeSearchFilter.Filters.Add(new CompareSimpleSearchFilter<DateTimeOffset>(SearchFieldType.StartTime, plan.EndTime, plan.IncludeEndTime ? SimpleFilterComparison.LessThenOrEqual : SimpleFilterComparison.LessThan));
                searchFilter = compositeSearchFilter;
            }

            return searchFilter;
        }



        public static Task<SnapshotResponse> GetSnapshotSearchResultOnCurrentConditionAsync(VideoSnapshotSearchClient client, ISearchQuery searchQueryVM, string context, int offset, int limit, ISearchFilter overrideFilter, CancellationToken cancellationToken)
        {
            return GetSnapshotSearchResultOnCurrentConditionAsync(client, searchQueryVM.Keyword, searchQueryVM.Fields, searchQueryVM.Targets, searchQueryVM.Sort, overrideFilter ?? searchQueryVM.Filters, context, offset, limit, cancellationToken);
        }

        public static async Task<SnapshotResponse> GetSnapshotSearchResultOnCurrentConditionAsync(VideoSnapshotSearchClient client, string keyword, SearchFieldType[] fields, SearchFieldType[] targets, SearchSort searchSort, ISearchFilter searchFilter, string context, int offset, int limit, CancellationToken cancellationToken)
        {
            return await client.GetVideoSnapshotSearchAsync(
                keyword,
                targets,
                searchSort,
                context,
                offset,
                limit,
                fields,
                searchFilter,
                cancellationToken
                );
        }
    }
}
