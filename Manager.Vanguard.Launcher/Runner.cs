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

namespace Manager.Vanguard.Launcher
{
    internal sealed partial class Runner(
        ILogger<Runner> Logger,
        GUIRunner GRunner,
        StartupRunner SRunner,
        RequestRunner RRunner
    )
    {
        private readonly ILogger logger = Logger;
        private readonly GUIRunner guiRunner = GRunner;
        private readonly RequestRunner requestRunner = RRunner;
        private readonly StartupRunner startupRunner = SRunner;

        public void Run(string[] args)
        {
            this.LogArgs(args);

            if (args.Length > 0)
            {
                string action = args[0];
                this.LogAction(action);

                if (action == "startup")
                {
                    this.LogStartupRunner();
                    try
                    {
                        this.startupRunner.Run();
                    }
                    catch (Exception ex)
                    {
                        this.LogStartupRunnerError(ex);
                        Environment.ExitCode = -1;
                    }
                    this.LogStoppingRunner();
                    return;
                }
                else if (action == "request")
                {
                    this.LogRequestRunner();
                    try
                    {
                        this.requestRunner.Run(args);
                    }
                    catch (Exception ex)
                    {
                        this.LogRequestRunnerError(ex);
                        Environment.ExitCode = -1;
                    }
                    this.LogStoppingRunner();
                    return;
                }
                else
                {
                    this.LogInvalidAction(action);
                }
            }

            this.LogGUIRunner();
            try
            {
                this.guiRunner.Run(args);
            }
            catch (Exception ex)
            {
                this.LogGUIRunnerError(ex);
                Environment.ExitCode = -1;
            }
            this.LogStoppingRunner();
        }

        [LoggerMessage(LogLevel.Information, "Starting launcher with args {args}")]
        private partial void LogArgs(string[] args);

        [LoggerMessage(LogLevel.Debug, "Found action {action}")]
        private partial void LogAction(string action);

        [LoggerMessage(LogLevel.Information, "Starting startup script")]
        private partial void LogStartupRunner();

        [LoggerMessage(LogLevel.Error, "Error in startup script")]
        private partial void LogStartupRunnerError(Exception ex);

        [LoggerMessage(LogLevel.Information, "Starting request handler")]
        private partial void LogRequestRunner();

        [LoggerMessage(LogLevel.Error, "Error in request handler")]
        private partial void LogRequestRunnerError(Exception ex);

        [LoggerMessage(LogLevel.Warning, "Invalid action: {action}. Falling back to GUI")]
        private partial void LogInvalidAction(string action);

        [LoggerMessage(LogLevel.Information, "Starting GUI")]
        private partial void LogGUIRunner();

        [LoggerMessage(LogLevel.Error, "Error in GUI")]
        private partial void LogGUIRunnerError(Exception ex);

        [LoggerMessage(LogLevel.Information, "Stopping launcher")]
        private partial void LogStoppingRunner();
    }
}
