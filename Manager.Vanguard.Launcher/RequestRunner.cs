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
using Microsoft.Extensions.Logging;

namespace Manager.Vanguard.Launcher
{
    internal sealed partial class RequestRunner(ILogger<RequestRunner> Logger, RequestManager RManager)
    {
        private readonly ILogger logger = Logger;
        private readonly RequestManager requestManager = RManager;

        public void Run(string[] args)
        {
            this.LogRunnerArgs(args);

            if (args.Length < 1)
            {
                throw new ArgumentException("No executable was requested", nameof(args));
            }

            string executable = args[0];
            string[] executableArgs = args[1..];

            if (!File.Exists(executable))
            {
                throw new ArgumentException("Executable does not exist", nameof(args));
            }
            if (Path.GetFullPath(executable) != executable)
            {
                throw new ArgumentException("Requested exectuable path is not absolute", nameof(args));
            }

            this.LogCreatingRequest(executable, executableArgs);
            this.requestManager.CreateRequest(new(executable, executableArgs));
            this.LogCreatedRequest(executable, executableArgs);

            throw new NotImplementedException();
        }

        [LoggerMessage(LogLevel.Debug, "Running request handler for args {args}")]
        private partial void LogRunnerArgs(string[] args);

        [LoggerMessage(
            LogLevel.Debug,
            "Creating request for play session with executable {executable} and args {args}"
        )]
        private partial void LogCreatingRequest(string executable, string[] args);

        [LoggerMessage(
            LogLevel.Information,
            "Created request for play session with executable {executable} and args {args}"
        )]
        private partial void LogCreatedRequest(string executable, string[] args);
    }
}
