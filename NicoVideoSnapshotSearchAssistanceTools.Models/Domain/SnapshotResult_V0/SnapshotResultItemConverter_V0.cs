using NiconicoToolkit.Channels;
using NiconicoToolkit.SnapshotSearch;
using NiconicoToolkit.Video;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NicoVideoSnapshotSearchAssistanceTools.Models.Domain.SnapshotResult_V0
{
    public static class SnapshotResultItemConverter_V0
    {
        public static SnapshotSearchItem_V0 Convert(SnapshotVideoItem item)
        {
            return new SnapshotSearchItem_V0()
            {
                CategoryTags = item.CategoryTags,
                ChannelId = item.ChannelId,
                CommentCounter = item.CommentCounter,
                ContentId = item.ContentId,
                Description = item.Description,
                Genre = item.Genre,
                LastCommentTime = item.LastCommentTime,
                LastResBody = item.LastResBody,
                LengthSeconds = item.LengthSeconds,
                LikeCounter = item.LikeCounter,
                MylistCounter = item.MylistCounter,
                StartTime = item.StartTime,
                Tags = item.Tags,
                ThumbnailUrl = item.ThumbnailUrl,
                Title = item.Title,
                UserId = item.UserId,
                ViewCounter = item.ViewCounter,
            };
        }

        public static SnapshotVideoItem ConvertBack(SnapshotSearchItem_V0 item)
        {
            return new SnapshotVideoItem()
            {
                CategoryTags = item.CategoryTags,
                ChannelId = !string.IsNullOrEmpty(item.ChannelId) ? (ChannelId)item.ChannelId : default(ChannelId?),
                CommentCounter = item.CommentCounter,
                ContentId = !string.IsNullOrEmpty(item.ContentId) ? (VideoId)item.ContentId : default(VideoId?),
                Description = item.Description,
                Genre = item.Genre,
                LastCommentTime = item.LastCommentTime,
                LastResBody = item.LastResBody,
                LengthSeconds = item.LengthSeconds,
                LikeCounter = item.LikeCounter,
                MylistCounter = item.MylistCounter,
                StartTime = item.StartTime,
                Tags = item.Tags,
                ThumbnailUrl = item.ThumbnailUrl,
                Title = item.Title,
                UserId = item.UserId,
                ViewCounter = item.ViewCounter,
            };
        }
    }
}
