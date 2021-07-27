using LiteDB;
using NicoVideoSnapshotSearchAssistanceTools.Models.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NicoVideoSnapshotSearchAssistanceTools.Models.Domain
{

    public sealed class SearchQueryDatabase_V0 : LiteDBServiceBase<SearchQueryEntity_V0>
    {
        public SearchQueryDatabase_V0(ILiteDatabase liteDatabase) : base(liteDatabase)
        {
        }
    }


    public sealed class SearchQueryEntity_V0
    {
        [BsonId(autoId: true)]
        public Guid Id { get; set; }

        public string Title { get; set; }

        public string QueryParameters { get; set; }

    }
}
