using Microsoft.Toolkit.Mvvm.Messaging;
using System;

namespace NicoVideoSnapshotSearchAssistanceTools.Presentation.ViewModels
{
    public class SearchQueryTemplateViewModel : SearchQueryViewModel
    {
        public SearchQueryTemplateViewModel(Guid id, string title, string queryParameters)
            : base(queryParameters)
        {
            Id = id;
            Title = title;
        }

        public Guid Id { get; }
        public string Title { get; }
    }
}
