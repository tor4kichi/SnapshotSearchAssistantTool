using Microsoft.Toolkit.Diagnostics;
using Microsoft.Toolkit.Mvvm.Messaging;
using Microsoft.Toolkit.Uwp.UI;
using NiconicoToolkit.Channels;
using NiconicoToolkit.SnapshotSearch;
using NiconicoToolkit.User;
using NiconicoToolkit.Video;
using NicoVideoSnapshotSearchAssistanceTools.Models.Domain;
using NicoVideoSnapshotSearchAssistanceTools.Presentation.Views;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Navigation;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NicoVideoSnapshotSearchAssistanceTools.Presentation.ViewModels
{


    public sealed class SearchResultPageViewModel : ViewModelBase, IConfirmNavigationAsync
    {
        public SearchResultPageViewModel(
            IMessenger messenger,
            ApplicationInternalSettings applicationInternalSettings,
            SearchResultSettings searchResultSettings
            )
        {
            _messenger = messenger;
            _applicationInternalSettings = applicationInternalSettings;
            _searchResultSettings = searchResultSettings;
        }

        private readonly IMessenger _messenger;
        private readonly ApplicationInternalSettings _applicationInternalSettings;
        private readonly SearchResultSettings _searchResultSettings;
        private SearchQueryViewModel _SearchQueryVM;
        public SearchQueryViewModel SearchQueryVM
        {
            get { return _SearchQueryVM; }
            private set { SetProperty(ref _SearchQueryVM, value); }
        }

        private SearchQueryResultMeta _resultMeta;
        public SearchQueryResultMeta ResultMeta
        {
            get { return _resultMeta; }
            private set { SetProperty(ref _resultMeta, value); }
        }

        public AdvancedCollectionView ItemsView { get; } = new AdvancedCollectionView();


        private ReactivePropertySlim<double> _ViewCounterWeightingFactor;
        public ReactivePropertySlim<double> ViewCounterWeightingFactor
        {
            get { return _ViewCounterWeightingFactor; }
            private set { SetProperty(ref _ViewCounterWeightingFactor, value); }
        }

        private ReactivePropertySlim<double> _MylistCounterWeightingFactor;
        public ReactivePropertySlim<double> MylistCounterWeightingFactor
        {
            get { return _MylistCounterWeightingFactor; }
            private set { SetProperty(ref _MylistCounterWeightingFactor, value); }
        }

        private ReactivePropertySlim<double> _CommentCounterWeightingFactor;
        public ReactivePropertySlim<double> CommentCounterWeightingFactor
        {
            get { return _CommentCounterWeightingFactor; }
            private set { SetProperty(ref _CommentCounterWeightingFactor, value); }
        }

        private ReactivePropertySlim<double> _LikeCounterWeightingFactor;
        public ReactivePropertySlim<double> LikeCounterWeightingFactor
        {
            get { return _LikeCounterWeightingFactor; }
            private set { SetProperty(ref _LikeCounterWeightingFactor, value); }
        }



        CancellationTokenSource _navigationCts;
        CompositeDisposable _navigationDisposable;

        private bool _isFailed;
        public bool IsFailed
        {
            get { return _isFailed; }
            private set { SetProperty(ref _isFailed, value); }
        }

        private string _FailedErrorMessage;

        public string FailedErrorMessage
        {
            get { return _FailedErrorMessage; }
            private set { SetProperty(ref _FailedErrorMessage, value); }
        }

        public const string CustomFieldTypeName_Index = "Index";
        public const string CustomFieldTypeName_Score = "Score";

        public readonly static string[] CustomFieldTypeNames = new[]
        {
            CustomFieldTypeName_Index,
            CustomFieldTypeName_Score
        };

        public Dictionary<string, bool> VisibilityMap { get; } = 
            Enumerable.Concat(
                SearchFieldTypeExtensions.FieldTypes.Select(x => x.ToString()),
                CustomFieldTypeNames
                )
                .ToDictionary(x => x, x => true);

        public override async Task OnNavigatedToAsync(INavigationParameters parameters)
        {
            _navigationDisposable = new CompositeDisposable();
            _navigationCts = new CancellationTokenSource();
            
            await base.OnNavigatedToAsync(parameters);

            try
            {
                var ct = _navigationCts.Token;
                if (!parameters.TryGetValue("query", out string queryParameters))
                {
                    ThrowHelper.ThrowInvalidOperationException(nameof(queryParameters));
                }

                if (!parameters.TryGetValue("version", out DateTimeOffset version))
                {
                    if (parameters.TryGetValue("version", out string versionStr))
                    {
                        version = DateTimeOffset.Parse(versionStr);
                    }
                    else
                    {
                        ThrowHelper.ThrowInvalidOperationException(nameof(version));
                    }
                }
                
                try
                {
                    ResultMeta = await SnapshotResultFileHelper.GetSearchQueryResultMetaAsync(queryParameters, version);
                }
                catch (Exception ex)
                {
                    ThrowHelper.ThrowInvalidOperationException(nameof(ResultMeta), ex);
                }

                _applicationInternalSettings.SaveLastOpenPage(nameof(SearchResultPage), ("query", queryParameters), ("version", version.ToString()));

                Guard.IsNotNull(ResultMeta, nameof(ResultMeta));
                SearchQueryVM = new SearchQueryViewModel(queryParameters, _messenger);

                var fieldHashSet = SearchQueryVM.Fields.Select(x => x.ToString()).ToHashSet();
                foreach (var pair in VisibilityMap.ToArray())
                {
                    if (pair.Key == CustomFieldTypeName_Index)
                    {
                        VisibilityMap[pair.Key] = true;
                    }
                    else if (pair.Key == CustomFieldTypeName_Score)
                    {
                        VisibilityMap[pair.Key] =
                            fieldHashSet.Contains(nameof(SearchFieldType.ViewCounter))
                            || fieldHashSet.Contains(nameof(SearchFieldType.MylistCounter))
                            || fieldHashSet.Contains(nameof(SearchFieldType.CommentCounter))
                            || fieldHashSet.Contains(nameof(SearchFieldType.LikeCounter))
                            ;
                    }
                    else
                    {
                        VisibilityMap[pair.Key] = fieldHashSet.Contains(pair.Key);
                    }
                }

                RaisePropertyChanged(nameof(VisibilityMap));

                ct.ThrowIfCancellationRequested();

                using (ItemsView.DeferRefresh())
                {
                    var itemsAsyncEnumerator = SnapshotResultFileHelper.GetSearchResultItemsAsync(ResultMeta, ct);
                    int counter = 1;

                    bool culcScore = VisibilityMap[CustomFieldTypeName_Score];
                    await foreach (var item in itemsAsyncEnumerator)
                    {
                        var itemVM = new SnapshotItemViewModel(counter, item);
                        if (culcScore)
                        {
                            CulcScore(itemVM);
                        }
                        ItemsView.Add(itemVM);
                        counter++;
                        ct.ThrowIfCancellationRequested();
                    }
                }

                ViewCounterWeightingFactor = _searchResultSettings.ToReactivePropertySlimAsSynchronized(x => x.ViewCounterWeightingFactor, mode: ReactivePropertyMode.DistinctUntilChanged)
                    .AddTo(_navigationDisposable);
                MylistCounterWeightingFactor = _searchResultSettings.ToReactivePropertySlimAsSynchronized(x => x.MylistCounterWeightingFactor, mode: ReactivePropertyMode.DistinctUntilChanged)
                    .AddTo(_navigationDisposable);
                CommentCounterWeightingFactor = _searchResultSettings.ToReactivePropertySlimAsSynchronized(x => x.CommentCounterWeightingFactor, mode: ReactivePropertyMode.DistinctUntilChanged)
                    .AddTo(_navigationDisposable);
                LikeCounterWeightingFactor = _searchResultSettings.ToReactivePropertySlimAsSynchronized(x => x.LikeCounterWeightingFactor, mode: ReactivePropertyMode.DistinctUntilChanged)
                    .AddTo(_navigationDisposable);

                _prevViewCounterWeightingFactor = _searchResultSettings.ViewCounterWeightingFactor;
                _prevMylistCounterWeightingFactor = _searchResultSettings.MylistCounterWeightingFactor;
                _prevCommentCounterWeightingFactor = _searchResultSettings.CommentCounterWeightingFactor;
                _prevLikeCounterWeightingFactor = _searchResultSettings.LikeCounterWeightingFactor;
            }
            catch (Exception ex)
            {
                IsFailed = true;
                FailedErrorMessage = ex.Message;
                ItemsView.Clear();
                throw;
            }
        }

        double _prevViewCounterWeightingFactor;
        double _prevMylistCounterWeightingFactor;
        double _prevCommentCounterWeightingFactor;
        double _prevLikeCounterWeightingFactor;

        private DelegateCommand _ReCulcScoreCommand;
        public DelegateCommand ReCulcScoreCommand =>
            _ReCulcScoreCommand ?? (_ReCulcScoreCommand = new DelegateCommand(ExecuteReCulcScoreCommand));

        async void ExecuteReCulcScoreCommand()
        {
            // 閉じた瞬間に呼ばれると同値のままの可能性があるのでちょい待ち
            await Task.Delay(1);

            if (_prevViewCounterWeightingFactor == _searchResultSettings.ViewCounterWeightingFactor
                && _prevMylistCounterWeightingFactor == _searchResultSettings.MylistCounterWeightingFactor
                && _prevCommentCounterWeightingFactor == _searchResultSettings.CommentCounterWeightingFactor
                && _prevLikeCounterWeightingFactor == _searchResultSettings.LikeCounterWeightingFactor
                )
            {
                return;
            }

            _prevViewCounterWeightingFactor = _searchResultSettings.ViewCounterWeightingFactor;
            _prevMylistCounterWeightingFactor = _searchResultSettings.MylistCounterWeightingFactor;
            _prevCommentCounterWeightingFactor = _searchResultSettings.CommentCounterWeightingFactor;
            _prevLikeCounterWeightingFactor = _searchResultSettings.LikeCounterWeightingFactor;

            Debug.WriteLine("スコア再計算 開始");
            foreach (var item in ItemsView.Cast<SnapshotItemViewModel>())
            {
                CulcScore(item);
                if (_navigationCts?.IsCancellationRequested ?? true) { return; }
            }

            if (ItemsView.SortDescriptions.Any(x => x.PropertyName == CustomFieldTypeName_Score))
            {
                ItemsView.RefreshSorting();
            }

            Debug.WriteLine("スコア再計算 完了");
        }
        

        #region Score

        private void CulcScore(SnapshotItemViewModel item)
        {
            item.Score = CulcScore(item.ViewCounter, item.MylistCounter, item.CommentCounter, item.LikeCounter);
        }

        public long CulcScore(long? viewCounter, long? mylistCounter, long? commentCounter, long? likeCounter)
        {
            return (long)Math.Round(
                  (viewCounter ?? 0) * _searchResultSettings.ViewCounterWeightingFactor
                + (mylistCounter ?? 0) * _searchResultSettings.MylistCounterWeightingFactor
                + (commentCounter ?? 0) * _searchResultSettings.CommentCounterWeightingFactor
                + (likeCounter ?? 0) * _searchResultSettings.LikeCounterWeightingFactor
                );
        }

        #endregion
    }


    public sealed class SnapshotItemViewModel : BindableBase
    {
        private readonly SnapshotVideoItem _snapshotVideoItem;

        public SnapshotItemViewModel(int index, SnapshotVideoItem snapshotVideoItem)
        {
            Index = index;
            _snapshotVideoItem = snapshotVideoItem;
        }

        public int Index { get; }


        private long? _Score;
        public long? Score
        {
            get { return _Score; }
            set { SetProperty(ref _Score, value); }
        }


        public long? MylistCounter => _snapshotVideoItem.MylistCounter;

        public long? LengthSeconds => _snapshotVideoItem.LengthSeconds;

        public string CategoryTags => _snapshotVideoItem.CategoryTags;

        public long? ViewCounter => _snapshotVideoItem.ViewCounter;

        public long? CommentCounter => _snapshotVideoItem.CommentCounter;

        public long? LikeCounter => _snapshotVideoItem.LikeCounter;

        public string Genre => _snapshotVideoItem.Genre;

        public DateTimeOffset? StartTime => _snapshotVideoItem.StartTime;

        public DateTimeOffset? LastCommentTime => _snapshotVideoItem.LastCommentTime;

        public string Description => _snapshotVideoItem.Description;

        public string Tags => _snapshotVideoItem.Tags;

        public string LastResBody => _snapshotVideoItem.LastResBody;

        public string ContentId => _snapshotVideoItem.ContentId;

        public long? UserId => _snapshotVideoItem.UserId.HasValue ? (long)_snapshotVideoItem.UserId.Value.RawId : null;

        public string Title => _snapshotVideoItem.Title;

        public string ChannelId => _snapshotVideoItem.ChannelId;

        public Uri ThumbnailUrl => _snapshotVideoItem.ThumbnailUrl;

    }
}
