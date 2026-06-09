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

using System.ComponentModel;
using Manager.Vanguard.Common;
using static Vanara.PInvoke.AdvApi32;

namespace Manager.Vanguard.Service
{
    public sealed partial class Worker(
        ILogger<Worker> Logger,
        IHostApplicationLifetime HostApplicationLifetime,
        ServiceManager SCM,
        RequestManager RequestManager
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
        private readonly ServiceManager serviceManager = SCM;
        private readonly RequestManager requestManager = RequestManager;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
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
            this.hostApplicationLifetime.StopApplication();
        }

        private async Task HandleNoRequest(CancellationToken stoppingToken)
        {
            this.logger.LogInformation("No play session request detected");

            bool shouldShutdown = false;

            ServiceState kernelDriverState;
            try
            {
                kernelDriverState = this.serviceManager.CheckStatus(ApplicationData.KernelLevelServiceName);
            }
            catch (Win32Exception ex)
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
                userServiceState = this.serviceManager.CheckStatus(ApplicationData.UserLevelServiceName);
            }
            catch (Win32Exception ex)
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
                    await this.ShutdownVanguard(stoppingToken);
                }
                catch (Win32Exception ex)
                {
                    this.logger.LogError(ex, "Could not shut down Vanguard");
                    Environment.ExitCode = -1;
                }
            }
        }

        private async Task HandleRequest(CancellationToken stoppingToken)
        {
            this.logger.LogInformation("Request detected");
        }

        private async Task ShutdownVanguard(CancellationToken stoppingToken)
        {
            this.logger.LogInformation("Shutting down Vanguard");

            this.serviceManager.SetStart(ApplicationData.KernelLevelServiceName, ServiceStartType.SERVICE_DISABLED);
            this.logger.LogInformation(
                $"Kernel level driver {{}} start mode set to {nameof(ServiceStartType.SERVICE_DISABLED)}",
                ApplicationData.KernelLevelServiceName
            );

            this.serviceManager.SetStart(ApplicationData.UserLevelServiceName, ServiceStartType.SERVICE_DISABLED);
            this.logger.LogInformation(
                $"User level service {{}} start mode set to {nameof(ServiceStartType.SERVICE_DISABLED)}",
                ApplicationData.UserLevelServiceName
            );

            if (this.serviceManager.CheckStatus(ApplicationData.KernelLevelServiceName) != ServiceState.SERVICE_STOPPED)
            {
                this.serviceManager.Stop(ApplicationData.KernelLevelServiceName);
                this.logger.LogInformation(
                    "Requested immediate shutdown of kernel level driver {}",
                    ApplicationData.KernelLevelServiceName
                );
            }

            if (this.serviceManager.CheckStatus(ApplicationData.UserLevelServiceName) != ServiceState.SERVICE_STOPPED)
            {
                this.serviceManager.Stop(ApplicationData.UserLevelServiceName);
                this.logger.LogInformation(
                    "Requested immediate shutdown of user level service {}",
                    ApplicationData.UserLevelServiceName
                );
            }

            await this.WaitForVanguardShutdown(stoppingToken);

            this.logger.LogInformation("Vanguard successfully shut down");
        }

        private async Task WaitForVanguardShutdown(CancellationToken stoppingToken)
        {
            ServiceState kernelDriverState = this.serviceManager.CheckStatus(ApplicationData.KernelLevelServiceName);
            ServiceState userServiceState = this.serviceManager.CheckStatus(ApplicationData.UserLevelServiceName);

            this.logger.LogInformation(
                "Kernel level driver {} state: {}; User level service {} state: {}",
                ApplicationData.KernelLevelServiceName,
                kernelDriverState,
                ApplicationData.UserLevelServiceName,
                userServiceState
            );

            while (
                kernelDriverState != ServiceState.SERVICE_STOPPED || userServiceState != ServiceState.SERVICE_STOPPED
            )
            {
                this.logger.LogInformation($"Waiting {SHUTDOWN_CHECK_INTERVAL_SECONDS} seconds");
                await Task.Delay(SHUTDOWN_CHECK_INTERVAL, stoppingToken);

                kernelDriverState = this.serviceManager.CheckStatus(ApplicationData.KernelLevelServiceName);
                userServiceState = this.serviceManager.CheckStatus(ApplicationData.UserLevelServiceName);

                this.logger.LogInformation(
                    "Kernel level driver {} state: {}; User level service {} state: {}",
                    ApplicationData.KernelLevelServiceName,
                    kernelDriverState,
                    ApplicationData.UserLevelServiceName,
                    userServiceState
                );
            }
        }
    }
}
