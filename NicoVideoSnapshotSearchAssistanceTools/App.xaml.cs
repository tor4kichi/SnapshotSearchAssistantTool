using Microsoft.Toolkit.Mvvm.Messaging;
using NicoVideoSnapshotSearchAssistanceTools.ViewModels;
using NicoVideoSnapshotSearchAssistanceTools.ViewModels.Messages;
using NicoVideoSnapshotSearchAssistanceTools.Views;
using Prism.Ioc;
using Prism.Modularity;
using Prism.Mvvm;
using Prism.Navigation;
using Prism.Unity;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Unity;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace NicoVideoSnapshotSearchAssistanceTools
{
    /// <summary>
    /// 既定の Application クラスを補完するアプリケーション固有の動作を提供します。
    /// </summary>
    sealed partial class App : PrismApplication
    {
        /// <summary>
        ///単一アプリケーション オブジェクトを初期化します。これは、実行される作成したコードの
        ///最初の行であるため、論理的には main() または WinMain() と等価です。
        /// </summary>
        public App()
        {
            this.InitializeComponent();

            this.UnhandledException += App_UnhandledException;
        }

        private void App_UnhandledException(object sender, Windows.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            if (e.Exception is OperationCanceledException)
            {
                e.Handled = true;
            }
        }

        readonly static Regex ViewToViewModelNameReplaceRegex = new Regex("[^.]+$");

        public override void ConfigureViewModelLocator()
        {
            ViewModelLocationProvider.SetDefaultViewTypeToViewModelTypeResolver(viewType => 
            {
                var pageToken = viewType.Name;

                var viewModelFullName = ViewToViewModelNameReplaceRegex.Replace(viewType.FullName, $"{pageToken}ViewModel")
                    .Replace(".Views.", ".ViewModels.");

                var viewModelType = Type.GetType(viewModelFullName);

                if (viewModelType == null)
                {
                    throw new ArgumentException(
                        string.Format(CultureInfo.InvariantCulture, pageToken, this.GetType().Namespace + ".ViewModels"),
                        "pageToken");
                }

                return viewModelType;
            });
        }

        public override void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
        {
            base.ConfigureModuleCatalog(moduleCatalog);

            //moduleCatalog.AddModule<AppCoreModule>();
        }

        public override void RegisterTypes(IContainerRegistry container)
        {
            container.RegisterForNavigation<ApplicationCorePage, ApplicationCorePageViewModel>();
        }

        protected override void RegisterRequiredTypes(IContainerRegistry containerRegistry)
        {
            base.RegisterRequiredTypes(containerRegistry);

            containerRegistry.RegisterInstance<IMessenger>(WeakReferenceMessenger.Default);
        }

        public override void OnInitialized()
        {
            InitializeUIShell();

            var messenger = Container.Resolve<IMessenger>();
            messenger.Send(new NavigationAppCoreFrameRequestMessage(new(nameof(ApplicationCorePage))));
        }

        private void InitializeUIShell()
        {
            Window.Current.Content = Container.Resolve<AppCoreFrameHost>();
            Window.Current.Activate();
        }
    }
}
