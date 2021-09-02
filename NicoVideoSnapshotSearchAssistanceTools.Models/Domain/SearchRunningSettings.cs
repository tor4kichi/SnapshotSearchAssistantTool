using NicoVideoSnapshotSearchAssistanceTools.Models.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NicoVideoSnapshotSearchAssistanceTools.Models.Domain
{
    public sealed class SearchRunningSettings : FlagsRepositoryBase
    {
        public DateTime? RetryAvairableTime
        {
            get => Read(default(DateTime?));
            set => Save(value);
        }

        public string PrevServerErrorMessage
        {
            get => Read(default(string));
            set => Save(value);
        }


        public SplitSearchPlanData SplitSearchPlanData
        {
            get => Read(default(SplitSearchPlanData));
            set => Save(value);
        }
    }


    public class SplitSearchPlanData
    {
        public SplitSearchPlan[] Plans { get; set; }
    }

    public struct SplitSearchPlan
    {
        public DateTimeOffset StartTime { get; set; }
        public DateTimeOffset EndTime { get; set; }

        public bool IncludeStartTime { get; set; }
        public bool IncludeEndTime { get; set; }

        public int RangeItemsCount { get; set; }
    }


}
