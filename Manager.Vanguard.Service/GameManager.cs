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

namespace Manager.Vanguard.Service
{
    internal sealed class GameManager(ILogger<GameManager> Logger, RequestManager requestManager)
    {
        private const int SESSION_START_CHECK_INTERVAL = 10;
        private const int SESSION_END_CHECK_INTERVAL = 60;
        private const int SESSION_END_CHECK_RETRIES = 5;

        private readonly ILogger logger = Logger;
        private readonly Request? requestedExecutable = requestManager.CheckRequest();

        public async Task WaitForPlaySessionStart(CancellationToken stoppingToken)
        {
            int sessionStartCheckInterval = SESSION_START_CHECK_INTERVAL * 1000;

            while (!this.IsGameOn())
            {
                this.logger.LogDebug(
                    "Session is not yet started. Waiting for {} seconds",
                    SESSION_START_CHECK_INTERVAL
                );
                await Task.Delay(sessionStartCheckInterval, stoppingToken);
            }

            this.logger.LogDebug("Session has started");
        }

        public async Task WaitForPlaySessionEnd(CancellationToken stoppingToken)
        {
            int sessionEndCheckInterval = SESSION_END_CHECK_INTERVAL * 1000;
            int retries = SESSION_END_CHECK_RETRIES;
            this.logger.LogInformation(
                "Checking for play session end every {} seconds ({} iterations required)",
                SESSION_END_CHECK_INTERVAL,
                SESSION_END_CHECK_RETRIES
            );

            while (retries > 0)
            {
                if (retries == SESSION_END_CHECK_RETRIES)
                {
                    this.logger.LogDebug("Checking if game is on");
                }
                else
                {
                    this.logger.LogDebug("Checking if game is back on ({} iterations left)", retries);
                }

                if (this.IsGameOn())
                {
                    this.logger.LogTrace("Game is on. Restore retries to {}", SESSION_END_CHECK_RETRIES);
                    retries = SESSION_END_CHECK_RETRIES;
                }
                else
                {
                    this.logger.LogTrace("Game is off. Update retries {} -> {}", retries, retries - 1);
                    retries--;
                }

                this.logger.LogTrace("Waiting for {} seconds", SESSION_END_CHECK_INTERVAL);
                await Task.Delay(sessionEndCheckInterval, stoppingToken);
            }
        }

        private bool IsGameOn()
        {
            this.logger.LogDebug("Checking whether user is playing");

            bool isGameOn = false;

            Process[] activeProcesses = Process.GetProcesses();
            this.logger.LogTrace("Current active processes: {}", activeProcesses);

            if (
                activeProcesses.Any(p =>
                    ApplicationData.GameProcesses.Contains(p.ProcessName, StringComparer.CurrentCultureIgnoreCase)
                )
            )
            {
                this.logger.LogDebug("Found a process in active processes");
                isGameOn = true;
            }
            else if (
                this.requestedExecutable is Request request
                && activeProcesses.Any(p => p.MainModule?.FileName == request.Executable)
            )
            {
                this.logger.LogDebug("Found requested executable in active processes");
                isGameOn = true;
            }

            if (isGameOn)
            {
                this.logger.LogDebug("Game is on");
            }
            else
            {
                this.logger.LogDebug("Game is off");
            }
            return isGameOn;
        }
    }
}
