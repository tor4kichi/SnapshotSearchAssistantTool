using Microsoft.Toolkit.Diagnostics;
using NiconicoToolkit.SnapshotSearch;
using NiconicoToolkit.SnapshotSearch.Filters;
using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NicoVideoSnapshotSearchAssistanceTools.Presentation.ViewModels
{
    public interface ISearchResultViewModel : INotifyPropertyChanged
    {
        /// <summary>
        /// SearchResultPage向けのフィルタ機能に用いる
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        bool Compare(SnapshotItemViewModel item);

        string FieldType { get; }
        string Comparison { get; set; }

        object Value { get; set; }
    }

    public abstract class SearchResultFilterViewModelBase : BindableBase
    {
        private readonly Action<object> _onRemove;

        public SearchResultFilterViewModelBase(Action<object> onRemove)
        {
            _onRemove = onRemove;
        }

        private DelegateCommand _RemoveCommand;
        public DelegateCommand RemoveCommand =>
            _RemoveCommand ?? (_RemoveCommand = new DelegateCommand(ExecuteRemoveCommand));

        void ExecuteRemoveCommand()
        {
            _onRemove(this);
        }

        public abstract bool Compare(SnapshotItemViewModel item);
    }


    public abstract class SearchResultFilterViewModel<TValue, TComparison> : SearchResultFilterViewModelBase, ISearchResultViewModel where TComparison : struct, Enum
    {
        private readonly static TComparison[] _Comparisons = Enum.GetValues(typeof(TComparison)).Cast<TComparison>().ToArray();
        public TComparison[] Comparisons => _Comparisons;

        public SearchResultFilterViewModel(Action<object> onRemove, string searchFieldType, TComparison comparison, TValue value)
            : base(onRemove)
        {
            FieldType = searchFieldType;
            _Comparison = comparison;
            _value = value;
        }

        public string FieldType { get; }

        private TComparison _Comparison;
        public TComparison Comparison
        {
            get { return _Comparison; }
            set { SetProperty(ref _Comparison, value); }
        }

        string ISearchResultViewModel.Comparison
        {
            get => _Comparison.ToString();
            set => _Comparison = Enum.Parse<TComparison>(value);
        }

        private TValue _value;
        public TValue Value
        {
            get { return _value; }
            set { SetProperty(ref _value, value); }
        }

        object ISearchResultViewModel.Value
        {
            get { return _value; }
            set { Value = (TValue)value; }
        }
    }

    public class DateTimeOffsetSearchResultFilterViewModel : SearchResultFilterViewModel<DateTimeOffset, SimpleFilterComparison>
    {
        public DateTimeOffsetSearchResultFilterViewModel(Action<object> onRemove, SearchFieldType searchFieldType, SimpleFilterComparison comparison, DateTimeOffset value)
            : base(onRemove, searchFieldType.ToString(), comparison, value)
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

        public override bool Compare(SnapshotItemViewModel item)
        {
            DateTimeOffset rightValue = FieldType switch
            {
                nameof(SearchFieldType.StartTime) => item.StartTime.Value,
                nameof(SearchFieldType.LastResBody) => item.LastCommentTime.Value,
                _ => throw new InvalidOperationException(),
            };

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

    public class IntSearchResultFilterViewModel : SearchResultFilterViewModel<int, SimpleFilterComparison>
    {
        public IntSearchResultFilterViewModel(Action<object> onRemove, SearchFieldType searchFieldType, SimpleFilterComparison comparison, int value)
            : base(onRemove, searchFieldType.ToString(), comparison, value)
        {
        }

        public override bool Compare(SnapshotItemViewModel item)
        {
            long rightValue = FieldType switch
            {
                nameof(SearchFieldType.ViewCounter) => item.ViewCounter.Value,
                nameof(SearchFieldType.MylistCounter) => item.MylistCounter.Value,
                nameof(SearchFieldType.LikeCounter) => item.LikeCounter.Value,
                nameof(SearchFieldType.CommentCounter) => item.CommentCounter.Value,
                _ => throw new InvalidOperationException(),
            };

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

    public class TimeSpanSearchResultFilterViewModel : SearchResultFilterViewModel<TimeSpan, SimpleFilterComparison>
    {
        public TimeSpanSearchResultFilterViewModel(Action<object> onRemove, SearchFieldType searchFieldType, SimpleFilterComparison comparison, TimeSpan value)
            : base(onRemove, searchFieldType.ToString(), comparison, value)
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

        public override bool Compare(SnapshotItemViewModel item)
        {
            TimeSpan rightValue = FieldType switch
            {
                nameof(SearchFieldType.LengthSeconds) => TimeSpan.FromSeconds(item.LengthSeconds.Value),
                _ => throw new InvalidOperationException(),
            };

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

    public enum StringComparerMethod
    {
        Contains,
        NotContains,
    }

    public class StringSearchResultFilterViewModel : SearchResultFilterViewModel<string, StringComparerMethod>
    {
        public StringSearchResultFilterViewModel(Action<object> onRemove, SearchFieldType searchFieldType, string value, StringComparerMethod comparerMethod, string[] suggestionItems = null)
            : base(onRemove, searchFieldType.ToString(), comparerMethod, value)
        {
            SuggestionItems = suggestionItems;
        }

        public string[] SuggestionItems { get; }

        public override bool Compare(SnapshotItemViewModel item)
        {
            if (string.IsNullOrWhiteSpace(Value)) { return true; }

            if (FieldType is nameof(SearchFieldType.TagsExact))
            {
                if (Comparison == StringComparerMethod.Contains)
                {
                    return item.Tags_Separated.Contains(Value);
                }
                else
                {
                    return item.Tags_Separated.Contains(Value) is false;
                }
            }
            else
            {
                string rightValue = FieldType switch
                {
                    nameof(SearchFieldType.Title) => item.Title,
                    nameof(SearchFieldType.Description) => item.Description,
                    nameof(SearchFieldType.LastResBody) => item.LastResBody,
                    nameof(SearchFieldType.CategoryTags) => item.CategoryTags,
                    nameof(SearchFieldType.Tags) => item.Tags,
                    nameof(SearchFieldType.Genre) => item.Genre,
                    nameof(SearchFieldType.GenreKeyword) => item.Genre,
                    _ => throw new InvalidOperationException(),
                };

                if (Comparison == StringComparerMethod.Contains)
                {
                    return rightValue.Contains(Value);
                }
                else
                {
                    return rightValue.Contains(Value) is false;
                }
                
            }
        }
    }
}
