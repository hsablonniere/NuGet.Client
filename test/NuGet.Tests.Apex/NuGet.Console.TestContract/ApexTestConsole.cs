// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Text.Editor;
using NuGet.VisualStudio;
using NuGetConsole;
using NuGetConsole.Implementation.Console;

namespace NuGet.Console.TestContract
{
    public class ApexTestConsole
    {
        private IWpfConsole _wpfConsole;

        public ApexTestConsole(IWpfConsole WpfConsole)
        {
            _wpfConsole = WpfConsole;
        }

        private bool EnsureInitilizeConsole()
        {
            var stopwatch = Stopwatch.StartNew();
            var timeout = TimeSpan.FromMinutes(5);
            do
            {
                if (_wpfConsole.Dispatcher.IsStartCompleted && _wpfConsole.Host != null) { return true; }
                System.Threading.Thread.Sleep(100);
            }
            while (stopwatch.Elapsed < timeout);
            return false;
        }

        public bool IsPackageInstalled(string projectName, string packageId, string version)
        {
            _wpfConsole.Clear();
            var command = $"Get-Package {packageId} -ProjectName {projectName}";
            if (WaitForActionComplete(() => RunCommand(command), TimeSpan.FromMinutes(5)))
            {
                var snapshot = (_wpfConsole.Content as IWpfTextViewHost).TextView.TextBuffer.CurrentSnapshot;
                for (var i = 0; i < snapshot.LineCount; i++)
                {
                    var snapshotLine = snapshot.GetLineFromLineNumber(i);
                    var lineText = snapshotLine.GetText();
                    var packageIdResult = Regex.IsMatch(lineText, $"\\b{packageId}\\b", RegexOptions.IgnoreCase);
                    var versionResult = Regex.IsMatch(lineText, $"\\b{version}\\b", RegexOptions.IgnoreCase);
                    if (packageIdResult && versionResult)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public void Clear()
        {
            _wpfConsole.Clear();
        }

        public bool RunCommand(string command, TimeSpan timeout)
        {
            return WaitForActionComplete(() => RunCommandWithoutWait(command), timeout);
        }

        public void RunCommandWithoutWait(string command)
        {
            if (!string.IsNullOrEmpty(command))
            {
                UIInvoke(async () =>
                {
                    var wpfHost = _wpfConsole.Host;
                    if (wpfHost.IsCommandEnabled)
                    {
                        _wpfConsole.WriteLine(command);
                        await Task.Run(() => wpfHost.Execute(_wpfConsole, command, null));
                    }
                });
            }
        }

        public bool WaitForActionComplete(Action action, TimeSpan timeout)
        {
            if (!EnsureInitilizeConsole())
            {
                return false;
            }

            using (var semaphore = new ManualResetEventSlim())
            {
                void eventHandler(object s, EventArgs e) => semaphore.Set();
                var dispatcher = (IPrivateConsoleDispatcher)_wpfConsole.Dispatcher;
                dispatcher.SetExecutingCommand(true);
                var asynchost = (IAsyncHost)_wpfConsole.Host;
                asynchost.ExecuteEnd += eventHandler;

                try
                {
                    // Run
                    action();

                    return semaphore.Wait(timeout);
                }
                finally
                {
                    asynchost.ExecuteEnd -= eventHandler;
                    dispatcher.SetExecutingCommand(false);
                }
            }
        }

        private void UIInvoke(Action action)
        {
            NuGetUIThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await NuGetUIThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                action();
            });
        }
    }
}
