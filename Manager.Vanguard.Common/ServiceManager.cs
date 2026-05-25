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
using Vanara.PInvoke;
using static Vanara.PInvoke.AdvApi32;

namespace Manager.Vanguard.Common
{
    public sealed partial class ServiceManager : IDisposable
    {
        private readonly ServiceAccount serviceAccount;
        private readonly SafeSC_HANDLE scm;
        private readonly ILogger logger;
        private bool disposed;

        public ServiceManager(ILogger<ServiceManager> logger, ServiceAccount serviceAccount)
        {
            this.logger = logger;
            this.serviceAccount = serviceAccount;
            this.disposed = false;

            this.LogOpenSCM();
            this.scm = OpenSCManager(null, null, ScManagerAccessTypes.SC_MANAGER_CONNECT);
            if (this.scm.IsInvalid)
            {
                Exception ex =
                    Win32Error.GetLastError().GetException()
                    ?? throw new ServiceManagerException(
                        "Service manager handle is invalid: last error MUST be a failure."
                    );
                this.LogErrorOpenSCM(ex);
                throw new ServiceManagerException(ex);
            }
            this.LogOpenedSCM();
        }

        public void Start(string serviceName)
        {
            this.LogStart(serviceName);
            using var service = OpenService(this.scm, serviceName, ServiceAccessTypes.SERVICE_START);
            if (service.IsInvalid)
            {
                Exception ex =
                    Win32Error.GetLastError().GetException()
                    ?? throw new ServiceManagerException("Service handle is invalid: last error must be a failure");
                this.LogStartInvalidHandle(serviceName, ex);
                throw new ServiceManagerException(ex);
            }
            if (!StartService(service))
            {
                Exception ex =
                    Win32Error.GetLastError().GetException()
                    ?? throw new ServiceManagerException("Service was not started: last error must be a failure");
                this.LogStartError(serviceName, ex);
                throw new ServiceManagerException(ex);
            }
            this.LogStarted(serviceName);
        }

        public void Stop(string serviceName)
        {
            this.LogStop(serviceName);
            using var service = OpenService(this.scm, serviceName, ServiceAccessTypes.SERVICE_STOP);
            if (service.IsInvalid)
            {
                Exception ex =
                    Win32Error.GetLastError().GetException()
                    ?? throw new ServiceManagerException("Service handle is invalid: last error must be a failure");
                this.LogStopInvalidHandle(serviceName, ex);
                throw new ServiceManagerException(ex);
            }
            SERVICE_CONTROL_STATUS_REASON_PARAMS reason = new()
            {
                dwReason =
                    SERVICE_STOP_REASON.SERVICE_STOP_REASON_FLAG_PLANNED
                    | SERVICE_STOP_REASON.SERVICE_STOP_REASON_MAJOR_NONE
                    | SERVICE_STOP_REASON.SERVICE_STOP_REASON_MINOR_NONE,
                pszComment = "Stop service as play session was not requested",
            };
            if (!StopService(service, ref reason))
            {
                Exception ex =
                    Win32Error.GetLastError().GetException()
                    ?? throw new ServiceManagerException("Service was not stopped: last error must be a failure");
                this.LogStopError(serviceName, ex);
                throw new ServiceManagerException(ex);
            }
            this.LogStopped(serviceName);
        }

        public void SetStart(string serviceName, ServiceStartMode startMode)
        {
            throw new NotImplementedException();
        }

        public bool CheckPermissions(string serviceName)
        {
            throw new NotImplementedException();
        }

        public void SetPermissions(string serviceName)
        {
            throw new NotSupportedException();
        }

        public void Dispose()
        {
            if (!this.disposed)
            {
                this.LogDisposing();
                this.scm.Dispose();
                this.disposed = true;
                this.LogEndDispose();
            }
            else
            {
                this.LogDisposed();
            }
        }

        #region Dispose Logging

        [LoggerMessage(LogLevel.Trace, $"{nameof(ServiceManager)} already disposed")]
        private partial void LogDisposed();

        [LoggerMessage(LogLevel.Trace, $"Disposing {nameof(ServiceManager)} instance")]
        private partial void LogDisposing();

        [LoggerMessage(LogLevel.Trace, $"Successfully disposed {nameof(ServiceManager)} instance")]
        private partial void LogEndDispose();

        #endregion

        #region Constructor Logging

        [LoggerMessage(LogLevel.Trace, "Opening handle to SCM")]
        private partial void LogOpenSCM();

        [LoggerMessage(LogLevel.Error, "Error while opening handle SCM")]
        private partial void LogErrorOpenSCM(Exception ex);

        [LoggerMessage(LogLevel.Trace, "Handle to SCM successfully opened")]
        private partial void LogOpenedSCM();

        #endregion

        #region Start Logging

        [LoggerMessage(LogLevel.Debug, "Starting service {serviceName}")]
        private partial void LogStart(string serviceName);

        [LoggerMessage(
            LogLevel.Error,
            "Error while opening handle to service {serviceName} with desired access SERVICE_START"
        )]
        private partial void LogStartInvalidHandle(string serviceName, Exception ex);

        [LoggerMessage(LogLevel.Error, "Error while starting service {serviceName}")]
        private partial void LogStartError(string serviceName, Exception ex);

        [LoggerMessage(LogLevel.Debug, "Service {serviceName} successfully started")]
        private partial void LogStarted(string serviceName);

        #endregion

        #region Stop Logging

        [LoggerMessage(LogLevel.Debug, "Stopping service {serviceName}")]
        private partial void LogStop(string serviceName);

        [LoggerMessage(
            LogLevel.Error,
            "Error while opening handle to service {serviceName} with desired access SERVICE_STOP"
        )]
        private partial void LogStopInvalidHandle(string serviceName, Exception ex);

        [LoggerMessage(LogLevel.Error, "Error while stopping service {serviceName}")]
        private partial void LogStopError(string serviceName, Exception ex);

        [LoggerMessage(LogLevel.Debug, "Service {serviceName} successfully stopped")]
        private partial void LogStopped(string serviceName);

        #endregion
    }

    public sealed class ServiceManagerException : Exception
    {
        public ServiceManagerException(string message)
            : base(message) { }

        public ServiceManagerException(Exception ex)
            : base($"Error in {nameof(ServiceManager)}", ex) { }
    }
}
