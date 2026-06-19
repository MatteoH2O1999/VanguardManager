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
            ILogger<IHost> appLogger = app.Services.GetRequiredService<ILogger<IHost>>();
            ApplicationConfiguration.Initialize();
            LogInitialized(appLogger);

            try
            {
                Runner runner = app.Services.GetRequiredService<Runner>();
                runner.Run(args);
            }
            catch (Exception ex)
            {
                Logs.LogOutOfHostCrash(appLogger, ex);
            }
        }

        [LoggerMessage(LogLevel.Information, "GUI configuration initialized")]
        private static partial void LogInitialized(ILogger logger);
    }
}
