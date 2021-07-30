using Microsoft.Toolkit.Diagnostics;
using Microsoft.Toolkit.Uwp.UI;
using NiconicoToolkit.Channels;
using NiconicoToolkit.SnapshotSearch;
using NiconicoToolkit.User;
using NiconicoToolkit.Video;
using NicoVideoSnapshotSearchAssistanceTools.Models.Domain;
using NicoVideoSnapshotSearchAssistanceTools.Presentation.Views;
using Prism.Mvvm;
using Prism.Navigation;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NicoVideoSnapshotSearchAssistanceTools.Presentation.ViewModels
{
    public sealed class SearchResultPageViewModel : ViewModelBase
    {
        public SearchResultPageViewModel(ApplicationInternalSettings applicationInternalSettings)
        {
            _applicationInternalSettings = applicationInternalSettings;
        }

        private SearchQueryResultMeta _resultMeta;
        public SearchQueryResultMeta ResultMeta
        {
            get { return _resultMeta; }
            set { SetProperty(ref _resultMeta, value); }
        }

        public AdvancedCollectionView ItemsView { get; } = new AdvancedCollectionView();

        CancellationTokenSource _navigationCts;

        private bool _isFailed;
        public bool IsFailed
        {
            get { return _isFailed; }
            set { SetProperty(ref _isFailed, value); }
        }

        private string _FailedErrorMessage;
        private readonly ApplicationInternalSettings _applicationInternalSettings;

        public string FailedErrorMessage
        {
            get { return _FailedErrorMessage; }
            set { SetProperty(ref _FailedErrorMessage, value); }
        }

        public override async Task OnNavigatedToAsync(INavigationParameters parameters)
        {            
            _navigationCts = new CancellationTokenSource();

            await base.OnNavigatedToAsync(parameters);

            try
            {
                var ct = _navigationCts.Token;
                if (!parameters.TryGetValue("query", out string queryParameters))
                {
                    ThrowHelper.ThrowInvalidOperationException(nameof(queryParameters));
                }

                if (!parameters.TryGetValue("version", out DateTimeOffset version))
                {
                    if (parameters.TryGetValue("version", out string versionStr))
                    {
                        version = DateTimeOffset.Parse(versionStr);
                    }
                    else
                    {
                        ThrowHelper.ThrowInvalidOperationException(nameof(version));
                    }
                }
                
                try
                {
                    ResultMeta = await SnapshotResultFileHelper.GetSearchQueryResultMetaAsync(queryParameters, version);
                }
                catch (Exception ex)
                {
                    ThrowHelper.ThrowInvalidOperationException(nameof(ResultMeta), ex);
                }

                _applicationInternalSettings.SaveLastOpenPage(nameof(SearchResultPage), ("query", queryParameters), ("version", version.ToString()));

                Guard.IsNotNull(ResultMeta, nameof(ResultMeta));

                ct.ThrowIfCancellationRequested();

                using (ItemsView.DeferRefresh())
                {
                    var itemsAsyncEnumerator = SnapshotResultFileHelper.GetSearchResultItemsAsync(ResultMeta, ct);
                    int counter = 1;
                    await foreach (var item in itemsAsyncEnumerator)
                    {
                        ItemsView.Add(new SnapshotItemViewModel(counter, item));
                        counter++;
                        ct.ThrowIfCancellationRequested();
                    }
                }
            }
            catch (Exception ex)
            {
                IsFailed = true;
                FailedErrorMessage = ex.Message;
                ItemsView.Clear();
                throw;
            }
        }
    }


    public sealed class SnapshotItemViewModel : BindableBase
    {
        private readonly SnapshotVideoItem _snapshotVideoItem;

        public SnapshotItemViewModel(int index, SnapshotVideoItem snapshotVideoItem)
        {
            Index = index;
            _snapshotVideoItem = snapshotVideoItem;
        }

        public int Index { get; }




        public long? MylistCounter => _snapshotVideoItem.MylistCounter;

        public long? LengthSeconds => _snapshotVideoItem.LengthSeconds;

        public string CategoryTags => _snapshotVideoItem.CategoryTags;

        public long? ViewCounter => _snapshotVideoItem.ViewCounter;

        public long? CommentCounter => _snapshotVideoItem.CommentCounter;

        public long? LikeCounter => _snapshotVideoItem.LikeCounter;

        public string Genre => _snapshotVideoItem.Genre;

        public DateTimeOffset? StartTime => _snapshotVideoItem.StartTime;

        public DateTimeOffset? LastCommentTime => _snapshotVideoItem.LastCommentTime;

        public string Description => _snapshotVideoItem.Description;

        public string Tags => _snapshotVideoItem.Tags;

        public string LastResBody => _snapshotVideoItem.LastResBody;

        public string ContentId => _snapshotVideoItem.ContentId;

        public long? UserId => _snapshotVideoItem.UserId.HasValue ? (long)_snapshotVideoItem.UserId.Value.RawId : null;

        public string Title => _snapshotVideoItem.Title;

        public string ChannelId => _snapshotVideoItem.ChannelId;

        public Uri ThumbnailUrl => _snapshotVideoItem.ThumbnailUrl;

    }
}
