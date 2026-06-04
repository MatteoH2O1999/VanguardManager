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

using System.Globalization;
using AutoUpdaterDotNET;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace Manager.Vanguard.Updater
{
    public sealed partial class Runner(ILogger<Runner> Logger)
    {
        private readonly ILogger logger = Logger;

        public void Run()
        {
            this.LogUpdateStart();

            CultureInfo installerCulture = new(
                (int?)
                    Registry.GetValue(
                        $"HKEY_LOCAL_MACHINE\\SOFTWARE\\{Application.CompanyName}\\{Application.ProductName}",
                        "InstallerCultureLCID",
                        null
                    )
                    ?? new CultureInfo("en-US").LCID
            );
            this.LogUpdateCulture(installerCulture.Name);

            string versionXmlUrl =
                $"https://github.com/MatteoH2O1999/VanguardManager/releases/latest/download/version-{installerCulture.Name}.xml";
            this.LogUpdateUrl(versionXmlUrl);

            AutoUpdater.Start(versionXmlUrl);

            this.LogUpdateEnd();
        }

        [LoggerMessage(LogLevel.Information, "Starting update")]
        private partial void LogUpdateStart();

        [LoggerMessage(LogLevel.Information, "Using culture {culture}")]
        private partial void LogUpdateCulture(string culture);

        [LoggerMessage(LogLevel.Information, "Checking version.xml at {url}")]
        private partial void LogUpdateUrl(string url);

        [LoggerMessage(LogLevel.Information, "Update procedure complete. Closing updater")]
        private partial void LogUpdateEnd();
    }
}
