using Microsoft.Toolkit.Diagnostics;
using Microsoft.Toolkit.Mvvm.Messaging;
using NiconicoToolkit;
using NiconicoToolkit.SnapshotSearch;
using NicoVideoSnapshotSearchAssistanceTools.Models.Domain;
using NicoVideoSnapshotSearchAssistanceTools.Presentation.ViewModels.Messages;
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
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace NicoVideoSnapshotSearchAssistanceTools.Presentation.ViewModels
{
    public sealed class SearchRunningManagementPageViewModel : ViewModelBase
    {
        public SearchRunningManagementPageViewModel(
            IMessenger messenger,
            NiconicoContext niconicoContext
            )
        {
            _messenger = messenger;
            _niconicoContext = niconicoContext;
        }

        private SearchQueryViewModel _SearchQueryVM;
        private readonly IMessenger _messenger;
        private readonly NiconicoContext _niconicoContext;

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

        public override async void OnNavigatedTo(INavigationParameters parameters)
        {
            base.OnNavigatedTo(parameters);

            _navigationDisposable = new CompositeDisposable();
            _cancellationTokenSource = new CancellationTokenSource()
                .AddTo(_navigationDisposable);

            var ct = _cancellationTokenSource.Token;
            try
            {
                if (parameters.TryGetValue("query", out string queryParameters))
                {
                    SearchQueryVM = new SearchQueryViewModel(queryParameters, _messenger);
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
                return;
            }

            Guard.IsNotNull(SearchQueryVM, nameof(SearchQueryVM));

            GoRunnningStateCommand = new[]
            {
                ServerAvairableValidationState.ObserveProperty(x => x.IsValid).Select(x => x == true),
                SearchConditionValidationState.ObserveProperty(x => x.IsValid).Select(x => x == true)
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
                SearchQueryText = SearchQueryVM.SeriaizeParameters();
                SearchQueryReadableText = Uri.UnescapeDataString(SearchQueryText);

                await RestoreSearchResultIfExistAsync(SearchQueryText);
            }
            catch
            {
                _navigationDisposable.Dispose();
                _navigationDisposable = null;

                return;
            }
        }

        public override void OnNavigatedFrom(INavigationParameters parameters)
        {
            _cancellationTokenSource.Cancel();
            _navigationDisposable?.Dispose();
            base.OnNavigatedFrom(parameters);
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

        async Task RestoreSearchResultIfExistAsync(string searchQueryId)
        {
            ServerAvairableValidationState.Reset();
            SearchConditionValidationState.Reset();

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

            var metaItems = await SnapshotResultFileHelper.GetSearchQueryResultMetaItemsAsync(searchQueryId);
            SearchQueryResultMeta meta = metaItems.FirstOrDefault(x => x.SnapshotVersion == CurrentApiVersion);

            IsAlreadyDownloadedSnapshotResult = meta != null 
                ? await SnapshotResultFileHelper.IsExistQueryResultItemsAsync(meta)
                : false
                ;

            try
            {
                SearchConditionValidationState.NowProcessing = true;

                if (meta is not null)
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
                        .Select(x => new SnapshotSearchResultPageItemViewModel(_niconicoContext.VideoSnapshotSearch, meta, SearchQueryVM, x, downloadCount.ElementAtOrDefault(x)) { CsvFile = tempFiles.ElementAtOrDefault(x) })
                        .ToArray();

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
                    var firstResult = await GetSnapshotSearchResultOnCurrentConditionAsync(_niconicoContext.VideoSnapshotSearch, SearchQueryVM, 0, 10);
                    if (!firstResult.IsSuccess)
                    {
                        // 取得失敗
                        // firstResult.Meta.ErrorMessage等をユーザーに提示する
                        // 検索条件編集に戻るか、時間を置いてから再試行するかをユーザーに委ねる
                        throw new Exception(firstResult.Meta.ErrorMessage);
                    }

                    meta = await SnapshotResultFileHelper.CreateSearchQueryResultMetaAsync(searchQueryId, CurrentApiVersion.Value, firstResult.Meta.TotalCount);
                    
                    SearchConditionValidationState.IsValid = true;
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

                ResultPageItems = Enumerable.Range(0, ToPageCount((int)meta.TotalCount))
                        .Select(x => new SnapshotSearchResultPageItemViewModel(_niconicoContext.VideoSnapshotSearch, meta, SearchQueryVM, x))
                        .ToArray();
            }

            Guard.IsNotNull(meta, nameof(meta));

            ResultMeta = meta;
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

        async Task RunDownloadLoopingAsync(CancellationToken ct)
        {
            ProcessedCount = ResultPageItems.TakeWhile(x => x.IsDownloaded).Sum(x => x.DownloadedCount);
            Stopwatch stopwatch = new Stopwatch();
            foreach (var pageItem in ResultPageItems.SkipWhile(x => x.IsDownloaded))
            {
                stopwatch.Start();
                await pageItem.DownloadAsync(ct);
                stopwatch.Stop();

                ProcessedCount += pageItem.DownloadedCount;

                await Task.Delay(stopwatch.Elapsed);

                stopwatch.Reset();
            }

            ProcessedCount = ResultMeta.TotalCount;

            RunningStatus = SearchRunningStatus.Completed;

            await SnapshotResultFileHelper.Temporary.IntegrationTemporarySearchResultItemsAsync(ResultMeta, ct);
        }


        #endregion Running State

        #region Completed State



        #endregion Completed State


        private DelegateCommand _OpenSearchResultCommand;
        public DelegateCommand OpenSearchResultCommand =>
            _OpenSearchResultCommand ?? (_OpenSearchResultCommand = new DelegateCommand(ExecuteOpenSearchResultCommand));

        void ExecuteOpenSearchResultCommand()
        {
            _messenger.Send<NavigationAppCoreFrameRequestMessage>(new(new(nameof(Views.SearchResultPage), ("query", ResultMeta.SearchQueryId), ("version", ResultMeta.SnapshotVersion))));
        }


        private DelegateCommand _SearchQueryTextCopyToClipboardCommand;
        public DelegateCommand SearchQueryTextCopyToClipboardCommand =>
            _SearchQueryTextCopyToClipboardCommand ?? (_SearchQueryTextCopyToClipboardCommand = new DelegateCommand(ExecuteSearchQueryTextCopyToClipboardCommand));

        void ExecuteSearchQueryTextCopyToClipboardCommand()
        {
            var dataPackage = new DataPackage() { RequestedOperation = DataPackageOperation.Copy };
            dataPackage.SetText(SearchConstants.VideoSearchApiUrl + "?" + SearchQueryText);
            Clipboard.SetContent(dataPackage);
        }



        internal static async Task<SnapshotResponse> GetSnapshotSearchResultOnCurrentConditionAsync(VideoSnapshotSearchClient client, SearchQueryViewModel searchQueryViewModel,  int offset, int limit)
        {
            return await client.GetVideoSnapshotSearchAsync(
                searchQueryViewModel.Keyword,
                searchQueryViewModel.Targets,
                searchQueryViewModel.Sort,
                searchQueryViewModel.Context,
                offset,
                limit,
                searchQueryViewModel.Fields,
                searchQueryViewModel.Filters
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

        public SnapshotSearchResultPageItemViewModel(VideoSnapshotSearchClient searchClient, SearchQueryResultMeta meta, SearchQueryViewModel searchQueryVM, int page, int downloadedCount = 0)
        {
            _searchClient = searchClient;
            _meta = meta;
            _searchQueryVM = searchQueryVM;
            Page = page;
        }

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
                var response = await SearchRunningManagementPageViewModel.GetSnapshotSearchResultOnCurrentConditionAsync(_searchClient, _searchQueryVM, Page * 100, 100);
                return (await SnapshotResultFileHelper.Temporary.SaveTemporarySearchResultRangeItemsAsync(_meta, Page, response.Items, ct), response.Items.Length);
            });
            
        }
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
