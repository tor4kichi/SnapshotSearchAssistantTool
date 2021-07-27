using NicoVideoSnapshotSearchAssistanceTools.Models.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NicoVideoSnapshotSearchAssistanceTools.Models.Domain
{
    public sealed class SearchQueryEditSettings : FlagsRepositoryBase
    {
        public string EdittingQueryParameters
        {
            get => Read<string>(string.Empty);
            set => Save(value);
        }
    }
}
