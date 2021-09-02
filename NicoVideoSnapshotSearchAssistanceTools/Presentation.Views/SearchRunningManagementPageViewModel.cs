using Microsoft.Toolkit.Diagnostics;
using Microsoft.Toolkit.Mvvm.Messaging;
using NiconicoToolkit;
using NiconicoToolkit.SnapshotSearch;
using NiconicoToolkit.SnapshotSearch.Filters;
using NiconicoToolkit.SnapshotSearch.JsonFilters;
using NiconicoToolkit.Video;
using NicoVideoSnapshotSearchAssistanceTools.Models.Domain;
using NicoVideoSnapshotSearchAssistanceTools.Presentation.ViewModels.Messages;
using NicoVideoSnapshotSearchAssistanceTools.Presentation.Views;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Navigation;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.System;

namespace NicoVideoSnapshotSearchAssistanceTools.Presentation.ViewModels
{
    public sealed class SearchRunningManagementPageViewModel : ViewModelBase
    {

        internal static void ThrowNiconicoWebExceptionIfNotSuccess(SnapshotResponseMeta resMeta)
        {
            if (resMeta.IsSuccess) { return; }
            throw new NiconicoWebException(resMeta.ErrorMessage, resMeta.Status, resMeta.ErrorCode);
        }

        public SearchRunningManagementPageViewModel(
            IMessenger messenger,
            NiconicoContext niconicoContext,
            SearchRunningSettings searchRunningSettings,
            ApplicationInternalSettings applicationInternalSettings
            )
        {
            _messenger = messenger;
            _niconicoContext = niconicoContext;
            _searchRunningSettings = searchRunningSettings;
            _applicationInternalSettings = applicationInternalSettings;
        }

        private readonly IMessenger _messenger;
        private readonly NiconicoContext _niconicoContext;
        private readonly SearchRunningSettings _searchRunningSettings;
        private readonly ApplicationInternalSettings _applicationInternalSettings;
        private SearchQueryViewModel _SearchQueryVM;
        public SearchQueryViewModel SearchQueryVM
        {
            get { return _SearchQueryVM; }
            private set { SetProperty(ref _SearchQueryVM, value); }
        }

        private string _SearchQueryText;
        public string SearchQueryText
        {
            get { return _SearchQueryText; }
            private set { SetProperty(ref _SearchQueryText, value); }
        }

        private string _SearchQueryReadableText;
        public string SearchQueryReadableText
        {
            get { return _SearchQueryReadableText; }
            private set { SetProperty(ref _SearchQueryReadableText, value); }
        }

        private DateTimeOffset? _CurrentApiVersion;
        public DateTimeOffset? CurrentApiVersion
        {
            get { return _CurrentApiVersion; }
            set { SetProperty(ref _CurrentApiVersion, value); }
        }

        private SearchQueryResultMeta _ResultMeta;
        public SearchQueryResultMeta ResultMeta
        {
            get { return _ResultMeta; }
            set { SetProperty(ref _ResultMeta, value); }
        }

        private SnapshotSearchResultPageItemViewModel[] _ResultPageItems;
        public SnapshotSearchResultPageItemViewModel[] ResultPageItems
        {
            get { return _ResultPageItems; }
            private set { SetProperty(ref _ResultPageItems, value); }
        }


        private SearchRunningStatus _RunningStatus;
        public SearchRunningStatus RunningStatus
        {
            get { return _RunningStatus; }
            private set { SetProperty(ref _RunningStatus, value); }
        }

        CompositeDisposable _navigationDisposable;

        CancellationTokenSource _cancellationTokenSource;

        public override async Task OnNavigatedToAsync(INavigationParameters parameters)
        {
            await base.OnNavigatedToAsync(parameters);

            Guard.IsNotNullOrEmpty(_applicationInternalSettings.ContextQueryParameter, nameof(_applicationInternalSettings.ContextQueryParameter));

            _navigationDisposable = new CompositeDisposable();
            _cancellationTokenSource = new CancellationTokenSource()
                .AddTo(_navigationDisposable);

            var ct = _cancellationTokenSource.Token;
            try
            {
                if (parameters.TryGetValue("query", out string queryParameters))
                {
                    SearchQueryVM = new SearchQueryViewModel(queryParameters);

                    _applicationInternalSettings.SaveLastOpenPage(nameof(SearchRunningManagementPage), ("query", queryParameters));
                }
                else
                {
                    throw new InvalidOperationException();
                }
            }
            catch (Exception ex)
            {
                _navigationDisposable.Dispose();
                _navigationDisposable = null;
                throw;
            }

            Guard.IsNotNull(SearchQueryVM, nameof(SearchQueryVM));

            if (_searchRunningSettings.RetryAvairableTime is not null and DateTime retryAvairableTime)
            {
                _RetryAvairableTime = retryAvairableTime;
                if (!CanRetryAndUpdateRetryWaitingTime())
                {
                    ServerErrorMessage = _searchRunningSettings.PrevServerErrorMessage ?? "リトライ待ち";
                    IsShowRetryAvairableTime = true;
                    StartRetryTimer();
                }
            }

            GoRunnningStateCommand = new[]
            {
                ServerAvairableValidationState.ObserveProperty(x => x.IsValid).Select(x => x == true),
                SearchConditionValidationState.ObserveProperty(x => x.IsValid).Select(x => x == true),
                this.ObserveProperty(x => x.RetryWaitingTime).Select(x => x <= TimeSpan.Zero),
                this.ObserveProperty(x => x.ResultMeta).Select(x => x?.TotalCount > 0),
            }
            .CombineLatestValuesAreAllTrue()
            .ToAsyncReactiveCommand()
            .AddTo(_navigationDisposable);

            GoRunnningStateCommand.Subscribe(async () =>
            {
                RunningStatus = SearchRunningStatus.Running;
                try
                {
                    await RunDownloadLoopingAsync(ct);
                }
                catch (OperationCanceledException)
                {

                }
                catch (Exception ex)
                {

                }
            })
                .AddTo(_navigationDisposable);

            try
            {
                SearchQueryText = SearchQueryVM.SeriaizeParameters(_applicationInternalSettings.ContextQueryParameter);
                SearchQueryReadableText = Uri.UnescapeDataString(SearchQueryText);

                await RestoreSearchResultIfExistAsync();
            }
            catch
            {
                _navigationDisposable.Dispose();
                _navigationDisposable = null;

                return;
            }


            if (!IsAlreadyDownloadedSnapshotResult && ResultMeta?.TotalCount != 0)
            {
                if (GoRunnningStateCommand.CanExecute())
                {
                    GoRunnningStateCommand.Execute();
                }
            }
        }

        public override void OnNavigatedFrom(INavigationParameters parameters)
        {
            ClearRetryTimer();
            ClearHandleNiconicoWebException();
            _cancellationTokenSource.Cancel();
            _navigationDisposable?.Dispose();
            base.OnNavigatedFrom(parameters);
        }




        private bool _IsQueryParameterError;
        public bool IsQueryParameterError
        {
            get { return _IsQueryParameterError; }
            set { SetProperty(ref _IsQueryParameterError, value); }
        }

        private string _QueryParameterErrorMessage;
        public string QueryParameterErrorMessage
        {
            get { return _QueryParameterErrorMessage; }
            set { SetProperty(ref _QueryParameterErrorMessage, value); }
        }        


        #region Prepering Status

        public PrepareStateValidationViewModel ServerAvairableValidationState { get; } = new PrepareStateValidationViewModel()
        {
            Title = "スナップショット検索 v2 サーバーの疎通確認",
            ErrorMessage = "サーバー利用不可"
        };
        public PrepareStateValidationViewModel SearchConditionValidationState { get; } = new PrepareStateValidationViewModel()
        {
            Title = "検索結果のテスト取得",
            ErrorMessage = "検索エラー"
        };

        private bool _IsAlreadyDownloadedSnapshotResult;
        public bool IsAlreadyDownloadedSnapshotResult
        {
            get { return _IsAlreadyDownloadedSnapshotResult; }
            set { SetProperty(ref _IsAlreadyDownloadedSnapshotResult, value); }
        }

        async Task RestoreSearchResultIfExistAsync()
        {
            ServerAvairableValidationState.Reset();
            SearchConditionValidationState.Reset();
            ResultPageItems = null;

            ServerAvairableValidationState.NowProcessing = true;

            try
            {
                CurrentApiVersion = await _niconicoContext.VideoSnapshotSearch.GetSnapshotVersionAsync();
                ServerAvairableValidationState.IsValid = true;
            }
            catch (Exception ex)
            {
                ServerAvairableValidationState.IsValid = false;
                ServerAvairableValidationState.ErrorMessage = ex.Message;
                // SnapshotSearchのサーバーが動作していない可能性があるため処理を中断
                return; 
            }
            finally
            {
                ServerAvairableValidationState.NowProcessing = false;
            }

            
            static int ToPageCount(int totalCount)
            {
                return totalCount == 0 ? 0 : totalCount / SearchConstants.MaxSearchLimit + 1;
            }

            var searchQueryWithoutContext = SearchQueryVM.SeriaizeParameters();
            var metaItems = await SnapshotResultFileHelper.GetSearchQueryResultMetaItemsAsync(searchQueryWithoutContext);
            SearchQueryResultMeta meta = metaItems.FirstOrDefault(x => x.SnapshotVersion == CurrentApiVersion);

            IsAlreadyDownloadedSnapshotResult = meta != null 
                ? await SnapshotResultFileHelper.IsExistQueryResultItemsAsync(meta)
                : false
                ;

            try
            {
                SearchConditionValidationState.NowProcessing = true;

                if (meta is not null && !IsAlreadyDownloadedSnapshotResult)
                {
                    if (meta.SnapshotVersion != CurrentApiVersion)
                    {
                        // ダウンロード済みのTempFileが利用できない場合はDL済みを削除
                        await SnapshotResultFileHelper.Temporary.DeleteTemporarySearchResultFilesAsync(meta);
                        meta = null;
                    }

                    var tempFiles = await SnapshotResultFileHelper.Temporary.GetAllTemporarySearchResultFilesAsync(meta);

                    int[] downloadCount = new int[tempFiles.Count];
                    // TempFilesの妥当性チェック
                    int count = 0;
                    try
                    {
                        // NameはTotalCountの桁数と同じ桁数になるようゼロ埋めしてある
                        foreach (var file in tempFiles.OrderBy(x => x.Name))
                        {
                            var index = int.Parse(Path.GetFileNameWithoutExtension(file.Name));
                            Guard.IsEqualTo(count, index, nameof(index));
                            count++;

                            var csvEnumerable = SnapshotResultFileHelper.LoadTemporaryCsvAsync(meta, file);

                            downloadCount[index] = await csvEnumerable.CountAsync();
                        }
                    }
                    catch 
                    {
                        // ダウンロード済みのTempFileが利用できない場合はDL済みを削除
                        await SnapshotResultFileHelper.Temporary.DeleteTemporarySearchResultFilesAsync(meta);
                        meta = null;
                        throw;
                    }

                    ResultPageItems = Enumerable.Range(0, ToPageCount((int)meta.TotalCount))
                        .Select(x => new SnapshotSearchResultPageItemViewModel(_niconicoContext.VideoSnapshotSearch, meta, SearchQueryVM, _applicationInternalSettings.ContextQueryParameter, x, downloadCount.ElementAtOrDefault(x)) { CsvFile = tempFiles.ElementAtOrDefault(x) })
                        .ToArray();

                    SearchConditionValidationState.IsValid = true;
                }
                else if (IsAlreadyDownloadedSnapshotResult)
                {
                    SearchConditionValidationState.IsValid = true;
                }
            }
            catch { }
            finally
            {
                SearchConditionValidationState.NowProcessing = false;
            }
            
            if (meta is null)
            {
                SearchConditionValidationState.NowProcessing = true;
                try
                {
                    var firstResult = await GetSnapshotSearchResultOnCurrentConditionAsync(_niconicoContext.VideoSnapshotSearch, SearchQueryVM, _applicationInternalSettings.ContextQueryParameter, 0, 10);
                    ThrowNiconicoWebExceptionIfNotSuccess(firstResult.Meta);

                    meta = await SnapshotResultFileHelper.CreateSearchQueryResultMetaAsync(searchQueryWithoutContext, CurrentApiVersion.Value, firstResult.Meta.TotalCount);
                    SearchConditionValidationState.IsValid = true;
                }
                catch (NiconicoWebException ex)
                {
                    HandleNiconicoWebException(ex);
                }
                catch (JsonException ex)
                {
                    // SnapshortResponseのDeserializeエラー
                    // Metaのファイル書き込み時のSerializeエラー
                    
                    SearchConditionValidationState.ErrorMessage = ex.Message;
                    SearchConditionValidationState.IsValid = false;
                    return;
                }
                catch (Exception ex)
                {
                    // ファイル書き込みのエラー

                    SearchConditionValidationState.ErrorMessage = ex.Message;
                    SearchConditionValidationState.IsValid = false;
                    return;
                }
                finally
                {
                    SearchConditionValidationState.NowProcessing = false;
                }
            }
            else 
            {
                SearchConditionValidationState.IsValid = true;
            }

            Guard.IsNotNull(meta, nameof(meta));

            ResultMeta = meta;

            if (meta.TotalCount == 0)
            {
                
            }
            else if (ResultPageItems == null)
            {
                if (meta.TotalCount > NiconicoToolkit.SnapshotSearch.SearchConstants.MaxSearchOffset)
                {
                    var plan = MakeSplitSearchPlan(meta, SearchQueryVM);
                }
                else
                {
                    ResultPageItems = Enumerable.Range(0, ToPageCount((int)meta.TotalCount))
                        .Select(x => new SnapshotSearchResultPageItemViewModel(_niconicoContext.VideoSnapshotSearch, meta, SearchQueryVM, _applicationInternalSettings.ContextQueryParameter, x))
                        .ToArray();
                }
            }
        }


        static readonly long MaxSearchOffset = 25_000;

        private async Task<SplitSearchPlanData> MakeSplitSearchPlan(SearchQueryResultMeta meta, ISearchQuery searchQueryVM)
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

            async Task<List<SplitSearchPlan>> SplitTimeAsync(long count, DateTimeOffset startTime, DateTimeOffset endTime, bool includeStartTime, bool includeEndTime)
            {
                int divide = (int)Math.Ceiling(count / (double)MaxSearchOffset);
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

                    var cloneFilter = CloneSearchFilter(searchQueryVM.Filters);
                    var changedFilter = ChangeStartTimeSearchFilterWithPlan(cloneFilter, plan);

                    var result = await GetSnapshotSearchResultOnCurrentConditionAsync(_niconicoContext.VideoSnapshotSearch, searchQueryVM, _applicationInternalSettings.ContextQueryParameter, 0, 10, changedFilter);
                    if (result.IsSuccess is false)
                    {
                        if (result.Meta.TotalCount > MaxSearchOffset)
                        {
                            var splitTimes = await SplitTimeAsync(result.Meta.TotalCount, plan.StartTime, plan.EndTime, plan.IncludeStartTime, plan.IncludeEndTime);
                            plans.AddRange(splitTimes);
                        }
                        else
                        {
                            plans.Add(plan);
                        }
                    }
                }

                return plans;
            }
            
            var plans = await SplitTimeAsync(meta.TotalCount, startTime.Value, endTime.Value, includeStartTime, includeEndTime);
            return new SplitSearchPlanData() { Plans = plans.ToArray() };
        }


        static ISearchFilter CloneSearchFilter(ISearchFilter searchFilter)
        {
            var s = SearchQuarySerializeHelper.SeriaizeParameters("", new SearchFieldType[0], new SearchSort(SearchFieldType.StartTime, SearchSortOrder.Asc), new SearchFieldType[0], searchFilter, "");
            var (_, _, _, _, filter) = SearchQuarySerializeHelper.ParseQueryParameters(s);
            return filter;
        }

        static ISearchFilter ChangeStartTimeSearchFilterWithPlan(ISearchFilter searchFilter, SplitSearchPlan plan)
        {
            if (searchFilter is CompositeSearchFilter simpleSearchFilter)
            {
                var timeFilters = simpleSearchFilter.Filters.Where(x => x is CompareSimpleSearchFilter<DateTimeOffset> timeFilter && timeFilter.FilterType == SearchFieldType.StartTime);
                foreach (var timeFilter in timeFilters)
                {
                    simpleSearchFilter.Filters.Remove(timeFilter);
                }

                simpleSearchFilter.Filters.Add(new CompareSimpleSearchFilter<DateTimeOffset>(SearchFieldType.StartTime, plan.StartTime, plan.IncludeStartTime ? SimpleFilterComparison.GreaterThanOrEqual : SimpleFilterComparison.GreaterThan));
                simpleSearchFilter.Filters.Add(new CompareSimpleSearchFilter<DateTimeOffset>(SearchFieldType.StartTime, plan.EndTime, plan.IncludeEndTime? SimpleFilterComparison.LessThenOrEqual : SimpleFilterComparison.LessThan));
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


        private AsyncReactiveCommand _GoRunnningStateCommand = new AsyncReactiveCommand();
        public AsyncReactiveCommand GoRunnningStateCommand
        {
            get { return _GoRunnningStateCommand; }
            set { SetProperty(ref _GoRunnningStateCommand, value); }
        }

        #endregion Prepering Status



        #region Running State

        private long _ProcessedCount;
        public long ProcessedCount
        {
            get { return _ProcessedCount; }
            set { SetProperty(ref _ProcessedCount, value); }
        }

        private string _ServerErrorMessage;
        public string ServerErrorMessage
        {
            get { return _ServerErrorMessage; }
            set { SetProperty(ref _ServerErrorMessage, value); }
        }

        private bool _IsShowRetryAvairableTime;
        public bool IsShowRetryAvairableTime
        {
            get { return _IsShowRetryAvairableTime; }
            set { SetProperty(ref _IsShowRetryAvairableTime, value); }
        }

        private int _RetryCount;
        public int RetryCount
        {
            get { return _RetryCount; }
            set { SetProperty(ref _RetryCount, value); }
        }

        private TimeSpan _RetryWaitingTime;
        public TimeSpan RetryWaitingTime
        {
            get { return _RetryWaitingTime; }
            set { SetProperty(ref _RetryWaitingTime, value); }
        }

        private DateTime _RetryAvairableTime;

        private DispatcherQueueTimer _retryTimer;
        private DispatcherQueueTimer RetryTimer
        {
            get
            {
                if (_retryTimer != null) { return _retryTimer; }

                _retryTimer = DispatcherQueue.GetForCurrentThread().CreateTimer();
                _retryTimer.Interval = TimeSpan.FromSeconds(1);
                _retryTimer.IsRepeating = true;
                _retryTimer.Tick += (timer, state) => 
                {
                    if (CanRetryAndUpdateRetryWaitingTime())
                    {
                        DownloadRetryCommand.Execute();                        
                    }
                };
                return _retryTimer;
            }
        }

        private bool CanRetryAndUpdateRetryWaitingTime()
        {
            RetryWaitingTime = _RetryAvairableTime - DateTime.Now;
            return _RetryAvairableTime <= DateTime.Now;
        }

        private void StartRetryTimer()
        {
            RetryTimer.Start();
        }

        private void ClearRetryTimer()
        {
            if (_retryTimer == null) { return; }

            _retryTimer.Stop();
            _retryTimer = null;

            IsShowRetryAvairableTime = false;

            _searchRunningSettings.RetryAvairableTime = null;
        }


        async Task RunDownloadLoopingAsync(CancellationToken ct)
        {
            IsShowRetryAvairableTime = false;

            ProcessedCount = ResultPageItems.TakeWhile(x => x.IsDownloaded).Sum(x => x.DownloadedCount);
            Stopwatch stopwatch = new Stopwatch();

            try
            {
#if DEBUG
                //throw new NiconicoWebException("検索条件エラーのテスト", 400, "just testing");
                //throw new NiconicoWebException("サーバーインターナルエラーのテスト", 500, "just testing");
                //throw new NiconicoWebException("サーバー高負荷orメンテナンスのテスト", 503, "just testing");
                //throw new NiconicoWebException("想定外のサーバエラーのテスト", 504, "just testing");
#endif
                foreach (var pageItem in ResultPageItems.SkipWhile(x => x.IsDownloaded))
                {
                    await Task.Delay(stopwatch.Elapsed);
                    stopwatch.Reset();
                    stopwatch.Start();
                    await pageItem.DownloadAsync(ct);
                    stopwatch.Stop();
                    ProcessedCount += pageItem.DownloadedCount;                    
                }
            }
            catch (NiconicoWebException ex)
            {
                HandleNiconicoWebException(ex);
                return;
            }
            finally
            {
                stopwatch.Stop();
            }

            ProcessedCount = ResultMeta.TotalCount;

            RunningStatus = SearchRunningStatus.Completed;

            var token = _cancellationTokenSource?.Token ?? default;
            SearchResultIntegrationFailedMessage = null;
            try
            {
                await SnapshotResultFileHelper.Temporary.IntegrationTemporarySearchResultItemsAsync(ResultMeta, token);
            }
            catch (Exception ex)
            {
                SearchResultIntegrationFailedMessage = ex.Message;
                return;
            }

            OpenSearchResultCommand.Execute();
        }

        void ClearHandleNiconicoWebException()
        {
            IsQueryParameterError = false;
            QueryParameterErrorMessage = null;
            ServerErrorMessage = null;
            _searchRunningSettings.PrevServerErrorMessage = null;
            IsShowRetryAvairableTime = false;
        }

        void HandleNiconicoWebException(NiconicoWebException ex)
        {
            ClearHandleNiconicoWebException();

            if (ex.StatusCode == 400)
            {
                IsQueryParameterError = true;
                QueryParameterErrorMessage = $"Status:{ex.StatusCode} ErrorCode:{ex.ErrorCode} Message:{ex.Message}";
                return;
            }
            else if (ex.StatusCode == 500)
            {
                ServerErrorMessage = $"Status:{ex.StatusCode} ErrorCode:{ex.ErrorCode} Message:{ex.Message}";
                _RetryAvairableTime = DateTime.Now + TimeSpan.FromMinutes(5);
                _searchRunningSettings.PrevServerErrorMessage = ServerErrorMessage;
                IsShowRetryAvairableTime = true;
                StartRetryTimer();
                return;
            }
            else if (ex.StatusCode == 503)
            {
                ServerErrorMessage = $"Status:{ex.StatusCode} ErrorCode:{ex.ErrorCode} Message:{ex.Message}";
                _RetryAvairableTime = DateTime.Now + TimeSpan.FromMinutes(5);
                _searchRunningSettings.PrevServerErrorMessage = ServerErrorMessage;
                IsShowRetryAvairableTime = true;
                StartRetryTimer();
                return;
            }
            else
            {
                ServerErrorMessage = $"Status:{ex.StatusCode} ErrorCode:{ex.ErrorCode} Message:{ex.Message}";
                throw ex;
            }
        }


        private DelegateCommand _DownloadRetryCommand;
        public DelegateCommand DownloadRetryCommand =>
            _DownloadRetryCommand ?? (_DownloadRetryCommand = new DelegateCommand(ExecuteDownloadRetryCommand));

        void ExecuteDownloadRetryCommand()
        {
            ClearRetryTimer();
            RetryCount++;
            _ = RunDownloadLoopingAsync(_cancellationTokenSource.Token);
        }

        #endregion Running State

        #region Completed State


        private string _SearchResultIntegrationFailedMessage;
        public string SearchResultIntegrationFailedMessage
        {
            get { return _SearchResultIntegrationFailedMessage; }
            set { SetProperty(ref _SearchResultIntegrationFailedMessage, value); }
        }


        private DelegateCommand _OpenSearchResultCommand;
        public DelegateCommand OpenSearchResultCommand =>
            _OpenSearchResultCommand ?? (_OpenSearchResultCommand = new DelegateCommand(ExecuteOpenSearchResultCommand));

        async void ExecuteOpenSearchResultCommand()
        {
            await _messenger.Send<NavigationAppCoreFrameRequestMessage>(new(new(nameof(Views.SearchResultPage), ("query", ResultMeta.SearchQueryId), ("version", ResultMeta.SnapshotVersion))));
        }


        #endregion Completed State




        private DelegateCommand _SearchQueryTextCopyToClipboardCommand;
        public DelegateCommand SearchQueryTextCopyToClipboardCommand =>
            _SearchQueryTextCopyToClipboardCommand ?? (_SearchQueryTextCopyToClipboardCommand = new DelegateCommand(ExecuteSearchQueryTextCopyToClipboardCommand));

        void ExecuteSearchQueryTextCopyToClipboardCommand()
        {
            var dataPackage = new DataPackage() { RequestedOperation = DataPackageOperation.Copy };
            dataPackage.SetText(SearchConstants.VideoSearchApiUrl + "?" + SearchQueryText);
            Clipboard.SetContent(dataPackage);
        }



        private DelegateCommand _OpenQueryEditPageCommand;
        public DelegateCommand OpenQueryEditPageCommand =>
            _OpenQueryEditPageCommand ?? (_OpenQueryEditPageCommand = new DelegateCommand(ExecuteOpenQueryEditPageCommand));

        async void ExecuteOpenQueryEditPageCommand()
        {
            await _messenger.Send<NavigationAppCoreFrameRequestMessage>(new(new(nameof(Views.QueryEditPage))));
        }

        internal static Task<SnapshotResponse> GetSnapshotSearchResultOnCurrentConditionAsync(VideoSnapshotSearchClient client, SearchQueryViewModel searchQueryVM, string context, int offset, int limit, ISearchFilter overrideFilter = null)
        {
            return GetSnapshotSearchResultOnCurrentConditionAsync(client, searchQueryVM.Keyword, searchQueryVM.Targets, searchQueryVM.Fields, searchQueryVM.Sort, overrideFilter ?? searchQueryVM.Filters, context, offset, limit);
        }

        internal static async Task<SnapshotResponse> GetSnapshotSearchResultOnCurrentConditionAsync(VideoSnapshotSearchClient client, string keyword, SearchFieldType[] fields, SearchFieldType[] targets, SearchSort searchSort, ISearchFilter searchFilter, string context, int offset, int limit)
        {
            return await client.GetVideoSnapshotSearchAsync(
                keyword,
                targets,
                searchSort,
                context,
                offset,
                limit,
                fields,
                searchFilter
                );
        }

        #region UI Debug

        private DelegateCommand<string> _StatusChangeTestCommand;
        public DelegateCommand<string> StatusChangeTestCommand =>
            _StatusChangeTestCommand ?? (_StatusChangeTestCommand = new DelegateCommand<string>(ExecuteStatusChangeTestCommand));

        void ExecuteStatusChangeTestCommand(string parameter)
        {
            if (Enum.TryParse(parameter, out SearchRunningStatus status))
            {
                RunningStatus = status;
            }
        }


        private DelegateCommand<PrepareStateValidationViewModel> _ToggleStateTestCommand;
        public DelegateCommand<PrepareStateValidationViewModel> ToggleStateTestCommand =>
            _ToggleStateTestCommand ?? (_ToggleStateTestCommand = new DelegateCommand<PrepareStateValidationViewModel>(ExecuteToggleStateTestCommand));

        void ExecuteToggleStateTestCommand(PrepareStateValidationViewModel parameter)
        {
            if (parameter.IsValid == null)
            {
                parameter.IsValid = true;
            }
            else if (parameter.IsValid == true)
            {
                parameter.IsValid = false;
            }
            else
            {
                parameter.IsValid = null;
            }

            parameter.NowProcessing = !parameter.NowProcessing;
        }


        private DelegateCommand _SetRetryTestCommand;
        public DelegateCommand SetRetryTestCommand =>
            _SetRetryTestCommand ?? (_SetRetryTestCommand = new DelegateCommand(ExecuteSetRetryTestCommand));

        void ExecuteSetRetryTestCommand()
        {
            IsShowRetryAvairableTime = true;
            ServerErrorMessage = "リトライのテスト中";
            RetryCount++;
            _RetryAvairableTime = DateTime.Now + TimeSpan.FromMinutes(30);
            StartRetryTimer();
        }


        #endregion UI Debug
    }

    public enum SearchRunningStatus
    {
        Preparing,
        Running,
        Completed,
    }




    public sealed class SnapshotSearchResultPageItemViewModel : BindableBase
    {
        private readonly VideoSnapshotSearchClient _searchClient;
        private readonly SearchQueryResultMeta _meta;
        private readonly SearchQueryViewModel _searchQueryVM;
        private readonly string _context;

        public SnapshotSearchResultPageItemViewModel(VideoSnapshotSearchClient searchClient, SearchQueryResultMeta meta, SearchQueryViewModel searchQueryVM, string context, int page, int downloadedCount = 0)
        {
            _searchClient = searchClient;
            _meta = meta;
            _searchQueryVM = searchQueryVM;
            _context = context;
            Page = page;
        }

        public SnapshotSearchResultPageItemViewModel(VideoSnapshotSearchClient searchClient, SearchQueryResultMeta meta, SearchQueryViewModel searchQueryVM, string context, SplitSearchPageItemToken splitedSearchFilter, int page, int downloadedCount = 0)
        {
            _searchClient = searchClient;
            _meta = meta;
            _searchQueryVM = searchQueryVM;
            _context = context;
            SplitedSearchFilter = splitedSearchFilter;
            Page = page;
        }

        public SplitSearchPageItemToken SplitedSearchFilter { get; }

        public int Page { get; }

        private bool _IsDownloaded;
        public bool IsDownloaded
        {
            get { return _IsDownloaded; }
            private set { SetProperty(ref _IsDownloaded, value); }
        }

        private StorageFile _CsvFile;
        public StorageFile CsvFile
        {
            get => _CsvFile;
            set
            {
                if (SetProperty(ref _CsvFile, value))
                {
                    IsDownloaded = _CsvFile != null;
                }
            }
        }

        private int _DownloadedCount;
        public int DownloadedCount
        {
            get { return _DownloadedCount; }
            set { SetProperty(ref _DownloadedCount, value); }
        }

        public async Task DownloadAsync(CancellationToken ct)
        {
            (CsvFile, DownloadedCount) = await Task.Run(async () => 
            {
                var response = await SearchRunningManagementPageViewModel.GetSnapshotSearchResultOnCurrentConditionAsync(_searchClient, _searchQueryVM, _context, Page * 100, 100, SplitedSearchFilter.SplitedSearchFilter);
                SearchRunningManagementPageViewModel.ThrowNiconicoWebExceptionIfNotSuccess(response.Meta);
                return (await SnapshotResultFileHelper.Temporary.SaveTemporarySearchResultRangeItemsAsync(_meta, Page + (SplitedSearchFilter?.PageCountOffset ?? 0), response.Items, ct), response.Items.Length);
            });
            
        }
        
    }

    public class SplitSearchPageItemToken
    {
        public ISearchFilter SplitedSearchFilter { get; init; }

        public int PageCountOffset { get; init; }
    }


    public sealed class NiconicoWebException : Exception
    {
        public NiconicoWebException(string message, long statusCode, string errorCode) : base(message)
        {
            StatusCode = statusCode;
            ErrorCode = errorCode;
        }

        public long StatusCode { get; }
        public string ErrorCode { get; }
    }

    public sealed class PrepareStateValidationViewModel : BindableBase
    {
        public string Title { get; set; }

        private bool? _isValid;
        public bool? IsValid
        {
            get { return _isValid; }
            set { SetProperty(ref _isValid, value); }
        }

        private bool _nowProcessing;
        public bool NowProcessing
        {
            get { return _nowProcessing; }
            set { SetProperty(ref _nowProcessing, value); }
        }

        private string _errorMessage;
        public string ErrorMessage
        {
            get { return _errorMessage; }
            set { SetProperty(ref _errorMessage, value); }
        }

        public void Reset()
        {
            IsValid = null;
            NowProcessing = false;
        }
    }
}
