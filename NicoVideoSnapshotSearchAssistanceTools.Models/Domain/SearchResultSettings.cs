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
        public SearchResultSettings()
        {
            _ViewCounterWeightingFactor = Read(1.0, nameof(ViewCounterWeightingFactor));
            _MylistCounterWeightingFactor = Read(1.0, nameof(MylistCounterWeightingFactor));
            _CommentCounterWeightingFactor = Read(1.0, nameof(CommentCounterWeightingFactor));
            _LikeCounterWeightingFactor = Read(1.0, nameof(LikeCounterWeightingFactor));
        }

        private double _ViewCounterWeightingFactor;
        public double ViewCounterWeightingFactor
        {
            get { return _ViewCounterWeightingFactor; }
            set 
            {
                if (SetProperty(ref _ViewCounterWeightingFactor, value))
                {
                    Save(value);
                }        
            }
        }

        private double _MylistCounterWeightingFactor;
        public double MylistCounterWeightingFactor
        {
            get { return _MylistCounterWeightingFactor; }
            set 
            {
                if (SetProperty(ref _MylistCounterWeightingFactor, value))
                {
                    Save(value);
                }
            }
        }

        private double _CommentCounterWeightingFactor;
        public double CommentCounterWeightingFactor
        {
            get { return _CommentCounterWeightingFactor; }
            set
            {
                if (SetProperty(ref _CommentCounterWeightingFactor, value))
                {
                    Save(value);
                }
            }
        }

        private double _LikeCounterWeightingFactor;
        public double LikeCounterWeightingFactor
        {
            get { return _LikeCounterWeightingFactor; }
            set
            {
                if (SetProperty(ref _LikeCounterWeightingFactor, value))
                {
                    Save(value);
                }
            }
        }
    }
}
