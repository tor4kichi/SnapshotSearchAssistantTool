using Microsoft.Toolkit.Mvvm.Messaging;
using Microsoft.Toolkit.Uwp.UI;
using NicoVideoSnapshotSearchAssistanceTools.Models.Domain;
using NicoVideoSnapshotSearchAssistanceTools.Presentation.ViewModels.Messages;
using NicoVideoSnapshotSearchAssistanceTools.Presentation.Views;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Navigation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NicoVideoSnapshotSearchAssistanceTools.Presentation.ViewModels
{
    public sealed class SearchHistoryPageViewModel : ViewModelBase
    {


        public AdvancedCollectionView HistoryItems { get; } = new AdvancedCollectionView();

        public SearchHistoryPageViewModel(
            IMessenger messenger,
            ApplicationInternalSettings applicationInternalSettings
            )
        {
            _messenger = messenger;
            _applicationInternalSettings = applicationInternalSettings;
        }

        CancellationTokenSource _navigationCts;
        private readonly IMessenger _messenger;
        private readonly ApplicationInternalSettings _applicationInternalSettings;

        public override async Task OnNavigatedToAsync(INavigationParameters parameters)
        {
            await base.OnNavigatedToAsync(parameters);

            _navigationCts = new CancellationTokenSource();
            var ct = _navigationCts.Token;

            using (HistoryItems.DeferRefresh())
            {
                HistoryItems.SortDescriptions.Clear();
                HistoryItems.SortDescriptions.Add(new (nameof(SearchQueryResultMetaViewModel.SnapshotVersion), SortDirection.Descending));
                await foreach (var meta in SnapshotResultFileHelper.GetAllQueryResultMetaAsync(ct))
                {
                    HistoryItems.Add(new SearchQueryResultMetaViewModel(meta));
                }

                if (parameters.TryGetValue("query", out string query))
                {
                    SetFilterByQueryId(query);
                }
            }

            _applicationInternalSettings.SaveLastOpenPage(nameof(SearchHistoryPage));
        }

        public override void OnNavigatedFrom(INavigationParameters parameters)
        {
            _navigationCts.Cancel();
            _navigationCts.Dispose();

            base.OnNavigatedFrom(parameters);
        }

        private void ClearFilter()
        {
            HistoryItems.Filter = null;
        }

        private void SetFilterByQueryId(string query)
        {
            HistoryItems.Filter = (item) => (item as SearchQueryResultMetaViewModel).SearchQueryId == query;
            //HistoryItems.RefreshFilter();
        }



        private DelegateCommand<SearchQueryViewModel> _OpenEditPageCommand;
        public DelegateCommand<SearchQueryViewModel> OpenEditPageCommand =>
            _OpenEditPageCommand ?? (_OpenEditPageCommand = new DelegateCommand<SearchQueryViewModel>(ExecuteOpenEditPageCommand));

        void ExecuteOpenEditPageCommand(SearchQueryViewModel searchQueryVM)
        {
            _messenger.Send(new NavigationAppCoreFrameRequestMessage(new(nameof(QueryEditPage), ("query", searchQueryVM.SeriaizeParameters()))));
        }


        private DelegateCommand<SearchQueryViewModel> _OpenSnapshotResultHistoryPageCommand;
        public DelegateCommand<SearchQueryViewModel> OpenSnapshotResultHistoryPageCommand =>
            _OpenSnapshotResultHistoryPageCommand ?? (_OpenSnapshotResultHistoryPageCommand = new DelegateCommand<SearchQueryViewModel>(ExecuteOpenSnapshotResultHistoryPageCommand));

        void ExecuteOpenSnapshotResultHistoryPageCommand(SearchQueryViewModel searchQueryVM)
        {
            _messenger.Send(new NavigationAppCoreFrameRequestMessage(new(nameof(SearchResultPage), ("query", searchQueryVM.SeriaizeParameters()))));
        }

        private DelegateCommand<SearchQueryResultMetaViewModel> _OpenSnapshotResultPageCommand;
        public DelegateCommand<SearchQueryResultMetaViewModel> OpenSnapshotResultPageCommand =>
            _OpenSnapshotResultPageCommand ??= new DelegateCommand<SearchQueryResultMetaViewModel>(ExecuteOpenSnapshotResultPageCommand);

        async void ExecuteOpenSnapshotResultPageCommand(SearchQueryResultMetaViewModel searchQueryResultMetaVM)
        {
            if (searchQueryResultMetaVM.TotalCount == 0) { return; }

            try
            {
                var req = await _messenger.Send(new NavigationAppCoreFrameRequestMessage(new(nameof(SearchResultPage), ("query", searchQueryResultMetaVM.SearchQueryId), ("version", searchQueryResultMetaVM.SnapshotVersion))));
                if (req.Success is false)
                {
                    throw req.Exception ?? new Exception();
                }
            }
            catch (Exception ex)
            {
                searchQueryResultMetaVM.IsOpenFailed = true;
                searchQueryResultMetaVM.OpenFailedMessage = ex.Message;
            }
        }
    }

    public sealed class SearchQueryResultMetaViewModel : SearchQueryViewModel
    {
        private readonly SearchQueryResultMeta _meta;
        
        public SearchQueryViewModel Query { get; }

        public SearchQueryResultMetaViewModel(SearchQueryResultMeta meta)
            : base(meta.SearchQueryId)
        {
            _meta = meta;
        }

        public string SearchQueryId => _meta.SearchQueryId;

        public DateTimeOffset SnapshotVersion => _meta.SnapshotVersion;

        public long TotalCount => _meta.TotalCount;

        public int CsvFormat => _meta.CsvFormat;


        private bool _IsOpenFailed;
        public bool IsOpenFailed
        {
            get { return _IsOpenFailed; }
            set { SetProperty(ref _IsOpenFailed, value); }
        }


        private string _OpenFailedMessage;
        public string OpenFailedMessage
        {
            get { return _OpenFailedMessage; }
            set { SetProperty(ref _OpenFailedMessage, value); }
        }

        
    }
}
