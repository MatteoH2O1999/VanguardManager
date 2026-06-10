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
    internal sealed class VanguardManager(ILogger<VanguardManager> Logger, ServiceManager SCM)
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
            this.logger.LogInformation("Activating Vanguard services");

            this.serviceManager.SetStart(ApplicationData.KernelLevelServiceName, ServiceStartType.SERVICE_SYSTEM_START);
            this.logger.LogInformation(
                $"Kernel level driver {{}} start mode set to {nameof(ServiceStartType.SERVICE_SYSTEM_START)}",
                ApplicationData.KernelLevelServiceName
            );

            this.serviceManager.SetStart(ApplicationData.UserLevelServiceName, ServiceStartType.SERVICE_DEMAND_START);
            this.logger.LogInformation(
                $"User level service {{}} start mode set to {nameof(ServiceStartType.SERVICE_DEMAND_START)}",
                ApplicationData.UserLevelServiceName
            );

            this.logger.LogInformation("Vanguard services activated");
        }

        public void DeactivateVanguard()
        {
            this.logger.LogInformation("Deactivating Vanguard services");

            this.serviceManager.SetStart(ApplicationData.UserLevelServiceName, ServiceStartType.SERVICE_DISABLED);
            this.logger.LogInformation(
                $"User level service {{}} start mode set to {nameof(ServiceStartType.SERVICE_DISABLED)}",
                ApplicationData.UserLevelServiceName
            );

            this.serviceManager.SetStart(ApplicationData.KernelLevelServiceName, ServiceStartType.SERVICE_DISABLED);
            this.logger.LogInformation(
                $"Kernel level driver {{}} start mode set to {nameof(ServiceStartType.SERVICE_DISABLED)}",
                ApplicationData.KernelLevelServiceName
            );

            this.logger.LogInformation("Vanguard services deactivated");
        }

        public async Task ShutdownVanguard(CancellationToken stoppingToken)
        {
            this.logger.LogInformation("Shutting down Vanguard services");

            if (UserLevelServiceState != ServiceState.SERVICE_STOPPED)
            {
                this.serviceManager.Stop(ApplicationData.UserLevelServiceName);
                this.logger.LogInformation(
                    "Requested immediate shutdown of user level service {}",
                    ApplicationData.UserLevelServiceName
                );
            }

            if (KernelDriverState != ServiceState.SERVICE_STOPPED)
            {
                this.serviceManager.Stop(ApplicationData.KernelLevelServiceName);
                this.logger.LogInformation(
                    "Requested immediate shutdown of kernel level driver {}",
                    ApplicationData.KernelLevelServiceName
                );
            }

            await this.WaitForVanguardShutdown(stoppingToken);

            this.logger.LogInformation("Vanguard services successfully shut down");
        }

        private async Task WaitForVanguardShutdown(CancellationToken stoppingToken)
        {
            ServiceState kernelDriverState = KernelDriverState;
            ServiceState userServiceState = UserLevelServiceState;

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

                kernelDriverState = KernelDriverState;
                userServiceState = UserLevelServiceState;

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
