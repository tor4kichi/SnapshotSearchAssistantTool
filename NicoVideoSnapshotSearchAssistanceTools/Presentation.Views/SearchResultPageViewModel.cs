using Microsoft.Toolkit.Diagnostics;
using Microsoft.Toolkit.Mvvm.Messaging;
using Microsoft.Toolkit.Uwp.UI.Custom;
using NiconicoToolkit;
using NiconicoToolkit.Channels;
using NiconicoToolkit.SnapshotSearch;
using NiconicoToolkit.SnapshotSearch.Filters;
using NiconicoToolkit.User;
using NiconicoToolkit.Video;
using NicoVideoSnapshotSearchAssistanceTools.Models.Domain;
using NicoVideoSnapshotSearchAssistanceTools.Models.Domain.Expressions;
using NicoVideoSnapshotSearchAssistanceTools.Presentation.Views;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Navigation;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

            ScoringVM = new(_searchResultSettings);
            ItemsView = new AdvancedCollectionView();
            ItemsView.Source = _Items;
            ItemsView.Filter = x =>
            {
                SnapshotItemViewModel item = x as SnapshotItemViewModel;
                foreach (var filter in Filters)
                {
                    if (!filter.Compare(item))
                    {
                        return false;
                    }
                }

                return true;
            };

            RestoreFilterSettings(_searchResultSettings.ResultFilters);

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

        ObservableCollection<SnapshotItemViewModel> _Items = new ObservableCollection<SnapshotItemViewModel>();
        public AdvancedCollectionView ItemsView { get; private set; }

        public ScoringExpressionViewModel ScoringVM { get; }

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

        public ReactivePropertySlim<bool> NowRefreshing { get; } = new ReactivePropertySlim<bool>();

        public ObservableCollection<SimpleFilterViewModelBase> Filters { get; } = new ObservableCollection<SimpleFilterViewModelBase>();

        private DelegateCommand<string> _AddSimpleFilterCommand;
        public DelegateCommand<string> AddSimpleFilterCommand =>
            _AddSimpleFilterCommand ?? (_AddSimpleFilterCommand = new DelegateCommand<string>(ExecuteAddSimpleFilterCommand));

        void ExecuteAddSimpleFilterCommand(string parameter)
        {
            if (Enum.TryParse<SearchFieldType>(parameter, out var fieldType))
            {
                Guard.IsTrue(fieldType.IsFilterField(), nameof(SearchFieldTypeExtensions.IsFilterField));

                var type = fieldType.GetAttrubute<SearchFieldTypeAttribute>().Type;
                if (type == typeof(int))
                {
                    if (fieldType == SearchFieldType.LengthSeconds)
                    {
                        Filters.Add(new TimeSpanSimpleFilterViewModel(RemoveSimpleFilterItem, fieldType, SimpleFilterComparison.GreaterThan, TimeSpan.Zero));
                    }
                    else
                    {
                        Filters.Add(new IntSimpleFilterViewModel(RemoveSimpleFilterItem, fieldType, SimpleFilterComparison.GreaterThan, 0));
                    }
                }
                else if (type == typeof(DateTimeOffset))
                {
                    var time = DateTimeOffset.Now;
                    time -= time.TimeOfDay;
                    Filters.Add(new DateTimeOffsetSimpleFilterViewModel(RemoveSimpleFilterItem, fieldType, SimpleFilterComparison.GreaterThan, time));
                }
                else if (type == typeof(string))
                {
                    if (fieldType is SearchFieldType.Genre or SearchFieldType.GenreKeyword)
                    {
                        Filters.Add(new StringSimpleFilterViewModel(RemoveSimpleFilterItem, fieldType, ""));
                    }
                    else
                    {
                        Filters.Add(new StringSimpleFilterViewModel(RemoveSimpleFilterItem, fieldType, ""));
                    }
                }
            }
        }

        private void RemoveSimpleFilterItem(ISimpleFilterViewModel filterVM)
        {
            NowRefreshing.Value = true;
            try
            {
                Filters.Remove(filterVM as SimpleFilterViewModelBase);
            }
            finally
            {
                NowRefreshing.Value = false;
            }
        }


        private DelegateCommand _RefreshFilterCommand;
        public DelegateCommand RefreshFilterCommand =>
            _RefreshFilterCommand ?? (_RefreshFilterCommand = new DelegateCommand(ExecuteRefreshFilterCommand));

        void ExecuteRefreshFilterCommand()
        {
            SaveFilterSettings();

            var tempItemsView = ItemsView;
            ItemsView = null;
            RaisePropertyChanged(nameof(ItemsView));
            tempItemsView.RefreshFilter();

            ItemsView = tempItemsView;
            RaisePropertyChanged(nameof(ItemsView));
        }


        void RestoreFilterSettings(IEnumerable<SearchResultFilterItem> filterItems)
        {
            Filters.Clear();

            if (filterItems is null) { return; }

            foreach (var filter in filterItems)
            {
                if (Enum.TryParse<SearchFieldType>(filter.FieldName, out var fieldType))
                {
                    var type = fieldType.GetAttrubute<SearchFieldTypeAttribute>().Type;
                    if (type == typeof(int))
                    {
                        if (fieldType == SearchFieldType.LengthSeconds)
                        {
                            Filters.Add(new TimeSpanSimpleFilterViewModel(RemoveSimpleFilterItem, fieldType, filter.Comparison, filter.GetValueAsTimeSpan()));
                        }
                        else
                        {
                            Filters.Add(new IntSimpleFilterViewModel(RemoveSimpleFilterItem, fieldType, filter.Comparison, filter.GetValueAsInt()));
                        }
                    }
                    else if (type == typeof(DateTimeOffset))
                    {
                        var time = filter.GetValueAsDateTimeOffset();
                        time -= time.TimeOfDay;
                        Filters.Add(new DateTimeOffsetSimpleFilterViewModel(RemoveSimpleFilterItem, fieldType, filter.Comparison, time));
                    }
                    else if (type == typeof(string))
                    {
                        if (fieldType is SearchFieldType.Genre or SearchFieldType.GenreKeyword)
                        {
                            Filters.Add(new StringSimpleFilterViewModel(RemoveSimpleFilterItem, fieldType, filter.GetValueAsString()));
                        }
                        else
                        {
                            Filters.Add(new StringSimpleFilterViewModel(RemoveSimpleFilterItem, fieldType, filter.GetValueAsString()));
                        }
                    }
                }
                else if (filter.FieldName == CustomFieldTypeName_Score)
                {
                    throw new NotImplementedException();
                }
                else if (filter.FieldName == CustomFieldTypeName_Index)
                {
                    throw new NotImplementedException();
                }
                else
                {
                    throw new NotSupportedException();
                }
            }
        }

        private void SaveFilterSettings()
        {
            _searchResultSettings.ResultFilters = Filters.Select(x =>
            {
                if (x is ISimpleFilterViewModel filter)
                {
                    if (filter.Value is TimeSpan timeSpan)
                    {
                        return new SearchResultFilterItem()
                        {
                            FieldName = filter.FieldType.ToString(),
                            Comparison = filter.Comparison,
                            Value = ((TimeSpan)filter.Value).TotalSeconds,
                        };
                    }
                    else
                    {
                        return new SearchResultFilterItem()
                        {
                            FieldName = filter.FieldType.ToString(),
                            Comparison = filter.Comparison,
                            Value = filter.Value,
                        };
                    }                    
                }
                else
                {
                    throw new NotImplementedException(x.GetType().Name);
                }
            }
            ).ToArray();
        }


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
                SearchQueryVM = new SearchQueryViewModel(queryParameters);

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
                    CulcExpressionTreeContext scoreCulcConctxt = new();

                    var itemsAsyncEnumerator = SnapshotResultFileHelper.GetSearchResultItemsAsync(ResultMeta, ct);
                    int counter = 1;

                    bool culcScore = VisibilityMap[CustomFieldTypeName_Score] && ScoringVM.PrepareScoreCulc();
                    await foreach (var item in itemsAsyncEnumerator)
                    {
                        var itemVM = new SnapshotItemViewModel(counter, item);
                        if (culcScore)
                        {
                            itemVM.Score = ScoringVM.CulcScore(itemVM);
                        }
                        _Items.Add(itemVM);
                        counter++;
                        ct.ThrowIfCancellationRequested();
                    }
                }

                
                /*
                new[]
                {
                    Filters.ObserveElementPropertyChanged().ToUnit(),
                    Filters.CollectionChangedAsObservable().ToUnit()
                }
                .Merge()
                .Subscribe(x => 
                {
                    ItemsView.RefreshFilter();
                })
                .AddTo(_navigationDisposable);
                */
            }
            catch (Exception ex)
            {
                IsFailed = true;
                FailedErrorMessage = ex.Message;
                ItemsView.Clear();
                throw;
            }
        }



        public override void OnNavigatedFrom(INavigationParameters parameters)
        {
            _navigationDisposable.Dispose();
            _navigationCts.Cancel();
            _navigationCts.Dispose();

            foreach (var removeItem in ScoringVM.SettingItems)
            {
//                ScoringVM.SettingItems.Remove(removeItem);
                removeItem.Dispose();
            }

            base.OnNavigatedFrom(parameters);
        }

        private DelegateCommand _ReCulcScoreCommand;
        public DelegateCommand ReCulcScoreCommand =>
            _ReCulcScoreCommand ?? (_ReCulcScoreCommand = new DelegateCommand(ExecuteReCulcScoreCommand));

        async void ExecuteReCulcScoreCommand()
        {
            // 閉じた瞬間に呼ばれると同値のままの可能性があるのでちょい待ち
            await Task.Delay(1);

            ScoringVM.PrepareScoreCulc();

            Debug.WriteLine("スコア再計算 開始");
            foreach (var item in ItemsView.Cast<SnapshotItemViewModel>())
            {
                item.Score = ScoringVM.CulcScore(item);

                if (_navigationCts?.IsCancellationRequested ?? true) { return; }
            }

            if (ItemsView.SortDescriptions.Any(x => x.PropertyName == CustomFieldTypeName_Score))
            {
                ItemsView.RefreshSorting();
            }

            Debug.WriteLine("スコア再計算 完了");
        }
        

    }


    public class ScoringExpressionViewModel : BindableBase
    {
        public ScoringExpressionViewModel(SearchResultSettings searchResultSettings)
        {
            _searchResultSettings = searchResultSettings;

            SettingItems = new (_searchResultSettings.ScoringSettings.Select(x => new ScoringExpressionItemViewModel(x)));
            CurrentScoringSetting = new ReactivePropertySlim<ScoringExpressionItemViewModel>(SettingItems.ElementAtOrDefault(_searchResultSettings.CurrentScoringSettingIndex) ?? SettingItems.ElementAtOrDefault(0));
        }

        public ObservableCollection<ScoringExpressionItemViewModel> SettingItems { get; }

        public ReactivePropertySlim<ScoringExpressionItemViewModel> CurrentScoringSetting { get; private set; }


        private readonly SearchResultSettings _searchResultSettings;
        CulcExpressionTreeContext _context = new CulcExpressionTreeContext();

        public void SaveScoringExpressionSettings()
        {
            foreach (var setting in SettingItems)
            {
                setting.ScoringSettingItem.Title = setting.Title.Value;
                setting.ScoringSettingItem.VariableDeclarations = setting.VariableDeclarations.Select(x => x.VariableDeclaration).ToArray();
                setting.ScoringSettingItem.ScoreCulsExpressionText = setting.ScoreCulcExpressionText.Value;
            }

            if (CurrentScoringSetting.Value?.IsRemoveRequested ?? false)
            {
                CurrentScoringSetting.Value = SettingItems.FirstOrDefault(x => x.IsRemoveRequested is false);
            }

            _searchResultSettings.CurrentScoringSettingIndex = SettingItems.IndexOf(CurrentScoringSetting.Value);

            _searchResultSettings.ScoringSettings = SettingItems
                .Where(x => x.IsRemoveRequested is false)
                .Select(x => x.ScoringSettingItem)
                .ToArray();
        }

        private DelegateCommand _SaveScoringSettingsCommand;
        public DelegateCommand SaveScoringSettingsCommand =>
            _SaveScoringSettingsCommand ?? (_SaveScoringSettingsCommand = new DelegateCommand(ExecuteSaveScoringSettingsCommand));

        void ExecuteSaveScoringSettingsCommand()
        {
            SaveScoringExpressionSettings();
        }

        public bool PrepareScoreCulc()
        {
            var scoringVM = CurrentScoringSetting.Value;
            if (scoringVM == null) 
            {
                return false; 
            }

            return scoringVM.PrepareScoreCulc();
        }

        public long? CulcScore(SnapshotItemViewModel item)
        {
            var scoringVM = CurrentScoringSetting.Value;
            if (scoringVM == null)
            {
                return null;
            }

            
            return scoringVM.CulcScore(item, _context);
        }

        private DelegateCommand _AddScoringExpressionCommand;
        public DelegateCommand AddScoringExpressionCommand =>
            _AddScoringExpressionCommand ?? (_AddScoringExpressionCommand = new DelegateCommand(ExecuteAddScoringExpressionCommand));

        void ExecuteAddScoringExpressionCommand()
        {
            SettingItems.Add(new ScoringExpressionItemViewModel(SearchResultSettings.CreateDefaultScoringSettingItem()));
        }

    }

    

    public class ScoringExpressionItemViewModel : BindableBase, IDisposable
    {
        public ScoringSettingItem ScoringSettingItem { get; }

        public ScoringExpressionItemViewModel(ScoringSettingItem scoringSettingItem)
        {
            ScoringSettingItem = scoringSettingItem;
            Title = new ReactivePropertySlim<string>(ScoringSettingItem.Title);
            ScoreCulcExpressionText = new ReactivePropertySlim<string>(ScoringSettingItem.ScoreCulsExpressionText);
            VariableDeclarations = new ObservableCollection<VariableDeclarationViewModel>(ScoringSettingItem.VariableDeclarations.Select(x => new VariableDeclarationViewModel(x)));
            PrepareScoreCulc();
        }

        public void Dispose()
        {
            Title.Dispose();
            ScoreCulcExpressionText.Dispose();
        }

        public ReactivePropertySlim<string> Title { get; }

        public ReactivePropertySlim<string> ScoreCulcExpressionText { get; }

        public ObservableCollection<VariableDeclarationViewModel> VariableDeclarations { get; }


        private DelegateCommand _AddVariableDeclarationCommand;
        public DelegateCommand AddVariableDeclarationCommand =>
            _AddVariableDeclarationCommand ?? (_AddVariableDeclarationCommand = new DelegateCommand(ExecuteAddVariableDeclarationCommand));

        void ExecuteAddVariableDeclarationCommand()
        {
            VariableDeclarations.Add(new VariableDeclarationViewModel(new ScoringVariableDeclaration()));
        }



        private bool _IsRemoveRequested;
        public bool IsRemoveRequested
        {
            get { return _IsRemoveRequested;; }
            set { SetProperty(ref _IsRemoveRequested, value); }
        }

        private DelegateCommand _ToggleRemoveRequestedCommand;

        public DelegateCommand ToggleRemoveRequestedCommand =>
            _ToggleRemoveRequestedCommand ?? (_ToggleRemoveRequestedCommand = new DelegateCommand(ExecuteRemoveCommand));

        void ExecuteRemoveCommand()
        {
            IsRemoveRequested = !IsRemoveRequested;
        }



        Dictionary<string, ICulcExpressionTreeNode> _variableDeclNodes;
        ICulcExpressionTreeNode _scoreCulcNode;

        public bool PrepareScoreCulc()
        {
            _variableDeclNodes = null;
            _scoreCulcNode = null;
            HasError = false;
            ErrorMessage = string.Empty;

            Dictionary<string, ICulcExpressionTreeNode> variableNameToExpressionNodeMap = new Dictionary<string, ICulcExpressionTreeNode>();
            foreach (var decl in VariableDeclarations)
            {
                try
                {
                    variableNameToExpressionNodeMap.Add(decl.VariableName, CulcExpressionTree.CreateCulcExpressionTree(decl.ExpressionText));
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.ToString());

                    HasError = true;
                    ErrorMessage = $"変数定義: {decl.VariableName} でエラー. : {ex.Message}";
                    return false;
                }
            }

            try
            {
                _scoreCulcNode = CulcExpressionTree.CreateCulcExpressionTree(ScoreCulcExpressionText.Value);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
                HasError = true;
                ErrorMessage = $"計算式評価に失敗 :\n {ex.Message}";
                return false;
            }


            _variableDeclNodes = variableNameToExpressionNodeMap;

            return true;
        }

        public long? CulcScore(SnapshotItemViewModel item, CulcExpressionTreeContext context)
        {
            if (_scoreCulcNode == null) { return null; }

            context.VariableToValueMap.Clear();
            context.VariableToValueMap["V"] = item.ViewCounter ?? 0;
            context.VariableToValueMap["C"] = item.CommentCounter ?? 0;
            context.VariableToValueMap["M"] = item.MylistCounter ?? 0;
            context.VariableToValueMap["L"] = item.LikeCounter ?? 0;

            foreach (var decl in _variableDeclNodes)
            {
                context.VariableToValueMap[decl.Key] = decl.Value.Culc(context);
            }

            return (long)Math.Round(_scoreCulcNode.Culc(context));
        }



        private bool _HasError;
        public bool HasError
        {
            get { return _HasError; }
            private set { SetProperty(ref _HasError, value); }
        }

        private string _ErrorMessage;
        public string ErrorMessage
        {
            get { return _ErrorMessage; }
            set { SetProperty(ref _ErrorMessage, value); }
        }
    }


    public sealed class VariableDeclarationViewModel : BindableBase
    {
        public VariableDeclarationViewModel(ScoringVariableDeclaration variableDeclaration)
        {
            VariableDeclaration = variableDeclaration;
            _VariableName = VariableDeclaration.VariableName;
            _ExpressionText = VariableDeclaration.ExpressionText;
        }

        public ScoringVariableDeclaration VariableDeclaration { get; }


        private string _VariableName;
        public string VariableName
        {
            get { return _VariableName; }
            set 
            {
                if (SetProperty(ref _VariableName, value))
                {
                    VariableDeclaration.VariableName = value;
                }
            }
        }


        private string _ExpressionText;
        public string ExpressionText
        {
            get 
            {
                return _ExpressionText; 
            }
            set 
            {
                if (SetProperty(ref _ExpressionText, value))
                {
                    VariableDeclaration.ExpressionText = value;
                }
            }
        }


        private bool _IsRemoveRequested;
        public bool IsRemoveRequested
        {
            get { return _IsRemoveRequested; ; }
            set { SetProperty(ref _IsRemoveRequested, value); }
        }


        private DelegateCommand _ToggleRemoveRequestedCommand;

        public DelegateCommand ToggleRemoveRequestedCommand =>
            _ToggleRemoveRequestedCommand ?? (_ToggleRemoveRequestedCommand = new DelegateCommand(ExecuteRemoveCommand));

        void ExecuteRemoveCommand()
        {
            IsRemoveRequested = !IsRemoveRequested;
        }
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

        string[] _Tags_Separated;
        public string[] Tags_Separated => _Tags_Separated ??= Tags?.Split(' ') ?? new string[0];
    }
}
