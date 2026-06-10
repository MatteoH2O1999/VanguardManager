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
                this.LogAcquiringServiceLock();
                IDisposable? serviceLock;
                try
                {
                    serviceLock = Locks.SERVICE.TryAcquire();
                }
                catch (LockException ex)
                {
                    this.LogServiceLockError(ex);
                    Environment.ExitCode = -1;
                    this.hostApplicationLifetime.StopApplication();
                    return;
                }

                if (serviceLock is null)
                {
                    this.LogServiceLockAlreadyInUse();
                    Environment.ExitCode = -1;
                }
                else
                {
                    this.LogServiceLockAcquired();
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
                this.LogStoppingService();
            }
            catch (OperationCanceledException)
            {
                this.LogCancelled();
                Environment.ExitCode = 1;
            }
            this.hostApplicationLifetime.StopApplication();
        }

        private async Task HandleNoRequest(CancellationToken stoppingToken)
        {
            this.LogNoRequest();

            bool shouldShutdown = false;

            ServiceState kernelDriverState;
            try
            {
                kernelDriverState = this.vanguardManager.KernelDriverState;
            }
            catch (ServiceManagerException ex)
            {
                this.LogProbeKernelDriverError(ex);
                Environment.ExitCode = -1;
                return;
            }
            if (kernelDriverState != ServiceState.SERVICE_STOPPED)
            {
                this.LogKernelDriverActiveWithoutRequest();
                shouldShutdown = true;
            }

            ServiceState userServiceState;
            try
            {
                userServiceState = this.vanguardManager.UserLevelServiceState;
            }
            catch (ServiceManagerException ex)
            {
                this.LogProbeUserServiceError(ex);
                Environment.ExitCode = -1;
                return;
            }
            if (userServiceState != ServiceState.SERVICE_STOPPED)
            {
                this.LogUserServiceActiveWithoutRequest();
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
                    this.LogVanguardShutdownError(ex);
                    Environment.ExitCode = -1;
                }
            }
            else
            {
                this.LogVanguardAlreadyStopped();
            }
        }

        private async Task HandleRequest(CancellationToken stoppingToken)
        {
            this.LogRequest();

            ServiceState kernelDriverState;
            try
            {
                kernelDriverState = this.vanguardManager.KernelDriverState;
            }
            catch (ServiceManagerException ex)
            {
                this.LogProbeKernelDriverError(ex);
                Environment.ExitCode = -1;
                return;
            }

            if (kernelDriverState == ServiceState.SERVICE_RUNNING)
            {
                this.LogVanguardRunning();
                this.LogWaitForPlaySessionStart();

                try
                {
                    await this.gameManager.WaitForPlaySessionStart(stoppingToken);
                }
                catch (Exception ex)
                {
                    this.LogWaitForPlaySessionStartError(ex);
                    Environment.ExitCode = -1;
                    return;
                }

                this.LogPlaySessionStart();
                this.LogDeleteRequest();

                try
                {
                    this.requestManager.DeleteRequest();
                }
                catch (RequestManagerException ex)
                {
                    this.LogDeleteRequestError(ex);
                    Environment.ExitCode = -1;
                    return;
                }

                this.LogRequestDeleted();
                this.LogDeactivatingVanguard();

                try
                {
                    this.vanguardManager.DeactivateVanguard();
                }
                catch (ServiceManagerException ex)
                {
                    this.LogDeactivatingVanguardError(ex);
                    Environment.ExitCode = -1;
                    return;
                }

                this.LogDeactivatedVanguard();
                this.LogWaitForPlaySessionEnd();

                try
                {
                    await this.gameManager.WaitForPlaySessionEnd(stoppingToken);
                }
                catch (ServiceManagerException ex)
                {
                    this.LogWaitForPlaySessionEndError(ex);
                    Environment.ExitCode = -1;
                    return;
                }

                this.LogPlaySessionEnd();
                this.LogShuttingDownVanguard();

                try
                {
                    await this.vanguardManager.ShutdownVanguard(stoppingToken);
                }
                catch (ServiceManagerException ex)
                {
                    this.LogShuttingDownVanguardError(ex);
                    Environment.ExitCode = -1;
                    return;
                }

                this.LogVanguardShutdown();
            }
            else
            {
                this.LogVanguardNotRunning();
                try
                {
                    this.vanguardManager.ActivateVanguard();
                }
                catch (ServiceManagerException ex)
                {
                    this.LogVanguardActivationError(ex);
                    Environment.ExitCode = -1;
                    return;
                }
                this.LogWaitForReboot();
            }
        }

        #region ExecuteAsync Logging

        [LoggerMessage(LogLevel.Debug, "Acquiring service lock")]
        private partial void LogAcquiringServiceLock();

        [LoggerMessage(LogLevel.Error, "Could not acquire service lock")]
        private partial void LogServiceLockError(LockException ex);

        [LoggerMessage(LogLevel.Error, "Service lock already in use by another process")]
        private partial void LogServiceLockAlreadyInUse();

        [LoggerMessage(LogLevel.Information, "Service lock acquired")]
        private partial void LogServiceLockAcquired();

        [LoggerMessage(LogLevel.Information, "Stopping service")]
        private partial void LogStoppingService();

        [LoggerMessage(LogLevel.Warning, "The operation was cancelled")]
        private partial void LogCancelled();

        #endregion

        #region HandleNoRequest Logging

        [LoggerMessage(LogLevel.Information, "No play session request detected")]
        private partial void LogNoRequest();

        [LoggerMessage(LogLevel.Error, "Could not probe for kernel driver status")]
        private partial void LogProbeKernelDriverError(ServiceManagerException ex);

        [LoggerMessage(LogLevel.Warning, "Kernel driver is active but no play session was requested")]
        private partial void LogKernelDriverActiveWithoutRequest();

        [LoggerMessage(LogLevel.Error, "Could not probe for user service status")]
        private partial void LogProbeUserServiceError(ServiceManagerException ex);

        [LoggerMessage(LogLevel.Warning, "User service is active but no play session was requested")]
        private partial void LogUserServiceActiveWithoutRequest();

        [LoggerMessage(LogLevel.Error, "Could not shut down Vanguard")]
        private partial void LogVanguardShutdownError(ServiceManagerException ex);

        [LoggerMessage(LogLevel.Information, "Vanguard is already stopped")]
        private partial void LogVanguardAlreadyStopped();

        #endregion

        #region HandleRequestLogging

        [LoggerMessage(LogLevel.Information, "Request detected")]
        private partial void LogRequest();

        [LoggerMessage(LogLevel.Information, "Vanguard is running")]
        private partial void LogVanguardRunning();

        [LoggerMessage(LogLevel.Information, "Waiting for play session start")]
        private partial void LogWaitForPlaySessionStart();

        [LoggerMessage(LogLevel.Error, "Could not wait for play session start")]
        private partial void LogWaitForPlaySessionStartError(Exception ex);

        [LoggerMessage(LogLevel.Information, "Play session started")]
        private partial void LogPlaySessionStart();

        [LoggerMessage(LogLevel.Information, "Request complete. Deleting request")]
        private partial void LogDeleteRequest();

        [LoggerMessage(LogLevel.Error, "Could not delete request")]
        private partial void LogDeleteRequestError(RequestManagerException ex);

        [LoggerMessage(LogLevel.Information, "Request deleted")]
        private partial void LogRequestDeleted();

        [LoggerMessage(LogLevel.Information, "Deactivating Vanguard")]
        private partial void LogDeactivatingVanguard();

        [LoggerMessage(LogLevel.Error, "Could not deactivate Vanguard")]
        private partial void LogDeactivatingVanguardError(ServiceManagerException ex);

        [LoggerMessage(LogLevel.Information, "Vanguard deactivated")]
        private partial void LogDeactivatedVanguard();

        [LoggerMessage(LogLevel.Information, "Waiting for play session end")]
        private partial void LogWaitForPlaySessionEnd();

        [LoggerMessage(LogLevel.Error, "Could not wait for play session end")]
        private partial void LogWaitForPlaySessionEndError(ServiceManagerException ex);

        [LoggerMessage(LogLevel.Information, "Play session ended")]
        private partial void LogPlaySessionEnd();

        [LoggerMessage(LogLevel.Information, "Shutting down Vanguard")]
        private partial void LogShuttingDownVanguard();

        [LoggerMessage(LogLevel.Error, "Could not shut down Vanguard")]
        private partial void LogShuttingDownVanguardError(ServiceManagerException ex);

        [LoggerMessage(LogLevel.Information, "Vanguard is shut down")]
        private partial void LogVanguardShutdown();

        [LoggerMessage(LogLevel.Information, "Vanguard is not running")]
        private partial void LogVanguardNotRunning();

        [LoggerMessage(LogLevel.Error, "Could not activate Vanguard")]
        private partial void LogVanguardActivationError(ServiceManagerException ex);

        [LoggerMessage(LogLevel.Information, "Waiting for reboot")]
        private partial void LogWaitForReboot();

        #endregion
    }
}
