using I18NPortable;
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
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using System.IO;
using System.Globalization;
using System.Text.Json.Serialization;
using Windows.System;

namespace NicoVideoSnapshotSearchAssistanceTools.Presentation.ViewModels
{
    public sealed class ScoreSnapshotResultFilterViewModel : SearchResultFilterViewModel<int, SimpleFilterComparison>
    {
        public ScoreSnapshotResultFilterViewModel(Action<object> onRemove, SimpleFilterComparison comparison, int score) 
            : base(onRemove, SearchResultPageViewModel.CustomFieldTypeName_Score, comparison, score)
        {
        }

        public override bool Compare(SnapshotItemViewModel item)
        {
            long rightValue = item.Score ?? 0;
            return Comparison switch
            {
                SimpleFilterComparison.Equal => rightValue == Value,
                SimpleFilterComparison.GreaterThan => rightValue > Value,
                SimpleFilterComparison.GreaterThanOrEqual => rightValue >= Value,
                SimpleFilterComparison.LessThan => rightValue < Value,
                SimpleFilterComparison.LessThenOrEqual => rightValue <= Value,
                _ => throw new NotSupportedException(),
            };
        }
    }

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

                return Filters.All(filter => filter.Compare(item));
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
                CustomFieldTypeNames,
                SearchFieldTypeExtensions.FieldTypes.Select(x => x.ToString())
                )
                .ToDictionary(x => x, x => true);

        public ReactiveProperty<bool> NowRefreshing { get; } = new ReactiveProperty<bool>();
        public ReactiveProperty<bool> IsAutoRefreshEnabled { get; } = new ReactiveProperty<bool>(true);
        public ReactiveProperty<bool> CanRefresh { get; } = new ReactiveProperty<bool>();

        public ObservableCollection<ISearchResultViewModel> Filters { get; } = new ObservableCollection<ISearchResultViewModel>();

        private DelegateCommand<string> _AddSimpleFilterCommand;
        public DelegateCommand<string> AddSimpleFilterCommand =>
            _AddSimpleFilterCommand ?? (_AddSimpleFilterCommand = new DelegateCommand<string>(ExecuteAddSimpleFilterCommand));

        void ExecuteAddSimpleFilterCommand(string parameter)
        {
            if (Enum.TryParse<SearchFieldType>(parameter, out var fieldType))
            {                
                var type = fieldType.GetAttrubute<SearchFieldTypeAttribute>().Type;
                if (type == typeof(int))
                {
                    if (fieldType == SearchFieldType.LengthSeconds)
                    {
                        Filters.Add(new TimeSpanSearchResultFilterViewModel(RemoveSimpleFilterItem, fieldType, SimpleFilterComparison.GreaterThan, TimeSpan.Zero));
                    }
                    else
                    {
                        Filters.Add(new IntSearchResultFilterViewModel(RemoveSimpleFilterItem, fieldType, SimpleFilterComparison.GreaterThan, 0));
                    }
                }
                else if (type == typeof(DateTimeOffset))
                {
                    var time = DateTimeOffset.Now;
                    time -= time.TimeOfDay;
                    Filters.Add(new DateTimeOffsetSearchResultFilterViewModel(RemoveSimpleFilterItem, fieldType, SimpleFilterComparison.GreaterThan, time));
                }
                else if (type == typeof(string))
                {
                    Filters.Add(new StringSearchResultFilterViewModel(RemoveSimpleFilterItem, fieldType, "", StringComparerMethod.Contains));
                }
            }
            else if (parameter is CustomFieldTypeName_Score)
            {
                Filters.Add(new ScoreSnapshotResultFilterViewModel(RemoveSimpleFilterItem, SimpleFilterComparison.GreaterThan, 0));
            }
        }

        private void RemoveSimpleFilterItem(object filterVM)
        {
            NowRefreshing.Value = true;
            try
            {
                Filters.Remove(filterVM as ISearchResultViewModel);
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
            CanRefresh.Value = false;

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
                            Filters.Add(new TimeSpanSearchResultFilterViewModel(RemoveSimpleFilterItem, fieldType, filter.GetSimpleFilterComparison(), filter.GetValueAsTimeSpan()));
                        }
                        else
                        {
                            Filters.Add(new IntSearchResultFilterViewModel(RemoveSimpleFilterItem, fieldType, filter.GetSimpleFilterComparison(), filter.GetValueAsInt()));
                        }
                    }
                    else if (type == typeof(DateTimeOffset))
                    {
                        var time = filter.GetValueAsDateTimeOffset();
                        time -= time.TimeOfDay;
                        Filters.Add(new DateTimeOffsetSearchResultFilterViewModel(RemoveSimpleFilterItem, fieldType, filter.GetSimpleFilterComparison(), time));
                    }
                    else if (type == typeof(string))
                    {
                        if (fieldType is SearchFieldType.Genre or SearchFieldType.GenreKeyword)
                        {
                            Filters.Add(new StringSearchResultFilterViewModel(RemoveSimpleFilterItem, fieldType, filter.GetValueAsString(), Enum.Parse<StringComparerMethod>(filter.Comparison)));
                        }
                        else
                        {
                            Filters.Add(new StringSearchResultFilterViewModel(RemoveSimpleFilterItem, fieldType, filter.GetValueAsString(), Enum.Parse<StringComparerMethod>(filter.Comparison)));
                        }
                    }
                }
                else if (filter.FieldName == CustomFieldTypeName_Score)
                {
                    Filters.Add(new ScoreSnapshotResultFilterViewModel(RemoveSimpleFilterItem, filter.GetSimpleFilterComparison(), filter.GetValueAsInt()));
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
                if (x is ISearchResultViewModel filter)
                {
                    if (filter.Value is TimeSpan timeSpan)
                    {
                        return new SearchResultFilterItem()
                        {
                            FieldName = filter.FieldType.ToString(),
                            Comparison = filter.Comparison.ToString(),
                            Value = ((TimeSpan)filter.Value).TotalSeconds,
                        };
                    }
                    else
                    {
                        return new SearchResultFilterItem()
                        {
                            FieldName = filter.FieldType.ToString(),
                            Comparison = filter.Comparison.ToString(),
                            Value = filter.Value,
                        };
                    }                    
                }
                else if (x is ScoreSnapshotResultFilterViewModel scoreFilter)
                {
                    return new SearchResultFilterItem()
                    {
                        FieldName = scoreFilter.FieldType,
                        Comparison = scoreFilter.Comparison.ToString(),
                        Value = scoreFilter.Value,
                    };
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


                static string LocalizeKey(string key)
                {
                    if (key == CustomFieldTypeName_Index)
                    {
                        return "CustomFieldType_Index".Translate();
                    }
                    else if (key == CustomFieldTypeName_Score)
                    {
                        return "CustomFieldType_Score".Translate();
                    }
                    else if (Enum.TryParse<SearchFieldType>(key, out var fieldType))
                    {
                        return fieldType.Translate();
                    }
                    else
                    {
                        throw new NotSupportedException(key);
                    }
                }

                ExportSettingItems = 
                    VisibilityMap.Where(x => x.Value).Select(x => new ExportSettingItem(x.Key, LocalizeKey(x.Key)))
                    .ToArray();

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

                
                new[]
                {
                    Filters.ObserveElementPropertyChanged().ToUnit(),
                    Filters.CollectionChangedAsObservable().ToUnit()
                }
                .Merge()
                .Subscribe(x => 
                {
                    if (IsAutoRefreshEnabled.Value)
                    {
                        SaveFilterSettings();
                        ItemsView.RefreshFilter();
                        CanRefresh.Value = false;
                    }
                    else
                    {
                        CanRefresh.Value = true;
                    }
                })
                .AddTo(_navigationDisposable);
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



        #region Export

        private DelegateCommand _ExportCommand;
        public DelegateCommand ExportCommand =>
            _ExportCommand ?? (_ExportCommand = new DelegateCommand(ExecuteExportCommand));


        public ExportSupportType[] ExportSupportTypeItems { get; } = 
            Enum.GetValues(typeof(ExportSupportType)).Cast<ExportSupportType>().ToArray();

        private ExportSupportType _ExportSupportType;
        public ExportSupportType ExportSupportType
        {
            get { return _ExportSupportType; }
            set { SetProperty(ref _ExportSupportType, value); }
        }



        private ExportSettingItem[] _ExportSettingItems;
        public ExportSettingItem[] ExportSettingItems
        {
            get { return _ExportSettingItems; }
            private set { SetProperty(ref _ExportSettingItems, value); }
        }

        async void ExecuteExportCommand()
        {
            // 出力先を取得
            if (ExportSupportType is ExportSupportType.Json)
            {
                var picker = new FileSavePicker()
                {
                    CommitButtonText = "Export".Translate(),
                    SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                    DefaultFileExtension = ".json",
                    SuggestedFileName = $"{ResultMeta.SnapshotVersion.ToString("d").Replace('/', '-')}.json"

                };

                picker.FileTypeChoices.Add("Json", new[] { ".json" });

                if (await picker.PickSaveFileAsync() is not null and StorageFile file)
                {
                    await ExportWithJsonAsync(file, _navigationCts.Token);

                    await Launcher.LaunchFolderPathAsync(Path.GetDirectoryName(file.Path), new FolderLauncherOptions() { ItemsToSelect = { file } });
                }
            }
            else if (ExportSupportType is ExportSupportType.Csv)
            {
                var picker = new FileSavePicker()
                {
                    CommitButtonText = "Export".Translate(),
                    SuggestedStartLocation = PickerLocationId.DocumentsLibrary,
                    DefaultFileExtension = ".csv",
                    SuggestedFileName = $"{ResultMeta.SnapshotVersion.ToString("d").Replace('/', '-')}.csv"
                };

                picker.FileTypeChoices.Add("CSV", new[] { ".csv" });

                if (await picker.PickSaveFileAsync() is not null and StorageFile file)
                {
                    await ExportWithCsvAsync(file, _navigationCts.Token);

                    await Launcher.LaunchFolderPathAsync(Path.GetDirectoryName(file.Path), new FolderLauncherOptions() { ItemsToSelect = { file } });
                }
            }
        }

        async Task ExportWithJsonAsync(StorageFile file, CancellationToken ct)
        {
            using (var writeStream = await file.OpenStreamForWriteAsync())
            {
                writeStream.SetLength(0);
                JsonSerializerOptions options = new() 
                {
                    WriteIndented = true,
                    Converters = 
                    {
                        new SnapshotItemViewModelJsonConverter(ExportSettingItems),
                    }
                };

                await JsonSerializer.SerializeAsync(writeStream, ItemsView.Cast<SnapshotItemViewModel>(), options: options, cancellationToken: ct);
            }
        }

        async Task ExportWithCsvAsync(StorageFile file, CancellationToken ct)
        {
            var config = new CsvHelper.Configuration.CsvConfiguration(CultureInfo.CurrentCulture)
            {
                MissingFieldFound = null,
            };


            Action<SerializeSnapshotItem, SnapshotItemViewModel>[] items = ExportSettingItems.Where(x => x.IsExport)
                .Select(x => (Action<SerializeSnapshotItem, SnapshotItemViewModel>)(x.Key switch
                {
                    CustomFieldTypeName_Index => (SerializeSnapshotItem dest, SnapshotItemViewModel source) => dest.Index = source.Index,
                    CustomFieldTypeName_Score => (SerializeSnapshotItem dest, SnapshotItemViewModel source) => dest.Score = source.Score,
                    nameof(SearchFieldType.ContentId) => (SerializeSnapshotItem dest, SnapshotItemViewModel source) => dest.ContentId = source.ContentId,
                    nameof(SearchFieldType.Title) => (SerializeSnapshotItem dest, SnapshotItemViewModel source) => dest.Title = source.Title,
                    nameof(SearchFieldType.Description) => (SerializeSnapshotItem dest, SnapshotItemViewModel source) => dest.Description = source.Description,
                    nameof(SearchFieldType.UserId) => (SerializeSnapshotItem dest, SnapshotItemViewModel source) => dest.UserId = source.UserId,
                    nameof(SearchFieldType.ChannelId) => (SerializeSnapshotItem dest, SnapshotItemViewModel source) => dest.ChannelId = source.ChannelId,
                    nameof(SearchFieldType.ViewCounter) => (SerializeSnapshotItem dest, SnapshotItemViewModel source) => dest.ViewCounter = source.ViewCounter,
                    nameof(SearchFieldType.MylistCounter) => (SerializeSnapshotItem dest, SnapshotItemViewModel source) => dest.MylistCounter = source.MylistCounter,
                    nameof(SearchFieldType.LikeCounter) => (SerializeSnapshotItem dest, SnapshotItemViewModel source) => dest.LikeCounter = source.LikeCounter,
                    nameof(SearchFieldType.LengthSeconds) => (SerializeSnapshotItem dest, SnapshotItemViewModel source) => dest.LengthSeconds = source.LengthSeconds,
                    nameof(SearchFieldType.ThumbnailUrl) => (SerializeSnapshotItem dest, SnapshotItemViewModel source) => dest.ThumbnailUrl = source.ThumbnailUrl,
                    nameof(SearchFieldType.StartTime) => (SerializeSnapshotItem dest, SnapshotItemViewModel source) => dest.StartTime = source.StartTime,
                    nameof(SearchFieldType.LastResBody) => (SerializeSnapshotItem dest, SnapshotItemViewModel source) => dest.LastResBody = source.LastResBody,
                    nameof(SearchFieldType.CommentCounter) => (SerializeSnapshotItem dest, SnapshotItemViewModel source) => dest.CommentCounter = source.CommentCounter,
                    nameof(SearchFieldType.LastCommentTime) => (SerializeSnapshotItem dest, SnapshotItemViewModel source) => dest.LastCommentTime = source.LastCommentTime,
                    nameof(SearchFieldType.CategoryTags) => (SerializeSnapshotItem dest, SnapshotItemViewModel source) => dest.CategoryTags = source.CategoryTags,
                    nameof(SearchFieldType.Tags) => (SerializeSnapshotItem dest, SnapshotItemViewModel source) => dest.Tags = source.Tags,
                    nameof(SearchFieldType.Genre) => (SerializeSnapshotItem dest, SnapshotItemViewModel source) => dest.Genre = source.Genre,
                    _ => throw new NotSupportedException(x.Key),
                }))
                .ToArray();

            SerializeSnapshotItem temp = new SerializeSnapshotItem();
            using (var fileStream = await file.OpenStreamForWriteAsync())
            using (var textWriter = new StreamWriter(fileStream))
            using (var cvsWriter = new CsvHelper.CsvWriter(textWriter, config))
            {
                fileStream.SetLength(0);

                cvsWriter.WriteHeader<SerializeSnapshotItem>();
                cvsWriter.NextRecord();
                foreach (var item in ItemsView.Cast<SnapshotItemViewModel>())
                {
                    foreach (var mappingAct in items)
                    {
                        mappingAct(temp, item);
                    }

                    cvsWriter.WriteRecord(temp);
                    cvsWriter.NextRecord();
                }
            }
        }

        #endregion Export
    }


    public class SnapshotItemViewModelJsonConverter : JsonConverter<SnapshotItemViewModel>
    {
        private readonly ExportSettingItem[] _settings;

        public SnapshotItemViewModelJsonConverter(ExportSettingItem[] settings)
        {
            _settings = settings;
        }

        

        public override SnapshotItemViewModel Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            throw new NotImplementedException();
        }

        public override void Write(Utf8JsonWriter writer, SnapshotItemViewModel value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();

            foreach (var item in _settings)
            {
                if (item.IsExport is false)
                {
                    continue;
                }

                static void WriteNullableNumber(Utf8JsonWriter writer, string propertyName, long? value)
                {
                    if (value.HasValue)
                        writer.WriteNumber(propertyName, value.Value);
                    else
                        writer.WriteNull(propertyName);
                }

                static void WriteNullableDateTimeOffset(Utf8JsonWriter writer, string propertyName, DateTimeOffset? value)
                {
                    if (value.HasValue)
                        writer.WriteString(propertyName, value.Value);
                    else
                        writer.WriteNull(propertyName);
                }

                switch (item.Key)
                {
                    case nameof(SearchFieldType.ContentId): writer.WriteString(nameof(SearchFieldType.ContentId), value.ContentId); break;
                    case nameof(SearchFieldType.Title): writer.WriteString(nameof(SearchFieldType.Title), value.Title); break;
                    case nameof(SearchFieldType.Description): writer.WriteString(nameof(SearchFieldType.Description), value.Description); break;
                    case nameof(SearchFieldType.UserId): WriteNullableNumber(writer, nameof(SearchFieldType.UserId), value.UserId); break;
                    case nameof(SearchFieldType.ViewCounter): WriteNullableNumber(writer, nameof(SearchFieldType.ViewCounter), value.ViewCounter); break;
                    case nameof(SearchFieldType.MylistCounter): WriteNullableNumber(writer, nameof(SearchFieldType.MylistCounter), value.MylistCounter); break;
                    case nameof(SearchFieldType.LikeCounter): WriteNullableNumber(writer, nameof(SearchFieldType.LikeCounter), value.LikeCounter); break;
                    case nameof(SearchFieldType.LengthSeconds): WriteNullableNumber(writer, nameof(SearchFieldType.LengthSeconds), value.LengthSeconds); break;
                    case nameof(SearchFieldType.ThumbnailUrl): writer.WriteString(nameof(SearchFieldType.ThumbnailUrl), value.ThumbnailUrl.OriginalString); break;
                    case nameof(SearchFieldType.StartTime): WriteNullableDateTimeOffset(writer, nameof(SearchFieldType.StartTime), value.StartTime); break;
                    case nameof(SearchFieldType.LastResBody): writer.WriteString(nameof(SearchFieldType.LastResBody), value.LastResBody); break;
                    case nameof(SearchFieldType.CommentCounter): WriteNullableNumber(writer, nameof(SearchFieldType.CommentCounter), value.CommentCounter); break;
                    case nameof(SearchFieldType.LastCommentTime): WriteNullableDateTimeOffset(writer, nameof(SearchFieldType.LastCommentTime), value.LastCommentTime); break;
                    case nameof(SearchFieldType.CategoryTags): writer.WriteString(nameof(SearchFieldType.CategoryTags), value.CategoryTags); break;
                    case nameof(SearchFieldType.Tags): writer.WriteString(nameof(SearchFieldType.Tags), value.Tags); break;
                    case nameof(SearchFieldType.Genre): writer.WriteString(nameof(SearchFieldType.Genre), value.Genre); break;
                }

            }

            writer.WriteEndObject();
        }
    }

    public enum ExportSupportType
    {
        Json,
        Csv,
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

        [JsonIgnore]
        public int Index { get; }

        private long? _Score;
        public long? Score
        {
            get { return _Score; }
            set { SetProperty(ref _Score, value); }
        }

        public string ContentId => _snapshotVideoItem.ContentId;
        public string Title => _snapshotVideoItem.Title;
        public string Description => _snapshotVideoItem.Description;
        public long? UserId => _snapshotVideoItem.UserId.HasValue ? (long)_snapshotVideoItem.UserId.Value.RawId : null;
        public string ChannelId => _snapshotVideoItem.ChannelId;
        public long? ViewCounter => _snapshotVideoItem.ViewCounter;
        public long? MylistCounter => _snapshotVideoItem.MylistCounter;
        public long? LikeCounter => _snapshotVideoItem.LikeCounter;
        public long? LengthSeconds => _snapshotVideoItem.LengthSeconds;
        public Uri ThumbnailUrl => _snapshotVideoItem.ThumbnailUrl;
        public DateTimeOffset? StartTime => _snapshotVideoItem.StartTime;
        public string LastResBody => _snapshotVideoItem.LastResBody;
        public long? CommentCounter => _snapshotVideoItem.CommentCounter;
        public DateTimeOffset? LastCommentTime => _snapshotVideoItem.LastCommentTime;
        public string CategoryTags => _snapshotVideoItem.CategoryTags;
        public string Tags => _snapshotVideoItem.Tags;
        public string Genre => _snapshotVideoItem.Genre;


        string[] _Tags_Separated;
        
        [JsonIgnore]
        public string[] Tags_Separated => _Tags_Separated ??= Tags?.Split(' ') ?? new string[0];
    }

    public sealed class SerializeSnapshotItem
    {
        public int? Index { get; set; }
        public long? Score { get; set; }
        public string ContentId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public long? UserId { get; set; }
        public string ChannelId { get; set; }
        public long? ViewCounter { get; set; }
        public long? MylistCounter { get; set; }
        public long? LikeCounter { get; set; }
        public long? LengthSeconds { get; set; }
        public Uri ThumbnailUrl { get; set; }
        public DateTimeOffset? StartTime { get; set; }
        public string LastResBody { get; set; }
        public long? CommentCounter { get; set; }
        public DateTimeOffset? LastCommentTime { get; set; }
        public string CategoryTags { get; set; }
        public string Tags { get; set; }
        public string Genre { get; set; }
    }

    public sealed class ExportSettingItem
    {
        public ExportSettingItem(string key, string label)
        {
            Key = key;
            Label = label;
        }

        public string Key { get; }

        public string Label { get; }

        public bool IsExport { get; set; } = true;
    }
}
