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

namespace Manager.Vanguard.Service
{
    public sealed partial class Worker(
        ILogger<Worker> Logger,
        IHostApplicationLifetime HostApplicationLifetime,
        ServiceManager SCM,
        RequestManager RequestManager
    ) : BackgroundService
    {
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
                this.logger.LogError("Could not acquire service lock", ex);
                this.hostApplicationLifetime.StopApplication();
                return;
            }

            if (serviceLock is null)
            {
                this.logger.LogError("Service lock already in use by another process");
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
            this.logger.LogInformation("No request detected");
        }

        private async Task HandleRequest(CancellationToken stoppingToken)
        {
            this.logger.LogInformation("Request detected");
        }
    }
}
