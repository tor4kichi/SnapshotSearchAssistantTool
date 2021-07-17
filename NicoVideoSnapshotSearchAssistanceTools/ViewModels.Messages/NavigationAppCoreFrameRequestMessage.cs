using Microsoft.Toolkit.Mvvm.Messaging.Messages;
using Prism.Navigation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Media.Animation;

namespace NicoVideoSnapshotSearchAssistanceTools.ViewModels.Messages
{
    public record NavigationAppCoreFrameRequestMessageValue
    {
        public NavigationAppCoreFrameRequestMessageValue(string path)
        {
            Path = path;
        }

        public NavigationAppCoreFrameRequestMessageValue(string path, params (string Name, object Value)[] parameters)
        {
            Path = path;
            Parameters = new NavigationParameters(parameters);
        }

        public INavigationParameters Parameters { get; init; }
        public NavigationTransitionInfo TransisionOverride { get; init; }
        public string Path { get; }
    }
    internal sealed class NavigationAppCoreFrameRequestMessage : AsyncRequestMessage<INavigationResult>
    {
        public NavigationAppCoreFrameRequestMessage(NavigationAppCoreFrameRequestMessageValue value)
        {
            Value = value;
        }

        public NavigationAppCoreFrameRequestMessageValue Value { get; }
    }
}
