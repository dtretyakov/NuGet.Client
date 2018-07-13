// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Windows.Input;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;
using NuGet.VisualStudio;
using IAsyncServiceProvider = Microsoft.VisualStudio.Shell.IAsyncServiceProvider;

namespace NuGetVSExtension
{
    /// <summary>
    /// UI command to show output window switched to PM output.
    /// Allows binding to any control.
    /// </summary>
    internal sealed class ShowErrorsCommand : ICommand
    {
        private readonly AsyncLazy<IVsOutputWindow> _vsOutputWindow;
        private readonly AsyncLazy<IVsUIShell> _vsUiShell;

        public ShowErrorsCommand(IAsyncServiceProvider asyncServiceProvider)
        {
            if (asyncServiceProvider == null)
            {
                throw new ArgumentNullException(nameof(asyncServiceProvider));
            }
            // get all services we need for display and activation of the NuGet output pane
            _vsOutputWindow = new AsyncLazy<IVsOutputWindow>(async () =>
            {
                return await asyncServiceProvider.GetServiceAsync<SVsOutputWindow, IVsOutputWindow>();
            },
            NuGetUIThreadHelper.JoinableTaskFactory);

            _vsUiShell = new AsyncLazy<IVsUIShell>(async () =>
            {
                return await asyncServiceProvider.GetServiceAsync<SVsUIShell, IVsUIShell>();
            },
            NuGetUIThreadHelper.JoinableTaskFactory);
        }

        // Actually never raised
        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        // False if services were unavailable during instantiation. Never change.
        public bool CanExecute(object parameter)
        {
            return NuGetUIThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                return (await _vsUiShell?.GetValueAsync()) != null && (await _vsOutputWindow?.GetValueAsync()) != null;
            });
        }

        public void Execute(object parameter)
        {
            NuGetUIThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

                IVsWindowFrame toolWindow = null;
                (await _vsUiShell.GetValueAsync()).FindToolWindow(0, ref GuidList.guidVsWindowKindOutput, out toolWindow);
                toolWindow?.Show();

                IVsOutputWindowPane pane;
                if ((await _vsOutputWindow.GetValueAsync()).GetPane(ref NuGetConsole.GuidList.guidNuGetOutputWindowPaneGuid, out pane) == VSConstants.S_OK)
                {
                    pane.Activate();
                }
            });
        }
    }
}
