// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Threading;
using NuGet.VisualStudio;
using NuGetConsole;
using NuGetConsole.Implementation.PowerConsole;

namespace NuGet.Console.TestContract
{
    [Export(typeof(NuGetApexConsoleTestService))]
    public class NuGetApexConsoleTestService
    {
        private Lazy<IWpfConsole> _wpfConsole => new Lazy<IWpfConsole>(GetWpfConsole);
        private static readonly TimeSpan _timeout = TimeSpan.FromMinutes(10);

        private IWpfConsole GetWpfConsole()
        {
            PowerConsoleWindow powershellConsole = null;
            var timer = Stopwatch.StartNew();

            while (powershellConsole?.ActiveHostInfo?.WpfConsole == null)
            {
                try
                {
                    var outputConsoleWindow = ServiceLocator.GetInstance<IPowerConsoleWindow>();
                    powershellConsole = outputConsoleWindow as PowerConsoleWindow;
                }
                catch when (timer.Elapsed < _timeout)
                {
                    // Retry until the console is loaded
                    Thread.Sleep(100);
                }
            }

            return powershellConsole.ActiveHostInfo.WpfConsole;
        }

        public NuGetApexConsoleTestService()
        {
        }

        public ApexTestConsole GetApexTestConsole()
        {
            return new ApexTestConsole(_wpfConsole.Value);
        }
    }
}
