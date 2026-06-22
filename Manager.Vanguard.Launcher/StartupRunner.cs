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

using System.Diagnostics;
using Manager.Vanguard.Common;
using Microsoft.Extensions.Logging;

namespace Manager.Vanguard.Launcher
{
    internal sealed partial class StartupRunner(ILogger<StartupRunner> Logger, RequestManager RManager)
    {
        private readonly ILogger logger = Logger;
        private readonly RequestManager requestManager = RManager;

        public void Run()
        {
            this.LogCheckingForPlaySession();

            Request? request = this.requestManager.CheckRequest();

            if (request is null)
            {
                this.LogNoPlaySession();
                return;
            }

            this.LogPlaySession(request);
            this.RunRequest(request);
        }

        private void RunRequest(Request request)
        {
            ProcessStartInfo startInfo = new() { FileName = request.Executable };
            foreach (string arg in request.Args)
            {
                startInfo.ArgumentList.Add(arg);
            }
            this.LogProcessStartInfo(startInfo);

            using Process process = new() { StartInfo = startInfo };
            process.Start();
        }

        [LoggerMessage(LogLevel.Debug, "Checking request for a play session")]
        private partial void LogCheckingForPlaySession();

        [LoggerMessage(LogLevel.Information, "No request for play session found")]
        private partial void LogNoPlaySession();

        [LoggerMessage(LogLevel.Information, "Found request {request}")]
        private partial void LogPlaySession(Request request);

        [LoggerMessage(LogLevel.Debug, "Running process with start info {startInfo}")]
        private partial void LogProcessStartInfo(ProcessStartInfo startInfo);
    }
}
