using NicoVideoSnapshotSearchAssistanceTools.Models.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NicoVideoSnapshotSearchAssistanceTools.Models.Domain
{
    public sealed class ApplicationInternalSettings : FlagsRepositoryBase
    {
        public string LastOpenPageName
        {
            get => Read(default(string));
            private set => Save(value);
        }

        public KeyValuePair<string, string>[] LastOpenPageParameterKeyValues
        {
            get => Read(default(KeyValuePair<string, string>[]));
            private set => Save(value);
        }        

        public void SaveLastOpenPage(string lastOpenPageName, params (string key, string value)[] parameters)
        {
            StringBuilder sb = new();
            bool isFirst = true;
            foreach (var p in parameters)
            {
                if (!isFirst)
                {
                    sb.Append("&");
                }

                sb.Append(p.key)
                    .Append('=')
                    .Append(p.value);
               
                isFirst = false;
            }

            LastOpenPageName = lastOpenPageName;
            LastOpenPageParameterKeyValues = parameters.Select(x => new KeyValuePair<string, string>(x.key, x.value)).ToArray();
        }


        public string ContextQueryParameter
        {
            get => Read(default(string));
            set => Save(value);
        }
    }
}
