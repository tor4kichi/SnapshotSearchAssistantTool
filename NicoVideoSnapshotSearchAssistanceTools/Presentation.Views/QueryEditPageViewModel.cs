using I18NPortable;
using Microsoft.Toolkit.Diagnostics;
using Microsoft.Toolkit.Mvvm.Input;
using Microsoft.Toolkit.Mvvm.Messaging;
using Microsoft.Toolkit.Mvvm.Messaging.Messages;
using NiconicoToolkit;
using NiconicoToolkit.SnapshotSearch;
using NiconicoToolkit.SnapshotSearch.Filters;
using NiconicoToolkit.SnapshotSearch.JsonFilters;
using NicoVideoSnapshotSearchAssistanceTools.Models.Domain;
using NicoVideoSnapshotSearchAssistanceTools.Models.Domain.Expressions;
using NicoVideoSnapshotSearchAssistanceTools.Presentation.Services;
using NicoVideoSnapshotSearchAssistanceTools.Presentation.ViewModels.Messages;
using NicoVideoSnapshotSearchAssistanceTools.Presentation.Views;
using NicoVideoSnapshotSearchAssistanceTools.Presentation.Views.Dialogs;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Navigation;
using Reactive.Bindings;
using Reactive.Bindings.Extensions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NicoVideoSnapshotSearchAssistanceTools.Presentation.ViewModels
{
    public enum FilterType
    {
        SimpleFilter,
        JsonFilter,
    }


    public class SearchQueryParameterTemplateViewModel : IDialogSelectableItem
    {
        public SearchQueryParameterTemplateViewModel(SearchQueryEntity_V0 searchQueryEntity)
        {
            SearchQueryEntity = searchQueryEntity;
        }

        public string Name => SearchQueryEntity.Title;

        public SearchQueryEntity_V0 SearchQueryEntity { get; }
    }

    public sealed class QueryEditPageViewModel : ViewModelBase
    {
        private readonly IMessenger _messenger;
        private readonly IDialogService _dialogService;
        private readonly SearchQueryDatabase_V0 _searchQueryDatabase;
        private readonly SearchQueryEditSettings _searchQueryEditSettings;
        private readonly ApplicationInternalSettings _applicationInternalSettings;

        public static string[] GenreKeywords { get; } = new[] 
        {
            "エンターテイメント",
            "ラジオ",
            "音楽・サウンド",
            "ダンス",
            "動物",
            "自然",
            "料理",
            "旅行・アウトドア",
            "乗り物",
            "スポーツ",
            "社会・政治・時事",
            "技術・工作",
            "解説・講座",
            "アニメ",
            "ゲーム",
            "その他",
            //"R-18",
        };

        public QueryEditPageViewModel(
            IMessenger messenger,
            IDialogService dialogService,
            SearchQueryDatabase_V0 searchQueryDatabase,
            SearchQueryEditSettings searchQueryEditSettings,
            ApplicationInternalSettings applicationInternalSettings
            )
        {
            _messenger = messenger;
            _dialogService = dialogService;
            _searchQueryDatabase = searchQueryDatabase;
            _searchQueryEditSettings = searchQueryEditSettings;
            _applicationInternalSettings = applicationInternalSettings;
        }


        private SearchQueryViewModel _SearchQueryVM;
        public SearchQueryViewModel SearchQueryVM
        {
            get { return _SearchQueryVM; }
            set { SetProperty(ref _SearchQueryVM, value); }
        }

        public ReactivePropertySlim<string> ContextQueryParameter { get; private set; }

        public SearchFieldTypeSelectableItem[] FieldSelectableItems { get; } =
            SearchFieldTypeExtensions.FieldTypes
            .Select(x => new SearchFieldTypeSelectableItem(x))
            .ToArray();

        public SearchFieldTypeSelectableItem[] TargetSelectableItems { get; } =
            SearchFieldTypeExtensions.TargetFieldTypes
            .Select(x => new SearchFieldTypeSelectableItem(x))
            .ToArray();

        public SearchSort[] SortItems { get; } =
            SearchFieldTypeExtensions.SortFieldTypes.SelectMany(ToSearchSort)
            .ToArray();

        private static IEnumerable<SearchSort> ToSearchSort(SearchFieldType fieldType)
        {
            yield return new SearchSort(fieldType, SearchSortOrder.Desc);
            yield return new SearchSort(fieldType, SearchSortOrder.Asc);
        }

        #region Filters JsonFilter

        private FilterType _FilterType;
        public FilterType FilterType
        {
            get { return _FilterType; }
            set { SetProperty(ref _FilterType, value); }
        }

        public FilterType[] FilterTypeItems { get; } = new[] 
        {
            FilterType.SimpleFilter,
            FilterType.JsonFilter,
        };


        public ObservableCollection<ISimpleFilterViewModel> SimpleFilters { get; } = new();

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
                        SimpleFilters.Add(new TimeSpanSimpleFilterViewModel(RemoveSimpleFilterItem, fieldType, SimpleFilterComparison.GreaterThan, TimeSpan.Zero));
                    }
                    else
                    {
                        SimpleFilters.Add(new IntSimpleFilterViewModel(RemoveSimpleFilterItem, fieldType, SimpleFilterComparison.GreaterThan, 0));
                    }
                }
                else if (type == typeof(DateTimeOffset))
                {
                    var time = DateTimeOffset.Now;
                    time -= time.TimeOfDay;
                    SimpleFilters.Add(new DateTimeOffsetSimpleFilterViewModel(RemoveSimpleFilterItem, fieldType, SimpleFilterComparison.GreaterThan, time));
                }
                else if (type == typeof(string))
                {
                    if (fieldType is SearchFieldType.Genre or SearchFieldType.GenreKeyword)
                    {
                        SimpleFilters.Add(new StringSimpleFilterViewModel(RemoveSimpleFilterItem, fieldType, "", GenreKeywords));
                    }
                    else
                    {
                        SimpleFilters.Add(new StringSimpleFilterViewModel(RemoveSimpleFilterItem, fieldType, ""));
                    }
                }
            }
        }

        private void RemoveSimpleFilterItem(ISimpleFilterViewModel filterVM)
        {
            SimpleFilters.Remove(filterVM);
        }

        private DelegateCommand<ISimpleFilterViewModel> _RemoveSimpleFilterCommand;
        public DelegateCommand<ISimpleFilterViewModel> RemoveSimpleFilterCommand =>
            _RemoveSimpleFilterCommand ?? (_RemoveSimpleFilterCommand = new DelegateCommand<ISimpleFilterViewModel>(ExecuteRemoveSimpleFilterCommand));

        void ExecuteRemoveSimpleFilterCommand(ISimpleFilterViewModel parameter)
        {
            SimpleFilters.Remove(parameter);
        }



        private string _JsonFilterExpression;
        public string JsonFilterExpression
        {
            get { return _JsonFilterExpression; }
            set { SetProperty(ref _JsonFilterExpression, value); }
        }

        private string _JsonFilterExpressionErrorMessage;
        public string JsonFilterExpressionErrorMessage
        {
            get { return _JsonFilterExpressionErrorMessage; }
            private set { SetProperty(ref _JsonFilterExpressionErrorMessage, value); }
        }

        public ObservableCollection<IJsonFilterViewModel> JsonFilters { get; } = new();


        private DelegateCommand<string> _AddJsonFilterCommand;
        public DelegateCommand<string> AddJsonFilterCommand =>
            _AddJsonFilterCommand ?? (_AddJsonFilterCommand = new DelegateCommand<string>(ExecuteAddJsonFilterCommand));

        void ExecuteAddJsonFilterCommand(string parameter)
        {
            if (Enum.TryParse<SearchFieldType>(parameter, out var fieldType))
            {
                Guard.IsTrue(fieldType.IsFilterField(), nameof(SearchFieldTypeExtensions.IsFilterField));

                var type = fieldType.GetAttrubute<SearchFieldTypeAttribute>().Type;
                var variableName = MakeVariableName(fieldType, JsonFilters);
                if (type == typeof(int))
                {
                    if (fieldType == SearchFieldType.LengthSeconds)
                    {
                        JsonFilters.Add(new TimeSpanJsonFilterViewModel(RemoveJsonFilterItem, variableName, fieldType, JsonFilterComparison.Range, 0, 0, true, true));
                    }
                    else
                    {
                        JsonFilters.Add(new IntJsonFilterViewModel(RemoveJsonFilterItem, variableName, fieldType, JsonFilterComparison.Range, 0, 10000, true, true));
                    }
                }
                else if (type == typeof(DateTimeOffset))
                {
                    JsonFilters.Add(new DateTimeOffsetJsonFilterViewModel(RemoveJsonFilterItem, variableName, fieldType, JsonFilterComparison.Range, null, null, true, true));
                }
                else if (type == typeof(string))
                {
                    if (fieldType is SearchFieldType.Genre or SearchFieldType.GenreKeyword)
                    {
                        JsonFilters.Add(new StringJsonFilterViewModel(RemoveJsonFilterItem, variableName, fieldType, "", GenreKeywords));
                    }
                    else
                    {
                        JsonFilters.Add(new StringJsonFilterViewModel(RemoveJsonFilterItem, variableName, fieldType, ""));
                    }
                }
                else
                {
                    throw new NotSupportedException();
                }

                if (JsonFilterExpression == null || !JsonFilterExpression.Contains(variableName))
                {
                    if (string.IsNullOrWhiteSpace(JsonFilterExpression))
                    {
                        JsonFilterExpression += $"{variableName}";
                    }
                    else
                    {
                        JsonFilterExpression += $" and {variableName}";
                    }
                }
            }
        }

        private static string MakeVariableName(SearchFieldType target, IReadOnlyList<IJsonFilterViewModel> items)
        {
            foreach (int postfix in Enumerable.Range(1, 9))
            {
                string candidateName = target.ToString() + postfix;
                if (items.Where(x => x.FieldType == target).Any(x => x.VariableName == candidateName) == false)
                {
                    return candidateName;
                }
            }

            throw new InvalidOperationException();
        }

        private void RemoveJsonFilterItem(IJsonFilterViewModel filterVM)
        {
            JsonFilters.Remove(filterVM);
        }

        private DelegateCommand<IJsonFilterViewModel> _RemoveJsonFilterCommand;
        public DelegateCommand<IJsonFilterViewModel> RemoveJsonFilterCommand =>
            _RemoveJsonFilterCommand ?? (_RemoveJsonFilterCommand = new DelegateCommand<IJsonFilterViewModel>(ExecuteRemoveJsonFilterCommand));

        void ExecuteRemoveJsonFilterCommand(IJsonFilterViewModel parameter)
        {
            JsonFilters.Remove(parameter);
        }



        void RecursiveParseJsonFilter(IJsonSearchFilter jsonFilter, StringBuilder sb)
        {
            if (jsonFilter is OrJsonFilter orFilter)
            {
                bool isFirst = true;
                foreach (var filter in orFilter.Filters)
                {
                    if (!isFirst)
                    {
                        sb.Append(" or ");
                    }

                    RecursiveParseJsonFilter(filter, sb);

                    isFirst = false;
                }
            }
            else if (jsonFilter is AndJsonFilter andFilter)
            {
                bool isFirst = true;
                foreach (var filter in andFilter.Filters)
                {
                    if (!isFirst)
                    {
                        sb.Append(" and ");
                    }

                    RecursiveParseJsonFilter(filter, sb);

                    isFirst = false;
                }
            }
            else if (jsonFilter is NotJsonFilter notFilter)
            {
                sb.Append("not ");
                bool isRequirePrioritizingToken = notFilter.Filter is not IValueJsonSearchFilter and not OrJsonFilter;
                if (isRequirePrioritizingToken)
                {
                    sb.Append("(");
                }

                RecursiveParseJsonFilter(notFilter.Filter, sb);

                if (isRequirePrioritizingToken)
                {
                    sb.Append(")");
                }
            }
            else if (jsonFilter is IValueJsonSearchFilter valueFilter)
            {
                var fieldType = valueFilter.FieldType;
                var type = fieldType.GetAttrubute<SearchFieldTypeAttribute>().Type;
                var variableName = MakeVariableName(fieldType, JsonFilters);
                JsonFilterComparison comparison = valueFilter switch
                {
                    RangeJsonFilter => JsonFilterComparison.Range,
                    EqualJsonFilter => JsonFilterComparison.Equal,
                    _ => throw new NotSupportedException(valueFilter.GetType().Name),
                };
                RangeJsonFilter rangeFilter = valueFilter as RangeJsonFilter;
                EqualJsonFilter equalFilter = valueFilter as EqualJsonFilter;

                if (type == typeof(int))
                {
                    if (fieldType == SearchFieldType.LengthSeconds)
                    {
                        if (rangeFilter != null)
                        {
                            JsonFilters.Add(new TimeSpanJsonFilterViewModel(RemoveJsonFilterItem, variableName, fieldType, comparison, (int?)rangeFilter.From, (int?)rangeFilter.To, rangeFilter.IncludeLower, rangeFilter.IncludeUpper));
                        }
                        else if (equalFilter != null)
                        {
                            JsonFilters.Add(new TimeSpanJsonFilterViewModel(RemoveJsonFilterItem, variableName, fieldType, comparison, (int?)equalFilter.Value, 0, true, true));
                        }
                        else
                        {
                            throw new NotSupportedException();
                        }
                    }
                    else
                    {
                        if (rangeFilter != null)
                        {
                            JsonFilters.Add(new IntJsonFilterViewModel(RemoveJsonFilterItem, variableName, fieldType, comparison, (int?)rangeFilter.From, (int?)rangeFilter.To, rangeFilter.IncludeLower, rangeFilter.IncludeUpper));
                        }
                        else if (equalFilter != null)
                        {
                            JsonFilters.Add(new IntJsonFilterViewModel(RemoveJsonFilterItem, variableName, fieldType, comparison, (int?)equalFilter.Value, 10000, true, true));
                        }
                        else
                        {
                            throw new NotSupportedException();
                        }
                    }
                }
                else if (type == typeof(DateTimeOffset))
                {
                    var time = DateTimeOffset.Now;
                    time -= time.TimeOfDay;
                    if (rangeFilter != null)
                    {
                        JsonFilters.Add(new DateTimeOffsetJsonFilterViewModel(RemoveJsonFilterItem, variableName, fieldType, comparison, (DateTimeOffset?)rangeFilter.From, (DateTimeOffset?)rangeFilter.To, rangeFilter.IncludeLower, rangeFilter.IncludeUpper));
                    }
                    else if (equalFilter != null)
                    {
                        JsonFilters.Add(new DateTimeOffsetJsonFilterViewModel(RemoveJsonFilterItem, variableName, fieldType, comparison, (DateTimeOffset?)equalFilter.Value, time, true, true));
                    }
                    else
                    {
                        throw new NotSupportedException();
                    }
                }
                else if (type == typeof(string))
                {
                    Guard.IsOfType<EqualJsonFilter>(valueFilter, nameof(valueFilter));

                    if (fieldType is SearchFieldType.Genre or SearchFieldType.GenreKeyword)
                    {
                        JsonFilters.Add(new StringJsonFilterViewModel(RemoveJsonFilterItem, variableName, fieldType, (string)equalFilter.Value, GenreKeywords));
                    }
                    else
                    {
                        JsonFilters.Add(new StringJsonFilterViewModel(RemoveJsonFilterItem, variableName, fieldType, (string)equalFilter.Value));
                    }
                }

                sb.Append(variableName);
            }
            else 
            {
                throw new NotSupportedException(jsonFilter?.GetType().Name);
            }
        }



        public IJsonSearchFilter JsonFilterVMToModel(IJsonFilterViewModel filterVM)
        {
            if (SearchFieldTypeExtensions.IsFilterField(filterVM.FieldType) is false)
            {
                throw new NotSupportedException();
            }

            var targetType = filterVM.FieldType.GetAttrubute<SearchFieldTypeAttribute>().Type;
            if (filterVM.Comparison == JsonFilterComparison.Equal)
            {
                var fieldType = filterVM.FieldType;
                return new EqualJsonFilter(fieldType, filterVM.FromValue);
            }
            else if (filterVM.Comparison == JsonFilterComparison.Range)
            {
                var fieldType = filterVM.FieldType;
                return new RangeJsonFilter(fieldType, filterVM.FromValue, filterVM.ToValue, filterVM.IncludeLower, filterVM.IncludeUpper);
            }
            else
            {
                throw new NotSupportedException();
            }
        }

        #endregion Filters JsonFilter


        private bool _IsLoadingFailed;
        public bool IsLoadingFailed
        {
            get { return _IsLoadingFailed; }
            set { SetProperty(ref _IsLoadingFailed, value); }
        }

        private string _FailedMessage;
        public string FailedMessage
        {
            get { return _FailedMessage; }
            set { SetProperty(ref _FailedMessage, value); }
        }

        CompositeDisposable _navigationDisposables;

        #region Input Field Message

        private IReadOnlyReactiveProperty<bool> _IsInvalidTarget;
        public IReadOnlyReactiveProperty<bool> IsInvalidTargets
        {
            get { return _IsInvalidTarget; }
            set { SetProperty(ref _IsInvalidTarget, value); }
        }


        private IReadOnlyReactiveProperty<bool> _IsInvalidSort;
        public IReadOnlyReactiveProperty<bool> IsInvalidSort
        {
            get { return _IsInvalidSort; }
            set { SetProperty(ref _IsInvalidSort, value); }
        }

        private IReadOnlyReactiveProperty<bool> _IsInvalidContext;
        public IReadOnlyReactiveProperty<bool> IsInvalidContext
        {
            get { return _IsInvalidContext; }
            set { SetProperty(ref _IsInvalidContext, value); }
        }

        #endregion Input Field Message        

        public override void OnNavigatedTo(INavigationParameters parameters)
        {
            base.OnNavigatedTo(parameters);

            _navigationDisposables = new CompositeDisposable();

            ContextQueryParameter = _applicationInternalSettings.ToReactivePropertySlimAsSynchronized(x => x.ContextQueryParameter)
                .AddTo(_navigationDisposables);
            RaisePropertyChanged(nameof(ContextQueryParameter));

            try
            {
                if (parameters.GetNavigationMode() == NavigationMode.New)
                {
                    string queryParameters = null;
                    if (parameters.TryGetValue("query", out string query))
                    {
                        queryParameters = query;
                    }
                    else
                    {
                        queryParameters = _searchQueryEditSettings.EdittingQueryParameters;
                    }

                    IsLoadingFailed = false;
                    try
                    {
                        SearchQueryVM = new SearchQueryViewModel(queryParameters);

                        foreach (var selectableItem in TargetSelectableItems)
                        {
                            selectableItem.IsSelected = SearchQueryVM.Targets.Any(x => x == selectableItem.Content);
                            selectableItem.ObserveProperty(x => x.IsSelected, isPushCurrentValueAtFirst: false)
                                .Subscribe(x => SearchQueryVM.Targets = TargetSelectableItems.Where(x => x.IsSelected).Select(x => x.Content).ToArray())
                                .AddTo(_navigationDisposables);
                        }

                        foreach (var selectableItem in FieldSelectableItems)
                        {
                            selectableItem.IsSelected = SearchQueryVM.Fields.Any(x => x == selectableItem.Content);
                            selectableItem.ObserveProperty(x => x.IsSelected, isPushCurrentValueAtFirst: false)
                                .Subscribe(x => SearchQueryVM.Fields = FieldSelectableItems.Where(x => x.IsSelected).Select(x => x.Content).ToArray())
                                .AddTo(_navigationDisposables);
                        }

                        SimpleFilters.Clear();
                        if (SearchQueryVM.Filters is CompositeSearchFilter compositeSearchFilter)
                        {
                            foreach (var simpleFilter in compositeSearchFilter.Filters)
                            {
                                if (simpleFilter is CompareSimpleSearchFilter<DateTimeOffset> dateTimeFilter)
                                {
                                    SimpleFilters.Add(new DateTimeOffsetSimpleFilterViewModel(RemoveSimpleFilterItem, dateTimeFilter.FilterType, dateTimeFilter.Condition, dateTimeFilter.Value));
                                }
                                else if (simpleFilter is CompareSimpleSearchFilter<int> numberFilter)
                                {
                                    if (numberFilter.FilterType == SearchFieldType.LengthSeconds)
                                    {
                                        SimpleFilters.Add(new TimeSpanSimpleFilterViewModel(RemoveSimpleFilterItem, numberFilter.FilterType, numberFilter.Condition, TimeSpan.FromSeconds(numberFilter.Value)));
                                    }
                                    else
                                    {
                                        SimpleFilters.Add(new IntSimpleFilterViewModel(RemoveSimpleFilterItem, numberFilter.FilterType, numberFilter.Condition, numberFilter.Value));
                                    }
                                }
                                else if (simpleFilter is CompareSimpleSearchFilter<string> stringFilter)
                                {
                                    if (stringFilter.FilterType is SearchFieldType.Genre or SearchFieldType.GenreKeyword)
                                    {
                                        SimpleFilters.Add(new StringSimpleFilterViewModel(RemoveSimpleFilterItem, stringFilter.FilterType, stringFilter.Value, GenreKeywords));
                                    }
                                    else
                                    {
                                        SimpleFilters.Add(new StringSimpleFilterViewModel(RemoveSimpleFilterItem, stringFilter.FilterType, stringFilter.Value));
                                    }
                                }
                            }

                            FilterType = FilterType.SimpleFilter;
                        }
                        else if (SearchQueryVM.Filters is IJsonSearchFilter jsonFilter)
                        {
                            StringBuilder sb = new();
                            RecursiveParseJsonFilter(jsonFilter, sb);
                            JsonFilterExpression = sb.ToString();

                            FilterType = FilterType.JsonFilter;
                        }


                        new[]
                        {
                            this.ObserveProperty(x => x.FilterType, isPushCurrentValueAtFirst: false).ToUnit(),
                            SimpleFilters.CollectionChangedAsObservable().ToUnit(),
                            SimpleFilters.ObserveElementPropertyChanged().ToUnit(),

                            this.ObserveProperty(x => x.JsonFilterExpression).ToUnit(),
                            //JsonFilters.CollectionChangedAsObservable().ToUnit(),
                            JsonFilters.ObserveElementPropertyChanged().ToUnit(),
                        }
                        .Merge()
                        .Subscribe(_ => RefreshFilterConditions())
                        .AddTo(_navigationDisposables);





                        IsInvalidTargets = SearchQueryVM.GetValidTargetsObservable().Select(x => !x).ToReadOnlyReactivePropertySlim().AddTo(_navigationDisposables);
                        IsInvalidSort = SearchQueryVM.GetValidSortObservable().Select(x => !x).ToReadOnlyReactivePropertySlim().AddTo(_navigationDisposables);
                        IsInvalidContext = ContextQueryParameter.Select(x => string.IsNullOrWhiteSpace(x)).ToReadOnlyReactivePropertySlim().AddTo(_navigationDisposables);

                        StartSearchProcessCommand = new[]
                        {
                            IsInvalidTargets,
                            IsInvalidSort,
                            IsInvalidContext,
                        }
                        .CombineLatestValuesAreAllFalse()
                        .ToReactiveCommand()
                        .AddTo(_navigationDisposables);

                        StartSearchProcessCommand.Subscribe(x => ExecuteStartSearchProcessCommand())
                            .AddTo(_navigationDisposables);

                        SearchQueryVM.PropertyChangedAsObservable()
                            .Subscribe(_ => SaveEdittingQueryParameters())
                            .AddTo(_navigationDisposables);
                    }
                    catch (Exception e)
                    {
                        IsLoadingFailed = true;
                        FailedMessage = e.Message;
                        throw;
                    }
                    _applicationInternalSettings.SaveLastOpenPage(nameof(QueryEditPage));
                }
            }
            catch
            {
                _navigationDisposables.Dispose();
                _navigationDisposables = null;
            }
        }


        private bool ValidateJsonFilterExpression()
        {
            JsonFilterExpressionErrorMessage = null;
            if (JsonFilters.Any() is false)
            {
                JsonFilterExpressionErrorMessage = null;
                return true;
            }

            if (string.IsNullOrWhiteSpace(JsonFilterExpression))
            {
                JsonFilterExpressionErrorMessage = "条件式が入力さていません";
                return false;
            }

            var dataBag = JsonFilters.Select(x => x.VariableName).ToHashSet();

            var tokens = LogicalOperatorExpressionTokenizer.Tokenize(JsonFilterExpression).ToArray();
            foreach (var token in tokens)
            {
                if (token is StringToken strToken)
                {
                    if (dataBag.Contains(strToken.String) is false
                        && strToken.String is not "or" and not "and" and not "not"
                        )
                    {
                        JsonFilterExpressionErrorMessage = "不明な文字列: " + strToken.String;
                        return false;
                    }
                }
            }

            return true;
        }

        private void RefreshFilterConditions()
        {
            if (FilterType is FilterType.SimpleFilter)
            {
                CompositeSearchFilter compositeSearchFilter = new();

                foreach (var simpleFilter in SimpleFilters)
                {
                    if (simpleFilter.FieldType == SearchFieldType.LengthSeconds)
                    {
                        compositeSearchFilter.AddCompareFilter(simpleFilter.FieldType, (int)((TimeSpan)simpleFilter.Value).TotalSeconds, simpleFilter.Comparison);
                    }
                    else
                    {
                        compositeSearchFilter.AddCompareFilter(simpleFilter.FieldType, simpleFilter.Value, simpleFilter.Comparison);
                    }
                }

                SearchQueryVM.Filters = compositeSearchFilter;
                
            }
            else
            {
                if (ValidateJsonFilterExpression() is false)
                {
                    return;
                }

                if (string.IsNullOrWhiteSpace(JsonFilterExpression))
                {
                    return;
                }

                try
                {
                    var dataBag = JsonFilters.ToDictionary(x => x.VariableName, x => JsonFilterVMToModel(x));
                    SearchQueryVM.Filters = LogicalOperatorExpressionTokenizer.Parse(JsonFilterExpression, dataBag);
                }
                catch (Exception ex)
                {
                    JsonFilterExpressionErrorMessage = ex.Message;
                }
            }
        }

        private void SaveEdittingQueryParameters()
        {
            _searchQueryEditSettings.EdittingQueryParameters = SearchQueryVM.SeriaizeParameters(); ;

            Debug.WriteLine("query saved! ");
        }

        public override void OnNavigatedFrom(INavigationParameters parameters)
        {
            _navigationDisposables?.Dispose();

            base.OnNavigatedFrom(parameters);
        }

        private AsyncRelayCommand _SaveCommand;
        public AsyncRelayCommand SaveCommand =>
            _SaveCommand ??= new AsyncRelayCommand(ExecuteSaveCommand);

        async Task ExecuteSaveCommand()
        {
            // テキスト入力ダイアログを表示してテンプレート名を取得

            if (SearchQueryVM == null) { throw new InvalidOperationException(); }
            var queryParameters = SearchQueryVM.SeriaizeParameters();
            var name = await GetSearchQueryTemplateNameAsync(queryParameters);
            if (string.IsNullOrWhiteSpace(name)) { return; }

            // TODO: 同名登録チェック

            _searchQueryDatabase.CreateItem(new SearchQueryEntity_V0() { Title = name, QueryParameters = queryParameters });
        }

        async Task<string> GetSearchQueryTemplateNameAsync(string queryParameters)
        {
            return await _dialogService.GetInputTextAsync("検索条件を名前を付けて保存", "", "");
        }


        private DelegateCommand _LoadSearchQueryFromTemplateCommand;
        public DelegateCommand LoadSearchQueryFromTemplateCommand =>
            _LoadSearchQueryFromTemplateCommand ?? (_LoadSearchQueryFromTemplateCommand = new DelegateCommand(ExecuteLoadSearchQueryFromTemplateCommand));

        async void ExecuteLoadSearchQueryFromTemplateCommand()
        {
            var searchQueryTemplateItems = _searchQueryDatabase.ReadAllItems().Select(x => new SearchQueryParameterTemplateViewModel(x));
            if (await _dialogService.GetItemAsync("検索条件をテンプレートから読み込み", searchQueryTemplateItems) is not null 
                and var searchQueryTemplateVM)
            {
                var queryParameters = (searchQueryTemplateVM as SearchQueryParameterTemplateViewModel).SearchQueryEntity.QueryParameters;
                _ = _messenger.Send<NavigationAppCoreFrameRequestMessage>(new (new (nameof(QueryEditPage), ("query", queryParameters))));
            }
        }


        private ReactiveCommand _StartSearchProcessCommand;
        public ReactiveCommand StartSearchProcessCommand
        {
            get { return _StartSearchProcessCommand; }
            private set { SetProperty(ref _StartSearchProcessCommand, value); }
        }

        void ExecuteStartSearchProcessCommand()
        {
            _messenger.Send<NavigationAppCoreFrameRequestMessage>(new(new(nameof(SearchRunningManagementPage), ("query", SearchQueryVM.SeriaizeParameters()))));
        }

        bool CanExecuteStartSearchProcessCommand()
        {
            if (!SearchQueryVM.Targets.Any()) { return false; }


            return true;
        }
    }

    public class SearchFieldTypeSelectableItem : SelectableItem<SearchFieldType>
    {
        public SearchFieldTypeSelectableItem(SearchFieldType content, bool isSelected = true) : base(content, isSelected)
        {
        }
    }

    public class SelectableItem<T> : BindableBase
    {
        public SelectableItem(T content, bool isSelected = true)
        {
            Content = content;
            _isSelected = isSelected;
        }

        public T Content { get; }

        private bool _isSelected;
        public bool IsSelected
        {
            get { return _isSelected; }
            set { SetProperty(ref _isSelected, value); }
        }
    }

    public interface IJsonFilterViewModel : INotifyPropertyChanged
    {
        SearchFieldType FieldType { get; }
        JsonFilterComparison Comparison { get; }
        string VariableName { get; }
        object FromValue { get; set; }
        object ToValue { get; set; }

        bool IncludeLower { get; }

        bool IncludeUpper { get; }
    }

    public enum JsonFilterComparison
    {
        Equal,
        Range
    }

    public abstract class JsonFilterViewModelBase : BindableBase
    {
        private readonly JsonFilterComparison[] _Comparisons = Enum.GetValues(typeof(JsonFilterComparison)).Cast<JsonFilterComparison>().ToArray();
        private readonly Action<IJsonFilterViewModel> _onRemove;

        public JsonFilterComparison[] Comparisons => _Comparisons;

        public JsonFilterViewModelBase(Action<IJsonFilterViewModel> onRemove)
        {
            _onRemove = onRemove;
        }

        private DelegateCommand _RemoveCommand;
        public DelegateCommand RemoveCommand =>
            _RemoveCommand ?? (_RemoveCommand = new DelegateCommand(ExecuteRemoveCommand));

        void ExecuteRemoveCommand()
        {
            _onRemove(this as IJsonFilterViewModel);
        }
    }

    public class JsonFilterViewModel<T> : JsonFilterViewModelBase, IJsonFilterViewModel
    {
        public JsonFilterViewModel(Action<IJsonFilterViewModel> onRemove, string variableName, SearchFieldType fieldType, JsonFilterComparison comparison, T fromValue, T toValue, bool includeLower, bool includeUpper)
            : base(onRemove)
        {
            VariableName = variableName;
            FieldType = fieldType;
            _Comparison = comparison;
            _FromValue = fromValue;
            _ToValue = toValue;
            IncludeLower = includeLower;
            IncludeUpper = includeUpper;
        }

        public string VariableName { get; }
        public SearchFieldType FieldType { get; }

        private JsonFilterComparison _Comparison;
        public JsonFilterComparison Comparison
        {
            get { return _Comparison; }
            set { SetProperty(ref _Comparison, value); }
        }


        private T _FromValue;
        public T FromValue
        {
            get { return _FromValue; }
            set { SetProperty(ref _FromValue, value); }
        }

        private T _ToValue;
        public T ToValue
        {
            get { return _ToValue; }
            set { SetProperty(ref _ToValue, value); }
        }


        private bool _IncludeLower;
        public bool IncludeLower
        {
            get { return _IncludeLower; }
            set { SetProperty(ref _IncludeLower, value); }
        }

        private bool _IncludeUpper;
        public bool IncludeUpper
        {
            get { return _IncludeUpper; }
            set { SetProperty(ref _IncludeUpper, value); }
        }

        object IJsonFilterViewModel.FromValue
        {
            get { return _FromValue; }
            set { FromValue = (T)value; }
        }

        object IJsonFilterViewModel.ToValue
        {
            get { return _ToValue; }
            set { ToValue = (T)value; }
        }

        public void UpdateValidationStatus()
        {
            GetValidationInvalidMessageText();
        }

        protected virtual string GetValidationInvalidMessageText()
        {
            if (_Comparison == JsonFilterComparison.Equal)
            {
                if (_FromValue is null)
                {
                    return "".Translate();
                }
            }

            return null;
        }
    }



    public sealed class DateTimeOffsetJsonFilterViewModel : JsonFilterViewModel<DateTimeOffset?>
    {
        public DateTimeOffsetJsonFilterViewModel(Action<IJsonFilterViewModel> onRemove, string variableName, SearchFieldType fieldType, JsonFilterComparison comparison, DateTimeOffset? fromValue, DateTimeOffset? toValue, bool includeLower, bool includeUpper) 
            : base(onRemove, variableName, fieldType, comparison, fromValue, toValue, includeLower, includeUpper)
        {
            _FromDate = fromValue is not null and DateTimeOffset fromDate ? new DateTimeOffset(fromDate.Year, fromDate.Month, fromDate.Day, 0, 0, 0, fromDate.Offset) : DateTimeOffset.Now;
            _FromTime = fromValue is not null and DateTimeOffset fromTime ? new TimeSpan(fromTime.Hour, fromTime.Minute, 0) : default(TimeSpan);
            if (fromValue is not null)
            {
                RefreshFromValue();
            }

            _ToDate = toValue is not null and DateTimeOffset toDate ? new DateTimeOffset(toDate.Year, toDate.Month, toDate.Day, 0, 0, 0, toDate.Offset) : DateTimeOffset.Now;
            _ToTime = toValue is not null and DateTimeOffset toTime ? new TimeSpan(toTime.Hour, toTime.Minute, 0) : default(TimeSpan);
            if (toValue is not null)
            {
                RefreshToValue();
            }
        }

        private DateTimeOffset _FromDate;
        public DateTimeOffset FromDate
        {
            get { return _FromDate; }
            set
            {
                if (SetProperty(ref _FromDate, value))
                {
                    RefreshFromValue();
                }
            }
        }

        private TimeSpan _FromTime;
        public TimeSpan FromTime
        {
            get { return _FromTime; }
            set
            {
                if (SetProperty(ref _FromTime, value))
                {
                    RefreshFromValue();
                }
            }
        }

        void RefreshFromValue()
        {
            FromValue = new DateTimeOffset(_FromDate.Year, _FromDate.Month, _FromDate.Day, _FromTime.Hours, _FromTime.Minutes, _FromTime.Seconds, _FromDate.Offset);
        }


        private DateTimeOffset _ToDate;
        public DateTimeOffset ToDate
        {
            get { return _ToDate; }
            set
            {
                if (SetProperty(ref _ToDate, value))
                {
                    RefreshToValue();
                }
            }
        }

        private TimeSpan _ToTime;
        public TimeSpan ToTime
        {
            get { return _ToTime; }
            set
            {
                if (SetProperty(ref _ToTime, value))
                {
                    RefreshToValue();
                }
            }
        }

        void RefreshToValue()
        {
            ToValue = new DateTimeOffset(_ToDate.Year, _ToDate.Month, _ToDate.Day, _ToTime.Hours, _ToTime.Minutes, _ToTime.Seconds, _ToDate.Offset);
        }
    }


    public sealed class IntJsonFilterViewModel : JsonFilterViewModel<int?>
    {
        public IntJsonFilterViewModel(Action<IJsonFilterViewModel> onRemove, string variableName, SearchFieldType fieldType, JsonFilterComparison comparison, int? fromValue, int? toValue, bool includeLower, bool includeUpper) 
            : base(onRemove, variableName, fieldType, comparison, fromValue, toValue, includeLower, includeUpper)
        {
        }

        private int _BindFromValue;
        public int BindFromValue
        {
            get { return _BindFromValue; }
            set 
            {
                if (SetProperty(ref _BindFromValue, value))
                {
                    FromValue = value;
                }
            }
        }

        private int _BindToValue;
        public int BindToValue
        {
            get { return _BindToValue; }
            set
            {
                if (SetProperty(ref _BindToValue, value))
                {
                    ToValue = value;
                }
            }
        }
    }

    public sealed class TimeSpanJsonFilterViewModel : JsonFilterViewModel<int?>
    {
        public TimeSpanJsonFilterViewModel(Action<IJsonFilterViewModel> onRemove, string variableName, SearchFieldType fieldType, JsonFilterComparison comparison, int? fromValue, int? toValue, bool includeLower, bool includeUpper) 
            : base(onRemove, variableName, fieldType, comparison, fromValue, toValue, includeLower, includeUpper)
        {
            if (fromValue is not null and int fromTimeSeconds)
            {
                TimeSpan fromTime = TimeSpan.FromSeconds(fromTimeSeconds);
                _FromHours = fromTime.Hours;
                _FromMinutes = fromTime.Minutes;
                _FromSeconds = fromTime.Seconds;
            }

            if (toValue is not null and int toTimeSeconds)
            {
                TimeSpan toTime = TimeSpan.FromSeconds(toTimeSeconds);
                _ToHours = toTime.Hours;
                _ToMinutes = toTime.Minutes;
                _ToSeconds = toTime.Seconds;
            }
        }


        private int _FromHours;
        public int FromHours
        {
            get { return _FromHours; }
            set
            {
                if (SetProperty(ref _FromHours, value))
                {
                    RefreshFromValue();
                }
            }
        }

        private int _FromMinutes;
        public int FromMinutes
        {
            get { return _FromMinutes; }
            set
            {
                if (SetProperty(ref _FromMinutes, value))
                {
                    RefreshFromValue();
                }
            }
        }

        private int _FromSeconds;
        public int FromSeconds
        {
            get { return _FromSeconds; }
            set
            {
                if (SetProperty(ref _FromSeconds, value))
                {
                    RefreshFromValue();
                }
            }
        }


        private void RefreshFromValue()
        {
            FromValue = (int)new TimeSpan(_FromHours, _FromMinutes, _FromSeconds).TotalSeconds;
        }


        private int _ToHours;
        public int ToHours
        {
            get { return _ToHours; }
            set
            {
                if (SetProperty(ref _ToHours, value))
                {
                    RefreshToValue();
                }
            }
        }

        private int _ToMinutes;
        public int ToMinutes
        {
            get { return _ToMinutes; }
            set
            {
                if (SetProperty(ref _ToMinutes, value))
                {
                    RefreshToValue();
                }
            }
        }

        private int _ToSeconds;
        public int ToSeconds
        {
            get { return _ToSeconds; }
            set
            {
                if (SetProperty(ref _ToSeconds, value))
                {
                    RefreshToValue();
                }
            }
        }


        private void RefreshToValue()
        {
            ToValue = (int)new TimeSpan(_ToHours, _ToMinutes, _ToSeconds).TotalSeconds;
        }
    }


    public sealed class StringJsonFilterViewModel : JsonFilterViewModel<string>
    {
        public StringJsonFilterViewModel(Action<IJsonFilterViewModel> onRemove, string variableName, SearchFieldType fieldType, string value, string[] suggestionItems = null) 
            : base(onRemove, variableName, fieldType, JsonFilterComparison.Equal, value, value, false, false)
        {
            SuggestionItems = suggestionItems;
        }


        private JsonFilterComparison _Comparison;
        new public JsonFilterComparison Comparison
        {
            get { return _Comparison; }
            private set
            {
                Guard.IsTrue(value != JsonFilterComparison.Equal, "");
                SetProperty(ref _Comparison, value);
            }
        }

        public string[] SuggestionItems { get; }
    }
}
