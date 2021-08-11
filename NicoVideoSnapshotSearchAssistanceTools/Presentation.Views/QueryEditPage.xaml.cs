using NicoVideoSnapshotSearchAssistanceTools.Presentation.ViewModels;
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
using Prism.Ioc;
using Microsoft.Toolkit.Mvvm.Messaging;
using NiconicoToolkit.SnapshotSearch;

// 空白ページの項目テンプレートについては、https://go.microsoft.com/fwlink/?LinkId=234238 を参照してください

namespace NicoVideoSnapshotSearchAssistanceTools.Presentation.Views
{
    /// <summary>
    /// それ自体で使用できる空白ページまたはフレーム内に移動できる空白ページ。
    /// </summary>
    public sealed partial class QueryEditPage : Page
    {
        public QueryEditPage()
        {
            this.InitializeComponent();
        }

        private void ListView_Tapped(object sender, TappedRoutedEventArgs e)
        {

        }
    }

    
   
}
