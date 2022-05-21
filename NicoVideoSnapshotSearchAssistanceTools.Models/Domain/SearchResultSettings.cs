using NiconicoToolkit.SnapshotSearch.Filters;
using NicoVideoSnapshotSearchAssistanceTools.Models.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace NicoVideoSnapshotSearchAssistanceTools.Models.Domain
{
    public sealed class SearchResultSettings : FlagsRepositoryBase
    {
        const string DefaultScoreCulcExpressionText = "V + C + M * 20 + L";

        public static ScoringSettingItem CreateDefaultScoringSettingItem()
        {
            return new ScoringSettingItem()
            {
                Title = "ランキング",
                ScoreCulsExpressionText = "V + C + M * 20 + L",
            };
        }


        public SearchResultSettings()
        {
            _ScoringSettings = Read(default(ScoringSettingItem[]), nameof(ScoringSettings)) ?? 
                new[] { CreateDefaultScoringSettingItem() };

            _CurrentScoringSettingIndex = Read(0, nameof(CurrentScoringSettingIndex));
            _ResultFilters = Read(default(SearchResultFilterItem[]), nameof(ResultFilters));
        }


        private int _CurrentScoringSettingIndex;
        public int CurrentScoringSettingIndex
        {
            get { return _CurrentScoringSettingIndex; }
            set { SetProperty(ref _CurrentScoringSettingIndex, value); }
        }


        private ScoringSettingItem[] _ScoringSettings;
        public ScoringSettingItem[] ScoringSettings
        {
            get => _ScoringSettings;
            set => SetProperty(ref _ScoringSettings, value);
        }


        private SearchResultFilterItem[] _ResultFilters;
        public SearchResultFilterItem[] ResultFilters
        {
            get { return _ResultFilters; }
            set { SetProperty(ref _ResultFilters, value); }
        }
    }


    public sealed class ScoringSettingItem
    {
        public string Title { get; set; }

        public ScoringVariableDeclaration[] VariableDeclarations { get; set; } = new ScoringVariableDeclaration[0];
        public string ScoreCulsExpressionText { get; set; }
    }

    public sealed class ScoringVariableDeclaration
    {
        public string VariableName { get; set; }

        public string ExpressionText { get; set; }
    }


    public sealed class SearchResultFilterItem
    {
        public string FieldName { get; set; }
        public string Comparison { get; set; } 
        public object Value { get; set; }

        public SimpleFilterComparison GetSimpleFilterComparison()
        {
            return Enum.Parse<SimpleFilterComparison>(Comparison);
        }

        public JsonElement GetValueAsJsonElement()
        {
            return (JsonElement)Value;
        }

        public int GetValueAsInt()
        {
            return GetValueAsJsonElement().GetInt32();
        }

        public TimeSpan GetValueAsTimeSpan()
        {
            return TimeSpan.FromSeconds(GetValueAsJsonElement().GetDouble());
        }

        public DateTimeOffset GetValueAsDateTimeOffset()
        {
            return GetValueAsJsonElement().GetDateTimeOffset();
        }

        public string GetValueAsString()
        {
            return GetValueAsJsonElement().GetString();
        }
    }
}
