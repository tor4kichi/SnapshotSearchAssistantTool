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


        public async Task SaveSplitSearchPlanDataAsync(SplitSearchPlanData data)
        {
            await SaveFileAsync(data, nameof(SplitSearchPlanData));
        }

        public async Task<SplitSearchPlanData> ReadSplitSearchPlanDataAsync()
        {
            return await ReadFileAsync(default(SplitSearchPlanData), nameof(SplitSearchPlanData));
        }

        public async Task ClearSplitSearchPlanDataAsync()
        {
            await SaveFileAsync(default(SplitSearchPlanData), nameof(SplitSearchPlanData));
        }
    }


    public class SplitSearchPlanData
    {
        public DateTimeOffset SnapshotVersion { get; set; }
        public string SearchQueryId { get; set; }

        public SplitSearchPlan[] Plans { get; set; }
    }

    public struct SplitSearchPlan
    {
        public DateTimeOffset StartTime { get; set; }
        public DateTimeOffset EndTime { get; set; }

        public bool IncludeStartTime { get; set; }
        public bool IncludeEndTime { get; set; }

        public long RangeItemsCount { get; set; }
    }


}
