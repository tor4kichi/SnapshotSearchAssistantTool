using NiconicoToolkit.Channels;
using NiconicoToolkit.User;
using NiconicoToolkit.Video;
using System;
using System.Runtime.Serialization;

namespace NicoVideoSnapshotSearchAssistanceTools.Models.Domain.SnapshotResult_V0
{
    public sealed class SnapshotSearchItem_V0
    {
        [DataMember(Name="mylistCounter")]
        public long? MylistCounter { get; init; }

        [DataMember(Name="lengthSeconds")]
        public long? LengthSeconds { get; init; }

        [DataMember(Name="categoryTags")]
        public string CategoryTags { get; init; }

        [DataMember(Name="viewCounter")]
        public long? ViewCounter { get; init; }

        [DataMember(Name="commentCounter")]
        public long? CommentCounter { get; init; }

        [DataMember(Name="likeCounter")]
        public long? LikeCounter { get; init; }

        [DataMember(Name="genre")]
        public string Genre { get; init; }

        [DataMember(Name="startTime")]
        public DateTimeOffset? StartTime { get; init; }

        [DataMember(Name="lastCommentTime")]
        public DateTimeOffset? LastCommentTime { get; init; }

        [DataMember(Name="description")]
        public string Description { get; init; }

        [DataMember(Name="tags")]
        public string Tags { get; init; }

        [DataMember(Name="lastResBody")]
        public string LastResBody { get; init; }

        [DataMember(Name="contentId")]
        public string? ContentId { get; init; }

        [DataMember(Name="userId")]
        public int? UserId { get; init; }

        [DataMember(Name="title")]
        public string Title { get; init; }

        [DataMember(Name="channelId")]
        public string? ChannelId { get; init; }

        [DataMember(Name="thumbnailUrl")]
        public Uri ThumbnailUrl { get; init; }
    }

}
