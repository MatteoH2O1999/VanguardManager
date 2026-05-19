// Copyright (C) 2026 Matteo Dell'Acqua
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY, without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
// See the
// GNU Affero General Public License for more details.
//
// You should have received a copy of the GNU Affero General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

using System.ServiceProcess;
using Microsoft.Extensions.Logging;

namespace Manager.Vanguard.Common
{
    public class ServiceManager(ILogger<ServiceManager> Logger)
    {
        private readonly ILogger logger = Logger;

        public void Start(string serviceName)
        {
            ServiceController service = new(serviceName);
            service.Start();
        }

        public void Stop(string serviceName)
        {
            ServiceController service = new(serviceName);
            if (!service.CanStop)
            {
                throw new InvalidOperationException($"Service {serviceName} cannot be stopped by LocalService.");
            }
            service.Stop();
        }

        public void SetStart(string serviceName, ServiceStartMode startMode)
        {
            throw new NotImplementedException();
        }

        public void SetPermissions(string serviceName)
        {
            throw new NotSupportedException();
        }
    }
}
