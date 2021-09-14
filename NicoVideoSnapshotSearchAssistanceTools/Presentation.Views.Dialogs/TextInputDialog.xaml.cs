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
    public sealed partial class TextInputDialog : ContentDialog
    {        
        public TextInputDialog()
        {
            this.InitializeComponent();
        }

        public async Task<string> ShowAsync(string title, string defaultText, string placeholderText, string confirmText, string cancelText)
        {
            Title = title;
            InputTextBox.Text = defaultText;
            InputTextBox.PlaceholderText = placeholderText;
            PrimaryButtonText = confirmText;
            SecondaryButtonText = cancelText;
            if (await base.ShowAsync() == ContentDialogResult.Primary)
            {
                return InputTextBox.Text;
            }
            else
            {
                return null;
            }
        }
    }
}
