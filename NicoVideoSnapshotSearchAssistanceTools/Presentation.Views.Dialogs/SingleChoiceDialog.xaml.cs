using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// コンテンツ ダイアログの項目テンプレートについては、https://go.microsoft.com/fwlink/?LinkId=234238 を参照してください

namespace NicoVideoSnapshotSearchAssistanceTools.Presentation.Views.Dialogs
{
    public sealed partial class SingleChoiceDialog : ContentDialog
    {
        public SingleChoiceDialog()
        {
            this.InitializeComponent();

            ItemsListView.SelectionChanged += ItemsListView_SelectionChanged;
        }
        
        public async Task<IDialogSelectableItem> ShowAsync(string title, IEnumerable<IDialogSelectableItem> items, IEnumerable<IDialogSelectableItem> selectedItems)
        {
            ItemsListView.SelectionMode = ListViewSelectionMode.Single;
            Title = title;
            ItemsListView.ItemsSource = items;

            foreach (var selected in selectedItems)
            {
                ItemsListView.SelectedItems.Add(selected);
            }

            if (await ShowAsync() == ContentDialogResult.Primary)
            {
                return (IDialogSelectableItem)ItemsListView.SelectedItems.First();
            }
            else
            {
                return default(IDialogSelectableItem);
            }
        }

        private void ItemsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            IsPrimaryButtonEnabled = ItemsListView.SelectedItems.Any();
        }
    }

    public interface IDialogSelectableItem
    {
        string Name { get; }
    }

}
