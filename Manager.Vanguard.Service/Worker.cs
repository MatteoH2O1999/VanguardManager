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
using static Vanara.PInvoke.AdvApi32;

namespace Manager.Vanguard.Service
{
    internal sealed partial class Worker(
        ILogger<Worker> Logger,
        IHostApplicationLifetime HostApplicationLifetime,
        RequestManager RequestManager,
        VanguardManager VManager,
        GameManager GManager
    ) : BackgroundService
    {
        private const int SHUTDOWN_CHECK_INTERVAL_SECONDS =
#if DEBUG
            1;
#else
            10;
#endif
        private const int SHUTDOWN_CHECK_INTERVAL = SHUTDOWN_CHECK_INTERVAL_SECONDS * 1000;

        private readonly ILogger logger = Logger;
        private readonly IHostApplicationLifetime hostApplicationLifetime = HostApplicationLifetime;
        private readonly VanguardManager vanguardManager = VManager;
        private readonly GameManager gameManager = GManager;
        private readonly RequestManager requestManager = RequestManager;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                this.logger.LogDebug("Acquiring service lock");
                IDisposable? serviceLock;
                try
                {
                    serviceLock = Locks.SERVICE.TryAcquire();
                }
                catch (LockException ex)
                {
                    this.logger.LogError(ex, "Could not acquire service lock");
                    Environment.ExitCode = -1;
                    this.hostApplicationLifetime.StopApplication();
                    return;
                }

                if (serviceLock is null)
                {
                    this.logger.LogError("Service lock already in use by another process");
                    Environment.ExitCode = -1;
                }
                else
                {
                    this.logger.LogInformation("Service lock acquired");
                    using (serviceLock)
                    {
                        if (this.requestManager.RequestExists())
                        {
                            await this.HandleRequest(stoppingToken);
                        }
                        else
                        {
                            await this.HandleNoRequest(stoppingToken);
                        }
                    }
                }
                this.logger.LogInformation("Stopping service");
            }
            catch (OperationCanceledException)
            {
                this.logger.LogWarning("The operation was cancelled");
                Environment.ExitCode = 1;
            }
            this.hostApplicationLifetime.StopApplication();
        }

        private async Task HandleNoRequest(CancellationToken stoppingToken)
        {
            this.logger.LogInformation("No play session request detected");

            bool shouldShutdown = false;

            ServiceState kernelDriverState;
            try
            {
                kernelDriverState = this.vanguardManager.KernelDriverState;
            }
            catch (ServiceManagerException ex)
            {
                this.logger.LogError(ex, "Could not probe for kernel driver status");
                Environment.ExitCode = -1;
                return;
            }
            if (kernelDriverState != ServiceState.SERVICE_STOPPED)
            {
                this.logger.LogWarning("Kernel driver is active but no play session was requested");
                shouldShutdown = true;
            }

            ServiceState userServiceState;
            try
            {
                userServiceState = this.vanguardManager.UserLevelServiceState;
            }
            catch (ServiceManagerException ex)
            {
                this.logger.LogError(ex, "Could not probe for user service status");
                Environment.ExitCode = -1;
                return;
            }
            if (userServiceState != ServiceState.SERVICE_STOPPED)
            {
                this.logger.LogWarning("User service is active but no play session was requested");
                shouldShutdown = true;
            }

            if (shouldShutdown)
            {
                try
                {
                    this.vanguardManager.DeactivateVanguard();
                    await this.vanguardManager.ShutdownVanguard(stoppingToken);
                }
                catch (ServiceManagerException ex)
                {
                    this.logger.LogError(ex, "Could not shut down Vanguard");
                    Environment.ExitCode = -1;
                }
            }
            else
            {
                this.logger.LogInformation("Vanguard is already stopped");
            }
        }

        private async Task HandleRequest(CancellationToken stoppingToken)
        {
            this.logger.LogInformation("Request detected");

            ServiceState kernelDriverState;
            try
            {
                kernelDriverState = this.vanguardManager.KernelDriverState;
            }
            catch (ServiceManagerException ex)
            {
                this.logger.LogError(ex, "Could not probe for kernel driver status");
                Environment.ExitCode = -1;
                return;
            }

            if (kernelDriverState == ServiceState.SERVICE_RUNNING)
            {
                this.logger.LogInformation("Vanguard is running");
                this.logger.LogInformation("Waiting for play session start");

                try
                {
                    await this.gameManager.WaitForPlaySessionStart(stoppingToken);
                }
                catch (ServiceManagerException ex)
                {
                    this.logger.LogError(ex, "Could not wait for play session start");
                    Environment.ExitCode = -1;
                    return;
                }

                this.logger.LogInformation("Play session started");
                this.logger.LogInformation("Request complete. Deleting request");

                try
                {
                    this.requestManager.DeleteRequest();
                }
                catch (RequestManagerException ex)
                {
                    this.logger.LogError(ex, "Could not delete request");
                    Environment.ExitCode = -1;
                    return;
                }

                this.logger.LogInformation("Request deleted");
                this.logger.LogInformation("Deactivating Vanguard");

                try
                {
                    this.vanguardManager.DeactivateVanguard();
                }
                catch (ServiceManagerException ex)
                {
                    this.logger.LogError(ex, "Could not deactivate Vanguard");
                    Environment.ExitCode = -1;
                    return;
                }

                this.logger.LogInformation("Vanguard deactivated");
                this.logger.LogInformation("Waiting for play session end");

                try
                {
                    await this.gameManager.WaitForPlaySessionEnd(stoppingToken);
                }
                catch (ServiceManagerException ex)
                {
                    this.logger.LogError(ex, "Could not wait for play session end");
                    Environment.ExitCode = -1;
                    return;
                }

                this.logger.LogInformation("Play session ended");
                this.logger.LogInformation("Shutting down Vanguard");

                try
                {
                    await this.vanguardManager.ShutdownVanguard(stoppingToken);
                }
                catch (ServiceManagerException ex)
                {
                    this.logger.LogError(ex, "Could not shut down Vanguard");
                    Environment.ExitCode = -1;
                    return;
                }

                this.logger.LogInformation("Vanguard is shut down");
            }
            else
            {
                this.logger.LogInformation("Vanguard is not running");
                try
                {
                    this.vanguardManager.ActivateVanguard();
                }
                catch (ServiceManagerException ex)
                {
                    this.logger.LogError(ex, "Could not activate Vanguard");
                    Environment.ExitCode = -1;
                    return;
                }
                this.logger.LogInformation("Waiting for reboot");
            }
        }
    }
}
