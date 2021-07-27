using Microsoft.Toolkit.Mvvm.Messaging;
using NiconicoToolkit.SnapshotSearch;
using NicoVideoSnapshotSearchAssistanceTools.Models.Domain;
using NicoVideoSnapshotSearchAssistanceTools.Presentation.ViewModels.Messages;
using NicoVideoSnapshotSearchAssistanceTools.Presentation.Views;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Navigation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NicoVideoSnapshotSearchAssistanceTools.Presentation.ViewModels
{
    public sealed class QueryManagementPageViewModel : ViewModelBase
    {
        private readonly IMessenger _messenger;
        private readonly SearchQueryDatabase_V0 _searchQueryDatabase;

        public QueryManagementPageViewModel() { }

        public QueryManagementPageViewModel(
            IMessenger messenger,
            SearchQueryDatabase_V0 searchQueryDatabase)
        {
            _messenger = messenger;
            _searchQueryDatabase = searchQueryDatabase;

            SearchQueryItems = new ObservableCollection<SearchQueryTemplateViewModel>();
        }

        public ObservableCollection<SearchQueryTemplateViewModel> SearchQueryItems { get; }

        public override void OnNavigatedTo(INavigationParameters parameters)
        {
            if (!SearchQueryItems.Any())
            {
                var items = _searchQueryDatabase.ReadAllItems();
                foreach (var item in items)
                {
                    try
                    {
                        SearchQueryItems.Add(new SearchQueryTemplateViewModel(item.Id, item.Title, item.QueryParameters, _messenger));
                    }
                    catch
                    {

                    }
                }
            }
        }
    }


}
