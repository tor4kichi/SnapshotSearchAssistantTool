using NicoVideoSnapshotSearchAssistanceTools.Models.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
}
