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

using Microsoft.Extensions.Localization;
using static Vanara.PInvoke.AdvApi32;

namespace Manager.Vanguard.Translations.Resources
{
    public sealed class LauncherLocalizer(IStringLocalizer<LauncherLocalizer> Localizer)
    {
        private readonly IStringLocalizer localizer = Localizer;

        public string RebootPrompt => this.localizer.GetString("RebootPrompt");

        public string RebootPromptTitle => this.localizer.GetString("RebootPromptTitle");

        public string AlreadyInUseMessage => this.localizer.GetString("AlreadyInUseMessage");

        public string AlreadyInUseTitle => this.localizer.GetString("AlreadyInUseTitle");

        public string StartupErrorMessage(Exception ex) => this.localizer.GetString("StartupErrorMessage", ex.Message);

        public string StartupErrorTitle => this.localizer.GetString("StartupErrorTitle");

        public string RebootErrorMessage(Exception ex) => this.localizer.GetString("RebootErrorMessage", ex.Message);

        public string RebootErrorTitle => this.localizer.GetString("RebootErrorTitle");

        public string InvalidServiceStateMessage(string service, ServiceStartType expected, ServiceStartType actual) =>
            this.localizer.GetString("InvalidServiceStateMessage", service, expected, actual);

        public string InvalidServiceStateTitle => this.localizer.GetString("InvalidServiceStateTitle");
    }
}
