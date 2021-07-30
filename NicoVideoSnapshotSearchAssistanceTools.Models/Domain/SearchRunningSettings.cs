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
    }
}
