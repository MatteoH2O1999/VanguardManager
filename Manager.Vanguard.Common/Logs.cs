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

using Microsoft.Extensions.Logging;

namespace Manager.Vanguard.Common
{
    public static partial class Logs
    {
        private const long MAX_FILE_SIZE = 10 * 1024 * 1024;

        public static T AddFileLogging<T>(this T logging, string appName)
            where T : ILoggingBuilder
        {
            logging.AddFile(
                $"{ApplicationData.AppData}/logs/{appName}-{{Date}}.log",
                minimumLevel: LogLevel.Information,
                retainedFileCountLimit: 31,
                fileSizeLimitBytes: MAX_FILE_SIZE,
                outputTemplate: "{Timestamp:o} {RequestId,13} [{Level}] {Message}{NewLine}{Exception}"
            );
            return logging;
        }

        [LoggerMessage(0, LogLevel.Critical, "Application crashed while out of host")]
        public static partial void LogOutOfHostCrash(this ILogger logger, Exception ex);
    }
}
