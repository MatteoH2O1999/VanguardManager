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
    internal sealed partial class GameManager(ILogger<GameManager> Logger, RequestManager requestManager)
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
                this.LogWaitForPlaySessionStartLoop(SESSION_START_CHECK_INTERVAL);
                await Task.Delay(sessionStartCheckInterval, stoppingToken);
            }

            this.LogWaitForPlaySessionStartSuccess();
        }

        public async Task WaitForPlaySessionEnd(CancellationToken stoppingToken)
        {
            int sessionEndCheckInterval = SESSION_END_CHECK_INTERVAL * 1000;
            int retries = SESSION_END_CHECK_RETRIES;
            this.LogBeginWaitForPlaySessionEnd(SESSION_END_CHECK_INTERVAL, SESSION_END_CHECK_RETRIES);

            while (retries > 0)
            {
                if (retries == SESSION_END_CHECK_RETRIES)
                {
                    this.LogWaitForPlaySessionEndCheck();
                }
                else
                {
                    this.LogWaitForPlaySessionEndCheckRemainingIterations(retries);
                }

                if (this.IsGameOn())
                {
                    this.LogWaitForPlaySessionEndGameOn(SESSION_END_CHECK_RETRIES);
                    retries = SESSION_END_CHECK_RETRIES;
                }
                else
                {
                    this.LogWaitForPlaySessionEndGameOff(retries, retries - 1);
                    retries--;
                }

                this.LogWaitForPlaySessionEndLoop(SESSION_END_CHECK_INTERVAL);
                await Task.Delay(sessionEndCheckInterval, stoppingToken);
            }
        }

        private bool IsGameOn()
        {
            this.LogCheckingIsGameOn();

            bool isGameOn = false;

            Process[] activeProcesses = Process.GetProcesses();
            this.LogIsGameOnCurrentActiveProcesses(activeProcesses);

            if (
                activeProcesses.Any(p =>
                    ApplicationData.GameProcesses.Contains(p.ProcessName, StringComparer.CurrentCultureIgnoreCase)
                )
            )
            {
                this.LogIsGameOnFoundProcess();
                isGameOn = true;
            }
            else if (this.requestedExecutable is Request request)
            {
                List<string> processExecutables = [];
                foreach (Process p in activeProcesses)
                {
                    this.LogIsGameOnCheckingProcessModule(p);
                    ProcessModule? m = null;
                    try
                    {
                        m = p.MainModule;
                    }
                    catch (Exception ex)
                    {
                        this.LogIsGameOnProcessModuleError(p, ex);
                    }
                    if (m is ProcessModule module)
                    {
                        processExecutables.Add(module.FileName);
                    }
                }
                if (processExecutables.Any(e => e == request.Executable))
                {
                    this.LogIsGameOnFoundRequestedProcess();
                    isGameOn = true;
                }
            }

            if (isGameOn)
            {
                this.LogIsGameOnTrue();
            }
            else
            {
                this.LogIsGameOnFalse();
            }
            return isGameOn;
        }

        #region WaitForPlaySessionStart Logging

        [LoggerMessage(LogLevel.Debug, "Session is not yet started. Waiting for {secondsToWait} seconds")]
        private partial void LogWaitForPlaySessionStartLoop(int secondsToWait);

        [LoggerMessage(LogLevel.Debug, "Session has started")]
        private partial void LogWaitForPlaySessionStartSuccess();

        #endregion

        #region WaitForPlaySessionEnd Logging

        [LoggerMessage(
            LogLevel.Information,
            "Checking for play session end every {interval} seconds ({retries} iterations required)"
        )]
        private partial void LogBeginWaitForPlaySessionEnd(int interval, int retries);

        [LoggerMessage(LogLevel.Debug, "Checking if game is on")]
        private partial void LogWaitForPlaySessionEndCheck();

        [LoggerMessage(LogLevel.Debug, "Checking if game is back on ({iterations} iterations left)")]
        private partial void LogWaitForPlaySessionEndCheckRemainingIterations(int iterations);

        [LoggerMessage(LogLevel.Trace, "Game is on. Restore retries to {restoredRetries}")]
        private partial void LogWaitForPlaySessionEndGameOn(int restoredRetries);

        [LoggerMessage(LogLevel.Trace, "Game is off. Update retries {oldRetries} -> {newRetries}")]
        private partial void LogWaitForPlaySessionEndGameOff(int oldRetries, int newRetries);

        [LoggerMessage(LogLevel.Trace, "Waiting for {secondsToWait} seconds")]
        private partial void LogWaitForPlaySessionEndLoop(int secondsToWait);

        #endregion

        #region IsGameOn Logging

        [LoggerMessage(LogLevel.Debug, "Checking whether user is playing")]
        private partial void LogCheckingIsGameOn();

        [LoggerMessage(LogLevel.Trace, "Current active processes: {processes}")]
        private partial void LogIsGameOnCurrentActiveProcesses(Process[] processes);

        [LoggerMessage(LogLevel.Debug, "Found a process in active processes")]
        private partial void LogIsGameOnFoundProcess();

        [LoggerMessage(LogLevel.Trace, $"Reading {nameof(Process.MainModule)} from process {{process}}")]
        private partial void LogIsGameOnCheckingProcessModule(Process process);

        [LoggerMessage(LogLevel.Trace, $"Cannot read {nameof(Process.MainModule)} from process {{process}}")]
        private partial void LogIsGameOnProcessModuleError(Process process, Exception ex);

        [LoggerMessage(LogLevel.Debug, "Found requested executable in active processes")]
        private partial void LogIsGameOnFoundRequestedProcess();

        [LoggerMessage(LogLevel.Debug, "Game is on")]
        private partial void LogIsGameOnTrue();

        [LoggerMessage(LogLevel.Debug, "Game is off")]
        private partial void LogIsGameOnFalse();

        #endregion
    }
}
