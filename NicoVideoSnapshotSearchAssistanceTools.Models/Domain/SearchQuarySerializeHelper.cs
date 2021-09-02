using Microsoft.Toolkit.Diagnostics;
using NiconicoToolkit;
using NiconicoToolkit.SnapshotSearch;
using NiconicoToolkit.SnapshotSearch.Filters;
using NiconicoToolkit.SnapshotSearch.JsonFilters;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;

namespace NicoVideoSnapshotSearchAssistanceTools.Models.Domain
{
    public static class SearchQuarySerializeHelper
    {
        static readonly Dictionary<string, SearchFieldType> _fieldTypeDescriptionMap =
            Enum.GetValues(typeof(SearchFieldType)).Cast<SearchFieldType>()
            .ToDictionary(x => x.GetDescription());
        static readonly Dictionary<string, SimpleFilterComparison> _conditionTypeDescriptionMap =
            Enum.GetValues(typeof(SimpleFilterComparison)).Cast<SimpleFilterComparison>()
            .ToDictionary(x => x.GetDescription());
        public static (string keyword, SearchSort sort, SearchFieldType[] field, SearchFieldType[] targets, ISearchFilter filter) ParseQueryParameters(string q)
        {
            var nvc = HttpUtility.ParseQueryString(q);

            string keyword = nvc.Get(SearchConstants.QuaryParameter) ?? string.Empty;
            var sortStr = nvc.Get(SearchConstants.SortParameter);
            SearchSort sort = null;
            if (sortStr != null)
            {
                if (sortStr.StartsWith("-"))
                {
                    var sortFiledType = sortStr.Remove(0, 1);
                    if (_fieldTypeDescriptionMap.TryGetValue(sortFiledType, out var type))
                    {
                        sort = new SearchSort(type, SearchSortOrder.Desc);
                    }
                    else
                    {
                        throw new NotSupportedException();
                    }
                }
                else if (sortStr.StartsWith("+"))
                {
                    var sortFiledType = sortStr.Remove(0, 1);
                    if (_fieldTypeDescriptionMap.TryGetValue(sortFiledType, out var type))
                    {
                        sort = new SearchSort(type, SearchSortOrder.Asc);
                    }
                    else
                    {
                        throw new NotSupportedException();
                    }
                }
                else
                {
                    if (_fieldTypeDescriptionMap.TryGetValue(sortStr, out var type))
                    {
                        sort = new SearchSort(type, SearchSortOrder.Desc);
                    }
                    else
                    {
                        throw new NotSupportedException();
                    }
                }
            }
            else
            {
                sort = new SearchSort(SearchFieldType.StartTime, SearchSortOrder.Desc);
            }

            SearchFieldType[] fields = null;
            var fieldValues = nvc.GetValues(SearchConstants.FieldsParameter);
            if (fieldValues?.Any() ?? false)
            {
                fields = fieldValues[0].Split(',').Select(x => _fieldTypeDescriptionMap[x]).ToArray();

            }
            else
            {
                fields = new SearchFieldType[0];
            }

            SearchFieldType[] targets = null;
            var targetValues = nvc.GetValues(SearchConstants.TargetsParameter);
            if (targetValues?.Any() ?? false)
            {
                targets = targetValues[0].Split(',', StringSplitOptions.RemoveEmptyEntries).Select(x => _fieldTypeDescriptionMap[x]).ToArray();
            }
            else
            {
                targets = new SearchFieldType[0];
            }

            ISearchFilter searchFilter = null;
            var filterValues = nvc.AllKeys.Where(x => x.StartsWith(SearchConstants.FiltersParameter));
            if (filterValues.Any())
            {
                CompositeSearchFilter compositeSearchFilter = new CompositeSearchFilter();
                foreach (var filter in filterValues)
                {
                    var split = filter.Split(new char[] { '[', ']' }, StringSplitOptions.RemoveEmptyEntries);
                    var field = _fieldTypeDescriptionMap[split[1]];
                    var condition = _conditionTypeDescriptionMap.GetValueOrDefault(split[2]);

                    var targetTypeAttr = field.GetAttrubute<SearchFieldTypeAttribute>();
                    Guard.IsFalse(targetTypeAttr.IsDefaultAttribute(), nameof(targetTypeAttr.IsDefaultAttribute));

                    var targetType = targetTypeAttr.Type;
                    Dictionary<SearchFieldType, int> containsCountMap = new();
                    foreach (var value in nvc.GetValues(filter))
                    {
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
                }

                searchFilter = compositeSearchFilter;
            }

            var jsonFilters = nvc.Get(SearchConstants.JsonFilterParameter);
            if (jsonFilters != null)
            {
                var data = JsonSerializer.Deserialize<IJsonSearchFilterData>(jsonFilters, JsonFilterSerializeHelper.SerializerOptions);
                searchFilter = RecursiveBuildJsonSearchFilter(data);
            }

            return (keyword, sort, fields, targets, searchFilter);
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
                    return new RangeJsonFilter(fieldType, from, to, rangeFilter.IncludeLower ?? true, rangeFilter.IncludeUpper ?? true);
                }
                else if (targetType == typeof(string))
                {
                    return new RangeJsonFilter(fieldType, rangeFilter.From, rangeFilter.To, rangeFilter.IncludeLower ?? true, rangeFilter.IncludeUpper ?? true);
                }
                else if (targetType == typeof(DateTimeOffset))
                {
                    object to = rangeFilter.To != null ? DateTimeOffset.Parse(rangeFilter.To) : null;
                    object from = rangeFilter.From != null ? DateTimeOffset.Parse(rangeFilter.From) : null;
                    return new RangeJsonFilter(fieldType, from, to, rangeFilter.IncludeLower ?? true, rangeFilter.IncludeUpper ?? true);
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
                    return new EqualJsonFilter(fieldType, int.Parse(equalFilter.Value));
                }
                else if (targetType == typeof(string))
                {
                    return new EqualJsonFilter(fieldType, equalFilter.Value);
                }
                else if (targetType == typeof(DateTimeOffset))
                {
                    return new EqualJsonFilter(fieldType, DateTimeOffset.Parse(equalFilter.Value));
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
            };

            // Contextは本来必須だが、アプリの仕様上、Contextをアプリ設定として保持したいので
            // 空の場合にシリアライズから除外できるようにしている
            if (!string.IsNullOrWhiteSpace(context))
            {
                nvc.Add(SearchConstants.ContextParameter, context);
            }

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
