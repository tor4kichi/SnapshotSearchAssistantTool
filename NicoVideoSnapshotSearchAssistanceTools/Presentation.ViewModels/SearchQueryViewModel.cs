using Microsoft.Toolkit.Diagnostics;
using Microsoft.Toolkit.Mvvm.Messaging;
using NiconicoToolkit;
using NiconicoToolkit.SnapshotSearch;
using NiconicoToolkit.SnapshotSearch.Filters;
using NiconicoToolkit.SnapshotSearch.JsonFilters;
using NicoVideoSnapshotSearchAssistanceTools.Models.Domain;
using NicoVideoSnapshotSearchAssistanceTools.Presentation.ViewModels.Messages;
using NicoVideoSnapshotSearchAssistanceTools.Presentation.Views;
using Prism.Commands;
using Prism.Mvvm;
using Reactive.Bindings.Extensions;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace NicoVideoSnapshotSearchAssistanceTools.Presentation.ViewModels
{

    public class SearchQueryViewModel : BindableBase, ISearchQuery
    {
        internal SearchQueryViewModel(string queryParameters)
        {
            (_keyword, _sort, _fields, _targets, _Filters) = SearchQuarySerializeHelper.ParseQueryParameters(queryParameters);
        }

        #region Query Parameter Property

        private string _keyword;
        public string Keyword
        {
            get { return _keyword; }
            set { SetProperty(ref _keyword, value); }
        }


        private SearchFieldType[] _targets;
        public SearchFieldType[] Targets
        {
            get { return _targets; }
            set { SetProperty(ref _targets, value); }
        }

        public IObservable<bool> GetValidTargetsObservable()
        {
            return this.ObserveProperty(x => x.Targets).Select(x => x.Any());
        }


        private SearchSort _sort;
        public SearchSort Sort
        {
            get { return _sort; }
            set { SetProperty(ref _sort, value); }
        }

        public IObservable<bool> GetValidSortObservable()
        {
            return this.ObserveProperty(x => x.Sort).Select(x => x != null);
        }


        private SearchFieldType[] _fields;
        public SearchFieldType[] Fields
        {
            get { return _fields; }
            set { SetProperty(ref _fields, value); }
        }

        private ISearchFilter _Filters;
        public ISearchFilter Filters
        {
            get { return _Filters; }
            set { SetProperty(ref _Filters, value); }
        }

        #endregion Query Parameter Property


        public string SeriaizeParameters(string context = null)
        {
            return SearchQuarySerializeHelper.SeriaizeParameters(Keyword, Targets, Sort, Fields, Filters, context);
        }



    }
}
