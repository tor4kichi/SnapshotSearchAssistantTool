using NicoVideoSnapshotSearchAssistanceTools.Presentation.Views.Dialogs;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NicoVideoSnapshotSearchAssistanceTools.Presentation.Services
{
    public interface IDialogService
    {
        Task<string> GetInputTextAsync(string title, string defaultText, string placeholderText);
        Task<IDialogSelectableItem> GetItemAsync(string title, IEnumerable<IDialogSelectableItem> items);
    }
}