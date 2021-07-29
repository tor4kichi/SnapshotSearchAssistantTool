using Microsoft.Toolkit.Diagnostics;
using Microsoft.Toolkit.Uwp.UI;
using NiconicoToolkit.Channels;
using NiconicoToolkit.SnapshotSearch;
using NiconicoToolkit.User;
using NiconicoToolkit.Video;
using NicoVideoSnapshotSearchAssistanceTools.Models.Domain;
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
    public sealed class SearchResultPageViewModel : ViewModelBase
    {
        private SearchQueryResultMeta _resultMeta;
        public SearchQueryResultMeta ResultMeta
        {
            get { return _resultMeta; }
            set { SetProperty(ref _resultMeta, value); }
        }

        public AdvancedCollectionView ItemsView { get; } = new AdvancedCollectionView();

        CancellationTokenSource _navigationCts;



        public override async void OnNavigatedTo(INavigationParameters parameters)
        {
            _navigationCts = new CancellationTokenSource();
            base.OnNavigatedTo(parameters);

            var ct = _navigationCts.Token;
            if (parameters.TryGetValue("query", out string queryParameters)
                && parameters.TryGetValue("version", out DateTimeOffset version)
                )
            {
                ResultMeta = await SnapshotResultFileHelper.GetSearchQueryResultMetaAsync(queryParameters, version);

                Guard.IsNotNull(ResultMeta, nameof(ResultMeta));

                ct.ThrowIfCancellationRequested();

                using (ItemsView.DeferRefresh())
                {
                    var itemsAsyncEnumerator = SnapshotResultFileHelper.GetSearchResultItemsAsync(ResultMeta, ct);
                    int counter = 1;
                    await foreach (var item in itemsAsyncEnumerator)
                    {
                        ItemsView.Add(new SnapshotItemViewModel(counter, item));
                    }
                }
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

        public VideoId? ContentId => _snapshotVideoItem.ContentId;

        public UserId? UserId => _snapshotVideoItem.UserId;

        public string Title => _snapshotVideoItem.Title;

        public ChannelId? ChannelId => _snapshotVideoItem.ChannelId;

        public Uri ThumbnailUrl => _snapshotVideoItem.ThumbnailUrl;

    }
}
