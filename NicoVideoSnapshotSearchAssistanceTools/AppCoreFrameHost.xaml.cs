using CommunityToolkit.Diagnostics;
using Microsoft.Toolkit.Mvvm.Messaging;
using NicoVideoSnapshotSearchAssistanceTools.ViewModels.Messages;
using Prism.Navigation;
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
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

// ユーザー コントロールの項目テンプレートについては、https://go.microsoft.com/fwlink/?LinkId=234236 を参照してください

namespace NicoVideoSnapshotSearchAssistanceTools
{
    public sealed partial class AppCoreFrameHost : UserControl
    {
        private readonly INavigationService _navigationService;
        private readonly IMessenger _messenger;

        public AppCoreFrameHost(IMessenger messenger)
        {
            this.InitializeComponent();

            _messenger = messenger;

            _messenger.Register<NavigationAppCoreFrameRequestMessage>(this, (r, m) => 
            {
                Guard.IsFalse(m.HasReceivedResponse, nameof(m.HasReceivedResponse));

                var value = m.Value;
                m.Reply(NavigationAsync(value.Path, value.Parameters, value.TransisionOverride));
            });

            _navigationService = NavigationService.Create(this.CoreFrame);
        }


        private Task<INavigationResult> NavigationAsync(string pageType, INavigationParameters parameters, NavigationTransitionInfo transitionOverride)
        {
            Guard.IsNotEmpty(pageType, nameof(pageType));

            if (parameters is not null && transitionOverride is not null)
            {
                return _navigationService.NavigateAsync(pageType, parameters, transitionOverride);
            }
            else if (parameters is not null)
            {
                return _navigationService.NavigateAsync(pageType, parameters);
            }
            else if (transitionOverride is not null)
            {
                return _navigationService.NavigateAsync(pageType, transitionOverride);
            }            
            else
            {
                return _navigationService.NavigateAsync(pageType);
            }
        }

    }
}
