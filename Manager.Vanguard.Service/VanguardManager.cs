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
    internal sealed partial class VanguardManager(ILogger<VanguardManager> Logger, ServiceManager SCM)
    {
        private const int SHUTDOWN_CHECK_INTERVAL_SECONDS =
#if DEBUG
            1;
#else
            10;
#endif
        private const int SHUTDOWN_CHECK_INTERVAL = SHUTDOWN_CHECK_INTERVAL_SECONDS * 1000;

        private readonly ILogger logger = Logger;
        private readonly ServiceManager serviceManager = SCM;

        public ServiceState KernelDriverState =>
            this.serviceManager.CheckStatus(ApplicationData.KernelLevelServiceName);
        public ServiceState UserLevelServiceState =>
            this.serviceManager.CheckStatus(ApplicationData.UserLevelServiceName);

        public void ActivateVanguard()
        {
            this.LogActivatingVanguard();

            this.serviceManager.SetStart(ApplicationData.UserLevelServiceName, ServiceStartType.SERVICE_DEMAND_START);
            this.LogActivatedUserService(ApplicationData.UserLevelServiceName);

            this.serviceManager.SetStart(ApplicationData.KernelLevelServiceName, ServiceStartType.SERVICE_SYSTEM_START);
            this.LogActivatedKernelDriver(ApplicationData.KernelLevelServiceName);

            this.LogActivatedVanguard();
        }

        public void DeactivateVanguard()
        {
            this.LogDeactivatingVanguard();

            this.serviceManager.SetStart(ApplicationData.UserLevelServiceName, ServiceStartType.SERVICE_DISABLED);
            this.LogDeactivatedUserService(ApplicationData.UserLevelServiceName);

            this.serviceManager.SetStart(ApplicationData.KernelLevelServiceName, ServiceStartType.SERVICE_DISABLED);
            this.LogDeactivatedKernelDriver(ApplicationData.KernelLevelServiceName);

            this.LogDeactivatedVanguard();
        }

        public async Task ShutdownVanguard(CancellationToken stoppingToken)
        {
            this.LogShuttingDownVanguard();

            if (UserLevelServiceState != ServiceState.SERVICE_STOPPED)
            {
                this.serviceManager.Stop(ApplicationData.UserLevelServiceName);
                this.LogUserServiceShutdown(ApplicationData.UserLevelServiceName);
            }

            if (KernelDriverState != ServiceState.SERVICE_STOPPED)
            {
                this.serviceManager.Stop(ApplicationData.KernelLevelServiceName);
                this.LogKernelDriverShutdown(ApplicationData.KernelLevelServiceName);
            }

            this.LogWaitingForVanguardShutdown();
            await this.WaitForVanguardShutdown(stoppingToken);

            this.LogVanguardShutdown();
        }

        private async Task WaitForVanguardShutdown(CancellationToken stoppingToken)
        {
            string driverName = ApplicationData.KernelLevelServiceName;
            string serviceName = ApplicationData.UserLevelServiceName;
            ServiceState kernelDriverState = KernelDriverState;
            ServiceState userServiceState = UserLevelServiceState;

            this.LogVanguardState(driverName, kernelDriverState, serviceName, userServiceState);

            while (
                kernelDriverState != ServiceState.SERVICE_STOPPED || userServiceState != ServiceState.SERVICE_STOPPED
            )
            {
                this.LogWaitForVanguardShutdownDelay(SHUTDOWN_CHECK_INTERVAL_SECONDS);

                await Task.Delay(SHUTDOWN_CHECK_INTERVAL, stoppingToken);

                kernelDriverState = KernelDriverState;
                userServiceState = UserLevelServiceState;

                this.LogVanguardState(driverName, kernelDriverState, serviceName, userServiceState);
            }
        }

        #region ActivateVanguardLogging

        [LoggerMessage(LogLevel.Information, "Activating Vanguard services")]
        private partial void LogActivatingVanguard();

        [LoggerMessage(
            LogLevel.Information,
            $"User level service {{serviceName}} start mode set to {nameof(ServiceStartType.SERVICE_DEMAND_START)}"
        )]
        private partial void LogActivatedUserService(string serviceName);

        [LoggerMessage(
            LogLevel.Information,
            $"Kernel level driver {{driverName}} start mode set to {nameof(ServiceStartType.SERVICE_SYSTEM_START)}"
        )]
        private partial void LogActivatedKernelDriver(string driverName);

        [LoggerMessage(LogLevel.Information, "Vanguard services activated")]
        private partial void LogActivatedVanguard();

        #endregion

        #region DeactivateVanguard Logging

        [LoggerMessage(LogLevel.Information, "Deactivating Vanguard services")]
        private partial void LogDeactivatingVanguard();

        [LoggerMessage(
            LogLevel.Information,
            $"User level service {{serviceName}} start mode set to {nameof(ServiceStartType.SERVICE_DISABLED)}"
        )]
        private partial void LogDeactivatedUserService(string serviceName);

        [LoggerMessage(
            LogLevel.Information,
            $"Kernel level driver {{driverName}} start mode set to {nameof(ServiceStartType.SERVICE_DISABLED)}"
        )]
        private partial void LogDeactivatedKernelDriver(string driverName);

        [LoggerMessage(LogLevel.Information, "Vanguard services deactivated")]
        private partial void LogDeactivatedVanguard();

        #endregion

        #region ShutdownVanguard Logging

        [LoggerMessage(LogLevel.Information, "Shutting down Vanguard services")]
        private partial void LogShuttingDownVanguard();

        [LoggerMessage(LogLevel.Information, "Requested immediate shutdown of user level service {serviceName}")]
        private partial void LogUserServiceShutdown(string serviceName);

        [LoggerMessage(LogLevel.Information, "Requested immediate shutdown of kernel level driver {driverName}")]
        private partial void LogKernelDriverShutdown(string driverName);

        [LoggerMessage(LogLevel.Information, "Waiting for Vanguard services shutdown")]
        private partial void LogWaitingForVanguardShutdown();

        [LoggerMessage(LogLevel.Information, "Vanguard services successfully shut down")]
        private partial void LogVanguardShutdown();

        #endregion

        #region WaitForVanguardShutdown Logging

        [LoggerMessage(
            LogLevel.Debug,
            "Kernel level driver {driverName} state: {driverState}; User level service {serviceName} state: {serviceState}"
        )]
        private partial void LogVanguardState(
            string driverName,
            ServiceState driverState,
            string serviceName,
            ServiceState serviceState
        );

        [LoggerMessage(LogLevel.Trace, "Waiting {seconds} seconds")]
        private partial void LogWaitForVanguardShutdownDelay(int seconds);

        #endregion
    }
}
