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

    public sealed class SimpleFilterTemplateSelector : DataTemplateSelector
    {
        public DataTemplate DateTimeOffsetTemplate { get; set; }
        public DataTemplate TimeSpanTemplate { get; set; }
        public DataTemplate IntTemplate { get; set; }
        public DataTemplate StringTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object item)
        {
            return this.SelectTemplateCore(item, null);
        }
        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            return item switch
            {
                DateTimeOffsetSimpleFilterViewModel => DateTimeOffsetTemplate,
                TimeSpanSimpleFilterViewModel => TimeSpanTemplate,
                IntSimpleFilterViewModel => IntTemplate,
                StringSimpleFilterViewModel => StringTemplate,
                _ => base.SelectTemplateCore(item, container),
            };
        }
    }

   
}
