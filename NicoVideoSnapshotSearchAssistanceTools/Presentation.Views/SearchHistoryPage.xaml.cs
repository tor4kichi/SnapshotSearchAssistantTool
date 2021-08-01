using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// 空白ページの項目テンプレートについては、https://go.microsoft.com/fwlink/?LinkId=234238 を参照してください

namespace NicoVideoSnapshotSearchAssistanceTools.Presentation.Views
{
    /// <summary>
    /// それ自体で使用できる空白ページまたはフレーム内に移動できる空白ページ。
    /// </summary>
    public sealed partial class SearchHistoryPage : Page
    {
        public SearchHistoryPage()
        {
            this.InitializeComponent();
        }

        private void ListView_ItemClick(object sender, ItemClickEventArgs e)
        {
            var listView = sender as ListView;
            if (e.ClickedItem is ViewModels.SearchQueryResultMetaViewModel vm)
            {
                vm.OpenSnapshotResultPageCommand.Execute();
            }
        }

        private void MenuFlyout_Opening(object sender, object e)
        {
            var flyout = sender as MenuFlyout;
            var dataContext = (flyout.Target as ListViewItem)?.Content;
            if (dataContext == null)
            {
                throw new InvalidOperationException();
            }

            foreach (var menuItem in flyout.Items)
            {
                menuItem.DataContext = dataContext;
            }
        }
    }
}
