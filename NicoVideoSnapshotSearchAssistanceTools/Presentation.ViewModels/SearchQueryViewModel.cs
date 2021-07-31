using Microsoft.Toolkit.Diagnostics;
using Microsoft.Toolkit.Mvvm.Messaging;
using NiconicoToolkit;
using NiconicoToolkit.SnapshotSearch;
using NiconicoToolkit.SnapshotSearch.Filters;
using NiconicoToolkit.SnapshotSearch.JsonFilters;
using NicoVideoSnapshotSearchAssistanceTools.Presentation.ViewModels.Messages;
using NicoVideoSnapshotSearchAssistanceTools.Presentation.Views;
using Prism.Commands;
using Prism.Mvvm;
using Reactive.Bindings.Extensions;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace NicoVideoSnapshotSearchAssistanceTools.Presentation.ViewModels
{

    public class SearchQueryViewModel : BindableBase
    {
        private readonly IMessenger _messenger;

        internal SearchQueryViewModel(string queryParameters, IMessenger messenger)
        {
            _messenger = messenger;
            ParseQueryParameters(queryParameters);
        }

        #region Query Parameter Property

        private string _keyword;
        public string Keyword
        {
            get { return _keyword; }
            set { SetProperty(ref _keyword, value); }
        }


        private SearchFieldType[] _targets;
        public SearchFieldType[] Targets
        {
            get { return _targets; }
            set { SetProperty(ref _targets, value); }
        }

        public IObservable<bool> GetValidTargetsObservable()
        {
            return this.ObserveProperty(x => x.Targets).Select(x => x.Any());
        }


        private SearchSort _sort;
        public SearchSort Sort
        {
            get { return _sort; }
            set { SetProperty(ref _sort, value); }
        }

        public IObservable<bool> GetValidSortObservable()
        {
            return this.ObserveProperty(x => x.Sort).Select(x => x != null);
        }


        private SearchFieldType[] _fields;
        public SearchFieldType[] Fields
        {
            get { return _fields; }
            set { SetProperty(ref _fields, value); }
        }


        private ISearchFilter _Filters;
        public ISearchFilter Filters
        {
            get { return _Filters; }
            set { SetProperty(ref _Filters, value); }
        }


        private string _Context;
        public string Context
        {
            get { return _Context; }
            set { SetProperty(ref _Context, value); }
        }

        public IObservable<bool> GetValidContextObservable()
        {
            return this.ObserveProperty(x => x.Context).Select(x => !string.IsNullOrWhiteSpace(x));
        }

        #endregion Query Parameter Property


        #region Commands

        private DelegateCommand _OpenEditPageCommand;
        public DelegateCommand OpenEditPageCommand =>
            _OpenEditPageCommand ?? (_OpenEditPageCommand = new DelegateCommand(ExecuteOpenEditPageCommand));

        void ExecuteOpenEditPageCommand()
        {
            _messenger.Send(new NavigationAppCoreFrameRequestMessage(new(nameof(QueryEditPage), ("query", SeriaizeParameters()))));
        }


        private DelegateCommand _OpenSnapshotResultHistoryPageCommand;
        public DelegateCommand OpenSnapshotResultHistoryPageCommand =>
            _OpenSnapshotResultHistoryPageCommand ?? (_OpenSnapshotResultHistoryPageCommand = new DelegateCommand(ExecuteOpenSnapshotResultHistoryPageCommand));

        void ExecuteOpenSnapshotResultHistoryPageCommand()
        {

        }


        private DelegateCommand _OpenLatestSnapshotResultPageCommand;
        public DelegateCommand OpenLatestSnapshotResultPageCommand =>
            _OpenLatestSnapshotResultPageCommand ?? (_OpenLatestSnapshotResultPageCommand = new DelegateCommand(ExecuteOpenLatestSnapshotResultPageCommand));

        void ExecuteOpenLatestSnapshotResultPageCommand()
        {

        }


        #endregion Commands


        static readonly Dictionary<string, SearchFieldType> _fieldTypeDescriptionMap =
            Enum.GetValues(typeof(SearchFieldType)).Cast<SearchFieldType>()
            .ToDictionary(x => x.GetDescription());
        static readonly Dictionary<string, SimpleFilterComparison> _conditionTypeDescriptionMap =
            Enum.GetValues(typeof(SimpleFilterComparison)).Cast<SimpleFilterComparison>()
            .ToDictionary(x => x.GetDescription());
        private void ParseQueryParameters(string q)
        {
            var nvc = HttpUtility.ParseQueryString(q);

            _keyword = nvc.Get(SearchConstants.QuaryParameter) ?? string.Empty;
            var sort = nvc.Get(SearchConstants.SortParameter);
            if (sort != null)
            {
                if (sort.StartsWith("-"))
                {
                    var sortFiledType = sort.Remove(0, 1);
                    if (_fieldTypeDescriptionMap.TryGetValue(sortFiledType, out var type))
                    {
                        _sort = new SearchSort(type, SearchSortOrder.Desc);
                    }
                    else
                    {
                        throw new NotSupportedException();
                    }
                }
                else if (sort.StartsWith("+"))
                {
                    var sortFiledType = sort.Remove(0, 1);
                    if (_fieldTypeDescriptionMap.TryGetValue(sortFiledType, out var type))
                    {
                        _sort = new SearchSort(type, SearchSortOrder.Asc);
                    }
                    else
                    {
                        throw new NotSupportedException();
                    }
                }
                else
                {
                    if (_fieldTypeDescriptionMap.TryGetValue(sort, out var type))
                    {
                        _sort = new SearchSort(type, SearchSortOrder.Desc);
                    }
                    else
                    {
                        throw new NotSupportedException();
                    }
                }
            }
            else
            {
                _sort = new SearchSort(SearchFieldType.StartTime, SearchSortOrder.Desc);
            }

            var fields = nvc.GetValues(SearchConstants.FieldsParameter);
            if (fields?.Any() ?? false)
            {
                _fields = fields[0].Split(',').Select(x => _fieldTypeDescriptionMap[x]).ToArray();

            }
            else
            {
                _fields = new SearchFieldType[0];
            }

            var targets = nvc.GetValues(SearchConstants.TargetsParameter);
            if (targets?.Any() ?? false)
            {
                _targets = targets[0].Split(',', StringSplitOptions.RemoveEmptyEntries).Select(x => _fieldTypeDescriptionMap[x]).ToArray();
            }
            else
            {
                _targets = new SearchFieldType[0];
            }

            var filters = nvc.AllKeys.Where(x => x.StartsWith(SearchConstants.FiltersParameter));
            if (filters.Any())
            {
                CompositeSearchFilter compositeSearchFilter = new CompositeSearchFilter();
                foreach (var filter in filters)
                {
                    var value = nvc.Get(filter);
                    var split = filter.Split(new char[] { '[', ']' }, StringSplitOptions.RemoveEmptyEntries);
                    var field = _fieldTypeDescriptionMap[split[1]];
                    var condition = _conditionTypeDescriptionMap[split[2]];

                    var targetTypeAttr = field.GetAttrubute<SearchFieldTypeAttribute>();
                    Guard.IsFalse(targetTypeAttr.IsDefaultAttribute(), nameof(targetTypeAttr.IsDefaultAttribute));

                    var targetType = targetTypeAttr.Type;
                    Dictionary<SearchFieldType, int> containsCountMap = new();
                    if (targetType == typeof(int))
                    {
                        compositeSearchFilter.AddCompareFilter(field, int.Parse(value), condition);
                    }
                    else if (targetType == typeof(string))
                    {
                        compositeSearchFilter.AddCompareFilter(field, value, condition);
                    }
                    else if (targetType == typeof(DateTimeOffset))
                    {
                        compositeSearchFilter.AddCompareFilter(field, DateTimeOffset.Parse(value), condition);
                    }
                    else
                    {
                        throw new NotSupportedException();
                    }
                }

                Filters = compositeSearchFilter;
            }

            var jsonFilters = nvc.Get(SearchConstants.JsonFilterParameter);
            if (jsonFilters != null)
            {
                var data = JsonSerializer.Deserialize<IJsonSearchFilterData>(jsonFilters);
                Filters = RecursiveBuildJsonSearchFilter(data);
            }

            Context = nvc.Get(SearchConstants.ContextParameter) ?? string.Empty;
        }

        static IJsonSearchFilter RecursiveBuildJsonSearchFilter(IJsonSearchFilterData data)
        {
            if (data is OrJsonFilterData orFilter)
            {
                return new OrJsonFilter(orFilter.Filters.Select(x => RecursiveBuildJsonSearchFilter(x as IJsonSearchFilterData)));
            }
            else if (data is AndJsonFilterData andFilter)
            {
                return new AndJsonFilter(andFilter.Filters.Select(x => RecursiveBuildJsonSearchFilter(x as IJsonSearchFilterData)));
            }
            else if (data is NotJsonFilterData notFilter)
            {
                return new NotJsonFilter(RecursiveBuildJsonSearchFilter(notFilter.Filter as IJsonSearchFilterData));
            }
            else if (data is RangeJsonFilterData rangeFilter)
            {
                var fieldType = _fieldTypeDescriptionMap[rangeFilter.Field];
                var targetTypeAttr = fieldType.GetAttrubute<SearchFieldTypeAttribute>();
                Guard.IsFalse(targetTypeAttr.IsDefaultAttribute(), nameof(targetTypeAttr.IsDefaultAttribute));

                var targetType = targetTypeAttr.Type;
                if (targetType == typeof(int))
                {
                    object to = rangeFilter.To != null ? int.Parse(rangeFilter.To) : null;
                    object from = rangeFilter.From != null ? int.Parse(rangeFilter.From) : null;
                    return new RangeJsonFilter<int>(fieldType, from, to, rangeFilter.IncludeLower ?? true, rangeFilter.IncludeUpper ?? true);
                }
                else if (targetType == typeof(string))
                {
                    return new RangeJsonFilter<string>(fieldType, rangeFilter.From, rangeFilter.To, rangeFilter.IncludeLower ?? true, rangeFilter.IncludeUpper ?? true);
                }
                else if (targetType == typeof(DateTime))
                {
                    object to = rangeFilter.To != null ? DateTime.Parse(rangeFilter.To) : null;
                    object from = rangeFilter.From != null ? DateTime.Parse(rangeFilter.From) : null;
                    return new RangeJsonFilter<DateTime>(fieldType, from, to, rangeFilter.IncludeLower ?? true, rangeFilter.IncludeUpper ?? true);
                }
                else
                {
                    throw new NotSupportedException();
                }
            }
            else if (data is EqualJsonFilterData equalFilter)
            {
                var fieldType = _fieldTypeDescriptionMap[equalFilter.Field];
                var targetTypeAttr = fieldType.GetAttrubute<SearchFieldTypeAttribute>();
                Guard.IsFalse(targetTypeAttr.IsDefaultAttribute(), nameof(targetTypeAttr.IsDefaultAttribute));

                var targetType = targetTypeAttr.Type;
                if (targetType == typeof(int))
                {
                    return new EqaulJsonFilter<int>(fieldType, int.Parse(equalFilter.Value));
                }
                else if (targetType == typeof(string))
                {
                    return new EqaulJsonFilter<string>(fieldType, equalFilter.Value);
                }
                else if (targetType == typeof(DateTime))
                {
                    return new EqaulJsonFilter<DateTime>(fieldType, DateTime.Parse(equalFilter.Value));
                }
                else
                {
                    throw new NotSupportedException();
                }
            }
            else
            {
                throw new NotSupportedException();
            }
        }

        public string SeriaizeParameters()
        {
            return SeriaizeParameters(Keyword, Targets, Sort, Fields, Filters, Context);
        }


        public static string SeriaizeParameters(
            string keyword, 
            SearchFieldType[] targets, 
            SearchSort sort, 
            SearchFieldType[] fields, 
            ISearchFilter searchFilter,
            string context
            )
        {
            var nvc = new NameValueCollection()
            {
                { SearchConstants.QuaryParameter, keyword ?? string.Empty },
                { SearchConstants.TargetsParameter, targets.ToQueryString() },
                { SearchConstants.SortParameter, sort.ToString() },
                { SearchConstants.ContextParameter, context },
            };

            if (fields is not null)
            {
                nvc.Add(SearchConstants.FieldsParameter, fields.ToQueryString());
            }

            if (searchFilter != null)
            {

                var filters = searchFilter.GetFilterKeyValues(new());
                foreach (var f in filters)
                {
                    nvc.Add(f.Key, f.Value);
                }
            }

            StringBuilder sb = new();
            bool isFirst = true;
            foreach (string key in nvc.Keys)
            {
                if (string.IsNullOrWhiteSpace(key)) continue;

                string[] values = nvc.GetValues(key);
                if (values == null) continue;

                foreach (string value in values)
                {
                    if (!isFirst) sb.Append("&");

                    sb.Append(Uri.EscapeDataString(key));
                    sb.Append("=");
                    sb.Append(Uri.EscapeDataString(value));

                    isFirst = false;
                }
            }



            return sb.ToString();
        }



    }
}
