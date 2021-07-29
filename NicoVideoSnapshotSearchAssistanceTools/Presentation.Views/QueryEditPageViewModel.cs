using Microsoft.Toolkit.Mvvm.Input;
using Microsoft.Toolkit.Mvvm.Messaging;
using Microsoft.Toolkit.Mvvm.Messaging.Messages;
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
using System.Diagnostics;
using System.Linq;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NicoVideoSnapshotSearchAssistanceTools.Presentation.ViewModels
{
    public sealed class QueryEditPageViewModel : ViewModelBase
    {
        private readonly IMessenger _messenger;
        private readonly SearchQueryDatabase_V0 _searchQueryDatabase;
        private readonly SearchQueryEditSettings _searchQueryEditSettings;

        public QueryEditPageViewModel(
            IMessenger messenger,
            SearchQueryDatabase_V0 searchQueryDatabase,
            SearchQueryEditSettings searchQueryEditSettings
            )
        {
            _messenger = messenger;
            _searchQueryDatabase = searchQueryDatabase;
            _searchQueryEditSettings = searchQueryEditSettings;
        }


        private SearchQueryViewModel _SearchQueryVM;
        public SearchQueryViewModel SearchQueryVM
        {
            get { return _SearchQueryVM; }
            set { SetProperty(ref _SearchQueryVM, value); }
        }


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

        private DateTimeOffset? _filterStartTime_From_Date;
        public DateTimeOffset? FilterStartTime_From_Date
        {
            get { return _filterStartTime_From_Date; }
            set { SetProperty(ref _filterStartTime_From_Date, value); }
        }

        private TimeSpan? _filterStartTime_From_Time;
        public TimeSpan? FilterStartTime_From_Time
        {
            get { return _filterStartTime_From_Time; }
            set { SetProperty(ref _filterStartTime_From_Time, value); }
        }

        private DelegateCommand _ClearFilterStartTimeFromCommand;
        public DelegateCommand ClearFilterStartTimeFromCommand =>
            _ClearFilterStartTimeFromCommand ?? (_ClearFilterStartTimeFromCommand = new DelegateCommand(ExecuteClearFilterStartTimeFromCommand));

        void ExecuteClearFilterStartTimeFromCommand()
        {
            FilterStartTime_From_Time = null;
            FilterStartTime_From_Date = null;
        }


        private bool _useJsonFilter;
        public bool UseJsonFilter
        {
            get { return _useJsonFilter; }
            set { SetProperty(ref _useJsonFilter, value); }
        }


        private DateTimeOffset? _filterStartTime_To_Date;
        public DateTimeOffset? FilterStartTime_To_Date
        {
            get { return _filterStartTime_To_Date; }
            set { SetProperty(ref _filterStartTime_To_Date, value); }
        }

        private TimeSpan? _filterStartTime_To_Time;
        public TimeSpan? FilterStartTime_To_Time
        {
            get { return _filterStartTime_To_Time; }
            set { SetProperty(ref _filterStartTime_To_Time, value); }
        }

        private DelegateCommand _ClearFilterStartTimeToCommand;
        public DelegateCommand ClearFilterStartTimeToCommand =>
            _ClearFilterStartTimeToCommand ?? (_ClearFilterStartTimeToCommand = new DelegateCommand(ExecuteClearFilterStartTimeToCommand));

        void ExecuteClearFilterStartTimeToCommand()
        {
            FilterStartTime_To_Time = null;
            FilterStartTime_To_Date = null;
        }



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

                        if (SearchQueryVM.Filters is CompositeSearchFilter compositeSearchFilter)
                        {
                            foreach (var simpleFilter in compositeSearchFilter.Filters)
                            {
                                if (simpleFilter is CompareSimpleSearchFilter<DateTimeOffset> dateTimeFilter)
                                {
                                    if (dateTimeFilter.FilterType == SearchFieldType.StartTime)
                                    {
                                        if (dateTimeFilter.Condition is SearchFilterCompareCondition.GreaterThan or SearchFilterCompareCondition.GreaterThanOrEqual)
                                        {
                                            var isEqauls = dateTimeFilter.Condition is SearchFilterCompareCondition.GreaterThanOrEqual;
                                            int year = dateTimeFilter.Value.Year;
                                            int month = dateTimeFilter.Value.Month;
                                            int day = dateTimeFilter.Value.Day;
                                            FilterStartTime_From_Date = new DateTimeOffset(year, month, day, 0, 0, 0, dateTimeFilter.Value.Offset);
                                            FilterStartTime_From_Time = new TimeSpan(dateTimeFilter.Value.Hour, dateTimeFilter.Value.Minute, 0);
                                        }
                                        else if (dateTimeFilter.Condition is SearchFilterCompareCondition.LessThan or SearchFilterCompareCondition.LessThenOrEqual)
                                        {
                                            var isEqauls = dateTimeFilter.Condition is SearchFilterCompareCondition.LessThenOrEqual;
                                            int year = dateTimeFilter.Value.Year;
                                            int month = dateTimeFilter.Value.Month;
                                            int day = dateTimeFilter.Value.Day;
                                            FilterStartTime_To_Date = new DateTimeOffset(year, month, day, 0, 0, 0, dateTimeFilter.Value.Offset);
                                            FilterStartTime_To_Time = new TimeSpan(dateTimeFilter.Value.Hour, dateTimeFilter.Value.Minute, 0);
                                        }
                                    }
                                    else if (dateTimeFilter.FilterType == SearchFieldType.LastCommentTime)
                                    {
                                        //
                                    }
                                }
                                else if (simpleFilter is  CompareSimpleSearchFilter<int> numberFilter)
                                {

                                }
                            }
                        }

                        new []
                        {
                            this.ObserveProperty(x => x.UseJsonFilter, isPushCurrentValueAtFirst: false).ToUnit(),
                            this.ObserveProperty(x => x.FilterStartTime_From_Date, isPushCurrentValueAtFirst: false).ToUnit(),
                            this.ObserveProperty(x => x.FilterStartTime_From_Time, isPushCurrentValueAtFirst: false).ToUnit(),
                            this.ObserveProperty(x => x.FilterStartTime_To_Date, isPushCurrentValueAtFirst: false).ToUnit(),
                            this.ObserveProperty(x => x.FilterStartTime_To_Time, isPushCurrentValueAtFirst: false).ToUnit(),

                            // TODO: JsonFilterの条件が変わった場合のObservableをここに追加
                        }
                        .Merge()
                        .Subscribe(_ => RefreshFilterConditions())
                        .AddTo(_navigationDisposables);

                        IsInvalidTargets = SearchQueryVM.GetValidTargetsObservable().Select(x => !x).ToReadOnlyReactivePropertySlim().AddTo(_navigationDisposables);
                        IsInvalidSort = SearchQueryVM.GetValidSortObservable().Select(x => !x).ToReadOnlyReactivePropertySlim().AddTo(_navigationDisposables);
                        IsInvalidContext = SearchQueryVM.GetValidContextObservable().Select(x => !x).ToReadOnlyReactivePropertySlim().AddTo(_navigationDisposables);

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
                if (FilterStartTime_From_Date is not null)
                {
                    FilterStartTime_From_Time ??= TimeSpan.Zero;
                    var date = FilterStartTime_From_Date.Value;
                    DateTimeOffset fromDateTime = new DateTimeOffset(date.Year, date.Month, date.Day, 0, 0, 0, date.Offset) + FilterStartTime_From_Time.Value;
                    compositeSearchFilter.AddCompareFilter(SearchFieldType.StartTime, fromDateTime, SearchFilterCompareCondition.GreaterThanOrEqual);
                }

                if (FilterStartTime_To_Date is not null)
                {
                    FilterStartTime_To_Time ??= TimeSpan.Zero;
                    var date = FilterStartTime_To_Date.Value;
                    DateTimeOffset toDateTime = new DateTimeOffset(date.Year, date.Month, date.Day, 0, 0, 0, date.Offset) + FilterStartTime_To_Time.Value;
                    compositeSearchFilter.AddCompareFilter(SearchFieldType.StartTime, toDateTime, SearchFilterCompareCondition.LessThenOrEqual);
                }

                SearchQueryVM.Filters = compositeSearchFilter;
            }
        }

        private void SaveEdittingQueryParameters()
        {
            _searchQueryEditSettings.EdittingQueryParameters = SearchQueryVM.SeriaizeParameters();

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
