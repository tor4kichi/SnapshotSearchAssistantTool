using NiconicoToolkit.SnapshotSearch;

namespace NicoVideoSnapshotSearchAssistanceTools.Presentation.ViewModels
{
    public interface ISearchQuery
    {
        SearchFieldType[] Fields { get; set; }
        ISearchFilter Filters { get; set; }
        string Keyword { get; set; }
        SearchSort Sort { get; set; }
        SearchFieldType[] Targets { get; set; }
    }
}