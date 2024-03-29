﻿using Microsoft.Toolkit.Mvvm.Messaging;
using NicoVideoSnapshotSearchAssistanceTools.Presentation.ViewModels;
using NicoVideoSnapshotSearchAssistanceTools.Presentation.Views;
using NicoVideoSnapshotSearchAssistanceTools.Presentation.ViewModels.Messages;
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
using LiteDB;
using Windows.Storage;
using Microsoft.Toolkit.Uwp.Helpers;
using Unity.Injection;
using NicoVideoSnapshotSearchAssistanceTools.Models.Infrastructure;
using NicoVideoSnapshotSearchAssistanceTools.Models.Domain;
using System.Web;
using System.Diagnostics;
using NicoVideoSnapshotSearchAssistanceTools.Presentation.Services;

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

            base.ConfigureViewModelLocator();
        }

        public override void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
        {
            base.ConfigureModuleCatalog(moduleCatalog);

            //moduleCatalog.AddModule<AppCoreModule>();
        }

        public override void RegisterTypes(IContainerRegistry container)
        {
            container.RegisterForNavigation<QueryEditPage, QueryEditPageViewModel>();
            container.RegisterForNavigation<QueryManagementPage, QueryManagementPageViewModel>();
            container.RegisterForNavigation<SearchRunningManagementPage, SearchRunningManagementPageViewModel>();
            container.RegisterForNavigation<SearchResultPage, SearchResultPageViewModel>();
            container.RegisterForNavigation<SearchHistoryPage, SearchHistoryPageViewModel>();
        }

        protected override void RegisterRequiredTypes(IContainerRegistry containerRegistry)
        {
            base.RegisterRequiredTypes(containerRegistry);

            var unityContainer = containerRegistry.GetContainer();
            unityContainer.RegisterType<LocalObjectStorageHelper>(new InjectionFactory(c => new LocalObjectStorageHelper(new SystemTextJsonSerializer())));

            LiteDatabase quertDb = new LiteDatabase($"Filename={Path.Combine(ApplicationData.Current.LocalFolder.Path, "query.db")};");
            unityContainer.RegisterInstance<ILiteDatabase>(quertDb);

            containerRegistry.RegisterInstance<IMessenger>(WeakReferenceMessenger.Default);

            NiconicoToolkit.NiconicoContext niconicoContext = new NiconicoToolkit.NiconicoContext("https://github.com/tor4kichi/TimeMachine2525");
            niconicoContext.SetupDefaultRequestHeaders();
            containerRegistry.RegisterInstance(niconicoContext);


            containerRegistry.Register<IDialogService, DialogService>();
        }

        public override async void OnInitialized()
        {
#if DEBUG
            Resources["IsDebug"] = true;
#else
            Resources["IsDebug"] = false;
#endif

            InitialzieLocalization();
            InitializeUIShell();

            var messenger = Container.Resolve<IMessenger>();
            var appInternalSettings = Container.Resolve<ApplicationInternalSettings>();

            try
            {
                if (!string.IsNullOrEmpty(appInternalSettings.LastOpenPageName))
                {
                    var navigationParameters = new NavigationParameters(appInternalSettings.LastOpenPageParameterKeyValues.Select(x => (x.Key, (object)x.Value)).ToArray());
                    await messenger.Send(new NavigationAppCoreFrameRequestMessage(new(appInternalSettings.LastOpenPageName, navigationParameters)));
                }
                else
                {
                    await messenger.Send(new NavigationAppCoreFrameRequestMessage(new(nameof(QueryEditPage))));
                }
            }
            catch
            {
                _ = messenger.Send(new NavigationAppCoreFrameRequestMessage(new(nameof(QueryEditPage))));
            }
        }

        private void InitialzieLocalization()
        {
            // ローカリゼーション用のライブラリを初期化
            try
            {
                I18NPortable.I18N.Current
#if DEBUG
                //.SetLogger(text => System.Diagnostics.Debug.WriteLine(text))
                .SetNotFoundSymbol("🍣")
#endif
                .SetFallbackLocale("ja")
                .Init(GetType().Assembly);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
            }

            Resources["Strings"] = I18NPortable.I18N.Current;
        }

        private void InitializeUIShell()
        {
            Window.Current.Content = Container.Resolve<AppCoreFrameHost>();
            Window.Current.Activate();
        }
    }
}
