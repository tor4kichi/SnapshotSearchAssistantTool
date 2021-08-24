using NicoVideoSnapshotSearchAssistanceTools.Presentation.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace NicoVideoSnapshotSearchAssistanceTools.Presentation.Views.TemplateSelector
{
    public sealed class JsonFilterTemplateSelector : DataTemplateSelector
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
                DateTimeOffsetJsonFilterViewModel => DateTimeOffsetTemplate,
                TimeSpanJsonFilterViewModel => TimeSpanTemplate,
                IntJsonFilterViewModel => IntTemplate,
                StringJsonFilterViewModel => StringTemplate,
                _ => base.SelectTemplateCore(item, container),
            };
        }
    }
}
