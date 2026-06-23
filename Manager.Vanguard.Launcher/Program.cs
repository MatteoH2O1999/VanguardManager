// Copyright (C) 2026 Matteo Dell'Acqua
//
// This file is part of Vanguard Manager.
//
// Vanguard Manager is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// Vanguard Manager is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY, without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU Affero General Public License for more details.
//
// You should have received a copy of the GNU Affero General Public License
// along with Vanguard Manager. If not, see <http://www.gnu.org/licenses/>.

using Manager.Vanguard.Common;
using Manager.Vanguard.Translations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Manager.Vanguard.Launcher
{
    internal static partial class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        public static void Main(string[] args)
        {
            HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

            builder.Logging.ClearProviders();
            builder.Logging.AddFileLogging("launcher");
            builder.Logging.AddEventLog(options =>
            {
                options.Filter = (_, level) => level >= LogLevel.Critical;
                options.SourceName = ApplicationData.AppName;
            });
#if DEBUG
            builder.Logging.SetMinimumLevel(LogLevel.Trace);
#endif

            builder.Services.AddLocalizations();
            builder.Services.AddCommons();
            builder.Services.AddGUI();
            builder.Services.AddTransient<GUIRunner>();
            builder.Services.AddTransient<RequestRunner>();
            builder.Services.AddTransient<StartupRunner>();
            builder.Services.AddTransient<Runner>();

            using IHost app = builder.Build();

            ILogger logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("LauncherHost");
            Localization localization = app.Services.GetRequiredService<Localization>();

            Logs.LogOutOfHostMessage(logger, LogLevel.Debug, "Acquiring launcher lock");
            IDisposable? launcherLock;
            try
            {
                launcherLock = Locks.LAUNCHER.TryAcquire();
            }
            catch (LockException ex)
            {
                Logs.LogOutOfHostMessage(logger, LogLevel.Error, "Could not acquire launcher lock", ex);
                Environment.ExitCode = -1;
                MessageBox.Show(
                    localization.Launcher.StartupErrorMessage(ex),
                    localization.Launcher.StartupErrorTitle,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error,
                    MessageBoxDefaultButton.Button1,
                    MessageBoxOptions.DefaultDesktopOnly
                );
                return;
            }

            if (launcherLock is null)
            {
                Logs.LogOutOfHostMessage(logger, LogLevel.Error, "Launcher lock already in use by another process");
                Environment.ExitCode = -1;
                MessageBox.Show(
                    localization.Launcher.AlreadyInUseMessage,
                    localization.Launcher.AlreadyInUseTitle,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning,
                    MessageBoxDefaultButton.Button1,
                    MessageBoxOptions.DefaultDesktopOnly
                );
                return;
            }
            else
            {
                Logs.LogOutOfHostMessage(logger, LogLevel.Information, "Launcher lock acquired");
            }

            using (launcherLock)
            {
                ApplicationConfiguration.Initialize();
                Logs.LogOutOfHostMessage(logger, LogLevel.Information, "GUI configuration initialized");

                try
                {
                    Runner runner = app.Services.GetRequiredService<Runner>();
                    runner.Run(args);
                }
                catch (Exception ex)
                {
                    Logs.LogOutOfHostCrash(logger, ex);
                    MessageBox.Show(
                        localization.Launcher.StartupErrorMessage(ex),
                        localization.Launcher.StartupErrorTitle,
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error,
                        MessageBoxDefaultButton.Button1,
                        MessageBoxOptions.DefaultDesktopOnly
                    );
                }
            }
        }
    }
}
