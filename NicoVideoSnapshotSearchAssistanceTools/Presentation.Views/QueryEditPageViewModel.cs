using Microsoft.Toolkit.Diagnostics;
using Microsoft.Toolkit.Mvvm.Input;
using Microsoft.Toolkit.Mvvm.Messaging;
using Microsoft.Toolkit.Mvvm.Messaging.Messages;
using NiconicoToolkit;
using NiconicoToolkit.SnapshotSearch;
using NiconicoToolkit.SnapshotSearch.Filters;
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
    public interface ISimpleFilterViewModel : INotifyPropertyChanged
    {
        SearchFieldType FieldType { get; }
        SimpleFilterComparison Comparison { get; set; }

        object Value { get; set; }
    }

    public abstract class SimpleFilterViewModelBase : BindableBase
    {
        private readonly SimpleFilterComparison[] _Comparisons = Enum.GetValues(typeof(SimpleFilterComparison)).Cast<SimpleFilterComparison>().ToArray();
        private readonly Action<ISimpleFilterViewModel> _onRemove;

        public SimpleFilterComparison[] Comparisons => _Comparisons;

        public SimpleFilterViewModelBase(Action<ISimpleFilterViewModel> onRemove)
        {
            _onRemove = onRemove;
        }

        private DelegateCommand _RemoveCommand;
        public DelegateCommand RemoveCommand =>
            _RemoveCommand ?? (_RemoveCommand = new DelegateCommand(ExecuteRemoveCommand));

        void ExecuteRemoveCommand()
        {
            _onRemove(this as ISimpleFilterViewModel);
        }
    }

    public abstract class SimpleFilterViewModel<T> : SimpleFilterViewModelBase, ISimpleFilterViewModel
    {
        public SimpleFilterViewModel(Action<ISimpleFilterViewModel> onRemove, SearchFieldType searchFieldType, SimpleFilterComparison comparison, T value)
            : base(onRemove)
        {
            FieldType = searchFieldType;
            _Comparison = comparison;
            _value = value;
        }

        public SearchFieldType FieldType { get; }

        private SimpleFilterComparison _Comparison;
        public SimpleFilterComparison Comparison
        {
            get { return _Comparison; }
            set { SetProperty(ref _Comparison, value); }
        }

        private T _value;
        public T Value
        {
            get { return _value; }
            set { SetProperty(ref _value, value); }
        }

        object ISimpleFilterViewModel.Value
        {
            get { return _value; }
            set { Value = (T)value; }
        }
    }

    public class DateTimeOffsetSimpleFilterViewModel : SimpleFilterViewModel<DateTimeOffset>
    {
        public DateTimeOffsetSimpleFilterViewModel(Action<ISimpleFilterViewModel> onRemove, SearchFieldType searchFieldType, SimpleFilterComparison comparison, DateTimeOffset value)
            : base(onRemove, searchFieldType, comparison, value)
        {
            _Date = new DateTimeOffset(value.Year, value.Month, value.Day, 0, 0, 0, value.Offset);
            _Time = new TimeSpan(value.Hour, value.Minute, 0);
            RefreshValue();
        }

        private DateTimeOffset _Date;
        public DateTimeOffset Date
        {
            get { return _Date; }
            set 
            {
                if (SetProperty(ref _Date, value))
                {
                    RefreshValue();
                }
            }
        }

        private TimeSpan _Time;
        public TimeSpan Time
        {
            get { return _Time; }
            set 
            {
                if (SetProperty(ref _Time, value))
                {
                    RefreshValue();
                }
            }
        }

        void RefreshValue()
        {
            Value = new DateTimeOffset(_Date.Year, _Date.Month, _Date.Day, _Time.Hours, _Time.Minutes, _Time.Seconds, _Date.Offset);
        }
    }

    public class IntSimpleFilterViewModel : SimpleFilterViewModel<int>
    {
        public IntSimpleFilterViewModel(Action<ISimpleFilterViewModel> onRemove, SearchFieldType searchFieldType, SimpleFilterComparison comparison, int value)
            : base(onRemove, searchFieldType, comparison, value)
        {
        }
    }

    public class TimeSpanSimpleFilterViewModel : SimpleFilterViewModel<TimeSpan>
    {
        public TimeSpanSimpleFilterViewModel(Action<ISimpleFilterViewModel> onRemove, SearchFieldType searchFieldType, SimpleFilterComparison comparison, TimeSpan value)
            : base(onRemove, searchFieldType, comparison, value)
        {
            _Hours = value.Hours;
            _Minutes = value.Minutes;
            _Seconds = value.Seconds;
        }

        private int _Hours;
        public int Hours
        {
            get { return _Hours; }
            set
            {
                if (SetProperty(ref _Hours, value))
                {
                    RefreshValue();
                }
            }
        }

        private int _Minutes;
        public int Minutes
        {
            get { return _Minutes; }
            set 
            {
                if (SetProperty(ref _Minutes, value))
                {
                    RefreshValue();
                }
            }
        }

        private int _Seconds;
        public int Seconds
        {
            get { return _Seconds; }
            set
            {
                if (SetProperty(ref _Seconds, value))
                {
                    RefreshValue();
                }
            }
        }


        private void RefreshValue()
        {
            Value = new TimeSpan(_Hours, _Minutes, _Seconds);
        }
    }

    public class StringSimpleFilterViewModel : SimpleFilterViewModel<string>
    {
        public StringSimpleFilterViewModel(Action<ISimpleFilterViewModel> onRemove, SearchFieldType searchFieldType, string value, string[] suggestionItems = null)
            : base(onRemove, searchFieldType, SimpleFilterComparison.Equal, value)
        {
            SuggestionItems = suggestionItems;
        }

        private SimpleFilterComparison _Comparison;
        new public SimpleFilterComparison Comparison
        {
            get { return _Comparison; }
            private set 
            {
                SetProperty(ref _Comparison, value); 
            }
        }

        public string[] SuggestionItems { get; }
    }

    public sealed class QueryEditPageViewModel : ViewModelBase
    {
        private readonly IMessenger _messenger;
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
            SearchQueryDatabase_V0 searchQueryDatabase,
            SearchQueryEditSettings searchQueryEditSettings,
            ApplicationInternalSettings applicationInternalSettings
            )
        {
            _messenger = messenger;
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

        private bool _useJsonFilter;
        public bool UseJsonFilter
        {
            get { return _useJsonFilter; }
            set { SetProperty(ref _useJsonFilter, value); }
        }


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
                        SearchQueryVM = new SearchQueryViewModel(queryParameters, _messenger);

                        _applicationInternalSettings.SaveLastOpenPage(nameof(QueryEditPage));

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
                                else if (simpleFilter is  CompareSimpleSearchFilter<int> numberFilter)
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
                        }

                        new []
                        {
                            this.ObserveProperty(x => x.UseJsonFilter, isPushCurrentValueAtFirst: false).ToUnit(),
                            SimpleFilters.CollectionChangedAsObservable().ToUnit(),
                            SimpleFilters.ObserveElementPropertyChanged().ToUnit(),
                            

                            // TODO: JsonFilterの条件が変わった場合のObservableをここに追加
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
                }
            }
            catch
            {
                _navigationDisposables.Dispose();
                _navigationDisposables = null;
            }
        }

        private void RefreshFilterConditions()
        {
            if (!UseJsonFilter)
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
            throw new NotImplementedException();
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

}
