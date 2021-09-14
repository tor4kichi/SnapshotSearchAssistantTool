using I18NPortable;
using NicoVideoSnapshotSearchAssistanceTools.Presentation.Views.Dialogs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NicoVideoSnapshotSearchAssistanceTools.Presentation.Services
{
    public sealed class DialogService : IDialogService
    {
        public Task<string> GetInputTextAsync(string title, string defaultText, string placeholderText)
        {
            var dialog = new TextInputDialog();
            return dialog.ShowAsync(title, defaultText, placeholderText, confirmText: "Confirm".Translate(), "Cancel".Translate());
        }


        public Task<IDialogSelectableItem> GetItemAsync(string title, IEnumerable<IDialogSelectableItem> items)
        {
            var dialog = new SingleChoiceDialog();
            return dialog.ShowAsync(title, items, Enumerable.Empty<IDialogSelectableItem>());
        }
    }

}
