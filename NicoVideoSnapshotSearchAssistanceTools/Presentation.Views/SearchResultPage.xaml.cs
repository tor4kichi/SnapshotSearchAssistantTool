using Microsoft.Toolkit.Uwp.UI;
using Microsoft.Toolkit.Uwp.UI.Controls;
using NicoVideoSnapshotSearchAssistanceTools.Presentation.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Uno.Extensions;
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
    public sealed partial class SearchResultPage : Page
    {
        public SearchResultPage()
        {
            this.InitializeComponent();
        }

        private void DataGrid_Sorting(object sender, Microsoft.Toolkit.Uwp.UI.Controls.DataGridColumnEventArgs e)
        {
            var dataGrid = sender as DataGrid;
            var acv = dataGrid.ItemsSource as AdvancedCollectionView;

            static bool IsSnapshotItemViewModelClassMemberPropertyName(string propertyName)
            {
                var members = typeof(SnapshotItemViewModel).GetMember(propertyName);
                return members != null && members.Any();
            }

            var propertyName = e.Column.Tag as string;
            if (propertyName == null
                || !IsSnapshotItemViewModelClassMemberPropertyName(propertyName)
                )
            {
                return;
            }

            using (acv.DeferRefresh())
            {
                acv.SortDescriptions.Clear();

                var sortDirection = e.Column.SortDirection;

                if (propertyName == nameof(SnapshotItemViewModel.Index))
                {
                    bool isSortChanged = false;
                    foreach (var column in dataGrid.Columns)
                    {
                        if (column.Tag as string == nameof(SnapshotItemViewModel.Index)) { continue; }

                        if (column.SortDirection != null)
                        {
                            isSortChanged = true;
                        }
                        column.SortDirection = null;
                    }

                    e.Column.SortDirection = !isSortChanged ? sortDirection switch
                    {
                        null => DataGridSortDirection.Descending,
                        DataGridSortDirection.Descending => null,
                        _ => throw new NotSupportedException(sortDirection?.ToString()),
                    }
                    : null;

                    static SortDirection ToAcvSortDirection(DataGridSortDirection? value)
                    {
                        return value == null ? SortDirection.Ascending : SortDirection.Descending;
                    }

                    acv.SortDescriptions.Add(new SortDescription(propertyName, ToAcvSortDirection(e.Column.SortDirection)));
                }
                else
                {
                    foreach (var column in dataGrid.Columns)
                    {
                        column.SortDirection = null;
                    }

                    e.Column.SortDirection = sortDirection switch
                    {
                        null => DataGridSortDirection.Descending,
                        DataGridSortDirection.Descending => DataGridSortDirection.Ascending,
                        DataGridSortDirection.Ascending => null,
                        _ => throw new NotSupportedException(sortDirection?.ToString()),
                    };

                    if (e.Column.SortDirection == null)
                    {
                        return;
                    }

                    static SortDirection ToAcvSortDirection(DataGridSortDirection value)
                    {
                        return value == DataGridSortDirection.Ascending ? SortDirection.Ascending : SortDirection.Descending;
                    }

                    acv.SortDescriptions.Add(new SortDescription(propertyName, ToAcvSortDirection(e.Column.SortDirection.Value)));
                }
            }
        }
    }
}
