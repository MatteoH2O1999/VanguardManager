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

using Manager.Vanguard.Launcher.GUI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Manager.Vanguard.Launcher
{
    internal sealed partial class GUIRunner(ILogger<GUIRunner> Logger, MainWindow MainWindow)
    {
        private readonly ILogger logger = Logger;
        private readonly MainWindow mainWindow = MainWindow;

        public void Run()
        {
            this.LogOpeningMainWindow();
            Application.Run(this.mainWindow);
        }

        [LoggerMessage(LogLevel.Information, "Opening main window")]
        private partial void LogOpeningMainWindow();
    }

    internal static class GUIRegistration
    {
        public static T AddGUI<T>(this T services)
            where T : IServiceCollection
        {
            services.AddTransient<MainWindow>();
            return services;
        }
    }
}
