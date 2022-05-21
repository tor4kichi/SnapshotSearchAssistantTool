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
    public sealed class SimpleFilterTemplateSelector : DataTemplateSelector
    {
        public DataTemplate DateTimeOffsetTemplate { get; set; }
        public DataTemplate TimeSpanTemplate { get; set; }
        public DataTemplate IntTemplate { get; set; }
        public DataTemplate StringTemplate { get; set; }
        public DataTemplate ScoreTemplate { get; set; }

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
                ScoreSnapshotResultFilter => ScoreTemplate,
                _ => base.SelectTemplateCore(item, container),
            };
        }
    }
}
