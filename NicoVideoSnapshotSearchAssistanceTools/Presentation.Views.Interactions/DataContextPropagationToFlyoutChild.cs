using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;

namespace NicoVideoSnapshotSearchAssistanceTools.Presentation.Views.Interactions
{
    public partial class FlyoutExtensions : DependencyObject
    {
        public static readonly DependencyProperty DataContextPropagationToFlyoutChildProperty =
            DependencyProperty.RegisterAttached(
              "DataContextPropagationToFlyoutChild",
              typeof(Boolean),
              typeof(FlyoutExtensions),
              new PropertyMetadata(false, OnDataContextPropagationToFlyoutChildChanged)
            );

        private static void OnDataContextPropagationToFlyoutChildChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (e.NewValue is bool b && b == true)
            {
                var flyout = d as FlyoutBase;
                flyout.Opening += MenuFlyout_Opening;
            }
        }

        private static void MenuFlyout_Opening(object sender, object e)
        {
            var flyout = sender as FlyoutBase;
            var dataContext = (flyout.Target as SelectorItem)?.Content ?? flyout.Target.DataContext;
            if (dataContext == null)
            {
                throw new InvalidOperationException();
            }

            if (flyout is MenuFlyout menuFlyout)
            {
                foreach (var menuItem in menuFlyout.Items)
                {
                    menuItem.DataContext = dataContext;
                }
            }
            else if (flyout is Flyout simpleFlyout)
            {
                if (simpleFlyout.Content is FrameworkElement fe)
                {
                    fe.DataContext = dataContext;
                }
            }
            else if (flyout is CommandBarFlyout cmdBarFlyout)
            {
                foreach (var item in cmdBarFlyout.PrimaryCommands)
                {
                    if (item is FrameworkElement fe)
                    {
                        fe.DataContext = dataContext;
                    }
                }

                foreach (var item in cmdBarFlyout.SecondaryCommands)
                {
                    if (item is FrameworkElement fe)
                    {
                        fe.DataContext = dataContext;
                    }
                }
            }
        }

        public static void SetDataContextPropagationToFlyoutChild(FlyoutBase element, Boolean value)
        {
            element.SetValue(DataContextPropagationToFlyoutChildProperty, value);
        }
        public static Boolean GetDataContextPropagationToFlyoutChild(FlyoutBase element)
        {
            return (Boolean)element.GetValue(DataContextPropagationToFlyoutChildProperty);
        }
    }
}
