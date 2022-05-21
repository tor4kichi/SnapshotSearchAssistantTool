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
    public interface ISimpleFilterViewModel : INotifyPropertyChanged
    {
        SearchFieldType FieldType { get; }
        SimpleFilterComparison Comparison { get; set; }

        object Value { get; set; }
    }

    public interface ISnapshorResultComparable : INotifyPropertyChanged
    {

        /// <summary>
        /// SearchResultPage向けのフィルタ機能に用いる
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        bool Compare(SnapshotItemViewModel item);
    }

    public abstract class SimpleFilterViewModelBase : BindableBase, ISnapshorResultComparable
    {
        private readonly static SimpleFilterComparison[] _Comparisons = Enum.GetValues(typeof(SimpleFilterComparison)).Cast<SimpleFilterComparison>().ToArray();
        private readonly Action<object> _onRemove;

        public SimpleFilterComparison[] Comparisons => _Comparisons;

        public SimpleFilterViewModelBase(Action<object> onRemove)
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


    public abstract class SimpleFilterViewModel<T> : SimpleFilterViewModelBase, ISimpleFilterViewModel
    {
        public SimpleFilterViewModel(Action<object> onRemove, SearchFieldType searchFieldType, SimpleFilterComparison comparison, T value)
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
        public DateTimeOffsetSimpleFilterViewModel(Action<object> onRemove, SearchFieldType searchFieldType, SimpleFilterComparison comparison, DateTimeOffset value)
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

        public override bool Compare(SnapshotItemViewModel item)
        {
            DateTimeOffset rightValue = FieldType switch
            {
                SearchFieldType.StartTime => item.StartTime.Value,
                SearchFieldType.LastResBody => item.LastCommentTime.Value,
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

    public class IntSimpleFilterViewModel : SimpleFilterViewModel<int>
    {
        public IntSimpleFilterViewModel(Action<object> onRemove, SearchFieldType searchFieldType, SimpleFilterComparison comparison, int value)
            : base(onRemove, searchFieldType, comparison, value)
        {
        }

        public override bool Compare(SnapshotItemViewModel item)
        {
            long rightValue = FieldType switch
            {
                SearchFieldType.ViewCounter => item.ViewCounter.Value,
                SearchFieldType.MylistCounter => item.MylistCounter.Value,
                SearchFieldType.LikeCounter => item.LikeCounter.Value,
                SearchFieldType.CommentCounter => item.CommentCounter.Value,
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

    public class TimeSpanSimpleFilterViewModel : SimpleFilterViewModel<TimeSpan>
    {
        public TimeSpanSimpleFilterViewModel(Action<object> onRemove, SearchFieldType searchFieldType, SimpleFilterComparison comparison, TimeSpan value)
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

        public override bool Compare(SnapshotItemViewModel item)
        {
            TimeSpan rightValue = FieldType switch
            {
                SearchFieldType.LengthSeconds => TimeSpan.FromSeconds(item.LengthSeconds.Value),
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

    public class StringSimpleFilterViewModel : SimpleFilterViewModel<string>
    {
        public StringSimpleFilterViewModel(Action<object> onRemove, SearchFieldType searchFieldType, string value, string[] suggestionItems = null)
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
                Guard.IsTrue(value != SimpleFilterComparison.Equal, "");
                SetProperty(ref _Comparison, value);
            }
        }

        public string[] SuggestionItems { get; }

        public override bool Compare(SnapshotItemViewModel item)
        {
            if (string.IsNullOrWhiteSpace(Value)) { return false; }

            if (FieldType is SearchFieldType.TagsExact)
            {
                return item.Tags_Separated.Contains(Value);
            }
            else
            {
                string rightValue = FieldType switch
                {
                    SearchFieldType.Title => item.Title,
                    SearchFieldType.Description => item.Description,
                    SearchFieldType.LastResBody => item.LastResBody,
                    SearchFieldType.CategoryTags => item.CategoryTags,
                    SearchFieldType.Tags => item.Tags,
                    SearchFieldType.Genre => item.Genre,
                    SearchFieldType.GenreKeyword => item.Genre,
                    _ => throw new InvalidOperationException(),
                };

                if (FieldType is SearchFieldType.GenreKeyword)
                {
                    return rightValue == Value;
                }
                else
                {
                    return rightValue.Contains(Value);
                }
            }
        }
    }
}
