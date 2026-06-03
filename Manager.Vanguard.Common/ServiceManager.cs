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

using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
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
                    Win32Error.GetExceptionForLastError()
                    ?? throw new ServiceManagerException(
                        "Service manager handle is invalid: last error must be a failure."
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
                    Win32Error.GetExceptionForLastError()
                    ?? throw new ServiceManagerException("Service handle is invalid: last error must be a failure");
                this.LogStartInvalidHandle(serviceName, ex);
                throw new ServiceManagerException(ex);
            }

            if (!StartService(service))
            {
                Exception ex =
                    Win32Error.GetExceptionForLastError()
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
                    Win32Error.GetExceptionForLastError()
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
                    Win32Error.GetExceptionForLastError()
                    ?? throw new ServiceManagerException("Service was not stopped: last error must be a failure");
                this.LogStopError(serviceName, ex);
                throw new ServiceManagerException(ex);
            }

            this.LogStopped(serviceName);
        }

        public void SetStart(string serviceName, ServiceStartType startMode)
        {
            if (startMode == ServiceStartType.SERVICE_NO_CHANGE)
            {
                throw new ArgumentException("Do not call this method if not changing the start type");
            }

            this.LogSetStart(serviceName, startMode);

            using var service = OpenService(this.scm, serviceName, ServiceAccessTypes.SERVICE_CHANGE_CONFIG);
            if (service.IsInvalid)
            {
                Exception ex =
                    Win32Error.GetExceptionForLastError()
                    ?? throw new ServiceManagerException("Service handle is invalid: last error must be a failure");
                this.LogSetStartInvalidHandle(serviceName, ex);
                throw new ServiceManagerException(ex);
            }

            if (
                !ChangeServiceConfig(
                    service,
                    ServiceTypes.SERVICE_NO_CHANGE,
                    startMode,
                    ServiceErrorControlType.SERVICE_NO_CHANGE
                )
            )
            {
                Exception ex =
                    Win32Error.GetExceptionForLastError()
                    ?? throw new ServiceManagerException(
                        "Service config was not changed: last error must be a failure"
                    );
                this.LogSetStartError(serviceName, startMode, ex);
                throw new ServiceManagerException(ex);
            }

            this.LogSetStartCompleted(serviceName, startMode);
        }

        public bool CheckPermissions(string serviceName)
        {
            this.LogCheckPermissions(serviceName);

            using var service = OpenService(
                this.scm,
                serviceName,
                ServiceAccessTypes.SERVICE_START
                    | ServiceAccessTypes.SERVICE_STOP
                    | ServiceAccessTypes.SERVICE_CHANGE_CONFIG
            );

            if (service.IsInvalid)
            {
                this.LogCheckedPermissionsFalse(serviceName);
                return false;
            }
            this.LogCheckedPermissionsTrue(serviceName);
            return true;
        }

        private CommonSecurityDescriptor GetPermissions(string serviceName)
        {
            this.LogGetPermissions(serviceName);

            using var service = OpenService(
                this.scm,
                serviceName,
                (ServiceAccessTypes)ServiceAccessRights.READ_CONTROL
            );
            if (service.IsInvalid)
            {
                Exception ex =
                    Win32Error.GetExceptionForLastError()
                    ?? throw new ServiceManagerException("Service handle is invalid: last error must be a failure");
                this.LogGetPermissionsInvalidHandle(serviceName, ex);
                throw new ServiceManagerException(ex);
            }

            if (!QueryServiceObjectSecurity(service, SECURITY_INFORMATION.DACL_SECURITY_INFORMATION, out var secRes))
            {
                Exception ex =
                    Win32Error.GetExceptionForLastError()
                    ?? throw new ServiceManagerException(
                        "Could not query service object security: last error must be a failure"
                    );
                this.LogGetPermissionsError(serviceName, ex);
                throw new ServiceManagerException(ex);
            }

            using var securityObject = secRes;
            if (
                !ConvertSecurityDescriptorToStringSecurityDescriptor(
                    secRes,
                    SDDL_REVISION.SDDL_REVISION_1,
                    SECURITY_INFORMATION.DACL_SECURITY_INFORMATION,
                    out var stringSecRes,
                    out uint len
                )
            )
            {
                Exception ex =
                    Win32Error.GetExceptionForLastError()
                    ?? throw new ServiceManagerException(
                        "Could not convert security descriptor to string handle: last error must be a failure"
                    );
                this.LogGetPermissionsConvertError(serviceName, ex);
                throw new ServiceManagerException(ex);
            }

            using var stringSecurityObject = stringSecRes;
            string? sddl = stringSecurityObject.ToString((int)len, CharSet.Auto);
            if (sddl is null)
            {
                this.LogGetPermissionsToStringError(serviceName);
                throw new ServiceManagerException("Could not convert string handle to string");
            }
            this.LogGetPermissionsSuccess(serviceName, sddl);

            return new(false, false, sddl);
        }

        private void SetPermissionsInternal(string serviceName, string sddl)
        {
            using var descriptor = ConvertStringSecurityDescriptorToSecurityDescriptor(sddl);
            if (!GetSecurityDescriptorDacl(descriptor, out bool isDaclPresent, out var dacl, out _))
            {
                Exception ex =
                    Win32Error.GetExceptionForLastError()
                    ?? throw new ServiceManagerException("Failed to get DACL: last error must be a failure");
                throw new ServiceManagerException(ex);
            }

            if (!isDaclPresent)
            {
                throw new ArgumentException("sddl was null");
            }

            Win32Error result = SetNamedSecurityInfo(
                serviceName,
                SE_OBJECT_TYPE.SE_SERVICE,
                SECURITY_INFORMATION.DACL_SECURITY_INFORMATION,
                ppDacl: dacl
            );

            if (!result.Succeeded)
            {
                ProcessStartInfo processStartInfo = new()
                {
                    UseShellExecute = true,
                    FileName = Path.Combine(Environment.SystemDirectory, "sc.exe"),
                    Verb = "runas",
                    WorkingDirectory = Environment.SystemDirectory,
                };
                processStartInfo.ArgumentList.Add("sdset");
                processStartInfo.ArgumentList.Add(serviceName);
                processStartInfo.ArgumentList.Add(sddl);

                using Process p = new() { StartInfo = processStartInfo };

                try
                {
                    p.Start();
                }
                catch (Win32Exception ex)
                {
                    throw new ServiceManagerException(ex);
                }

                p.WaitForExit();

                if (p.ExitCode != 0)
                {
                    throw new ServiceManagerException($"sc.exe failed with exit code {p.ExitCode}");
                }
            }
        }

        public void SetServicePermissions(string serviceName)
        {
            this.SetPermissions(serviceName, this.serviceAccount.SID);
        }

        public void SetCurrentAccountPermissions(string serviceName)
        {
            this.SetPermissions(
                serviceName,
                WindowsIdentity.GetCurrent().User
                    ?? throw new ServiceManagerException("Could not obtain current user SID")
            );
        }

        public void SetPermissions(string serviceName, SecurityIdentifier sid)
        {
            this.LogSetPermissions(serviceName, sid);

            CommonSecurityDescriptor currentPermissions = this.GetPermissions(serviceName);
            string currentSddl = currentPermissions.GetSddlForm(AccessControlSections.All);
            this.LogSetPermissionsCurrentSDDL(serviceName, currentSddl);

            DiscretionaryAcl dacl =
                currentPermissions.DiscretionaryAcl ?? throw new ServiceManagerException("DACL cannot be null");
            dacl.Purge(sid);
            dacl.AddAccess(
                AccessControlType.Allow,
                sid,
                (int)(
                    ServiceAccessRights.SERVICE_STOP
                    | ServiceAccessRights.SERVICE_START
                    | ServiceAccessRights.SERVICE_CHANGE_CONFIG
                ),
                InheritanceFlags.None,
                PropagationFlags.None
            );

            string newSddl = currentPermissions.GetSddlForm(AccessControlSections.All);
            this.LogSetPermissionsNewSDDL(serviceName, newSddl);

            if (currentSddl == newSddl)
            {
                this.LogSetPermissionsSkip(serviceName, sid);
                return;
            }

            this.LogSetPermissionsPerform(serviceName);
            this.SetPermissionsInternal(serviceName, newSddl);
            this.LogSetPermissionsSuccess(serviceName, sid);
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

        [LoggerMessage(LogLevel.Error, "Error while opening handle to SCM")]
        private partial void LogErrorOpenSCM(Exception ex);

        [LoggerMessage(LogLevel.Trace, "Handle to SCM successfully opened")]
        private partial void LogOpenedSCM();

        #endregion

        #region Start Logging

        [LoggerMessage(LogLevel.Debug, "Starting service {serviceName}")]
        private partial void LogStart(string serviceName);

        [LoggerMessage(
            LogLevel.Error,
            $"Error while opening handle to service {{serviceName}} with desired access {nameof(ServiceAccessTypes.SERVICE_START)}"
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
            $"Error while opening handle to service {{serviceName}} with desired access {nameof(ServiceAccessTypes.SERVICE_STOP)}"
        )]
        private partial void LogStopInvalidHandle(string serviceName, Exception ex);

        [LoggerMessage(LogLevel.Error, "Error while stopping service {serviceName}")]
        private partial void LogStopError(string serviceName, Exception ex);

        [LoggerMessage(LogLevel.Debug, "Service {serviceName} successfully stopped")]
        private partial void LogStopped(string serviceName);

        #endregion

        #region SetStart Logging

        [LoggerMessage(LogLevel.Debug, "Setting start mode of service {serviceName} to {startType}")]
        private partial void LogSetStart(string serviceName, ServiceStartType startType);

        [LoggerMessage(
            LogLevel.Error,
            $"Error while opening handle to service {{serviceName}} with desired access {nameof(ServiceAccessTypes.SERVICE_CHANGE_CONFIG)}"
        )]
        private partial void LogSetStartInvalidHandle(string serviceName, Exception ex);

        [LoggerMessage(LogLevel.Error, "Error while setting start mode of service {serviceName} to {startType}")]
        private partial void LogSetStartError(string serviceName, ServiceStartType startType, Exception ex);

        [LoggerMessage(LogLevel.Debug, "Start mode of service {serviceName} successfully set to {startType}")]
        private partial void LogSetStartCompleted(string serviceName, ServiceStartType startType);

        #endregion

        #region CheckPermissions Logging

        [LoggerMessage(
            LogLevel.Debug,
            "Checking whether current account can open handle to service {serviceName} with permissions "
                + $"{nameof(ServiceAccessTypes.SERVICE_START)}, {nameof(ServiceAccessTypes.SERVICE_STOP)} "
                + $"and {nameof(ServiceAccessTypes.SERVICE_CHANGE_CONFIG)}"
        )]
        private partial void LogCheckPermissions(string serviceName);

        [LoggerMessage(
            LogLevel.Debug,
            "Current account can open handle to service {serviceName} with permissions"
                + $"{nameof(ServiceAccessTypes.SERVICE_START)}, {nameof(ServiceAccessTypes.SERVICE_STOP)} "
                + $"and {nameof(ServiceAccessTypes.SERVICE_CHANGE_CONFIG)}"
        )]
        private partial void LogCheckedPermissionsTrue(string serviceName);

        [LoggerMessage(
            LogLevel.Debug,
            "Current account can't open handle to service {serviceName} with permissions"
                + $"{nameof(ServiceAccessTypes.SERVICE_START)}, {nameof(ServiceAccessTypes.SERVICE_STOP)} "
                + $"and {nameof(ServiceAccessTypes.SERVICE_CHANGE_CONFIG)}"
        )]
        private partial void LogCheckedPermissionsFalse(string serviceName);

        #endregion

        #region GetPermissions Logging

        [LoggerMessage(LogLevel.Debug, "Getting security descriptor from service {serviceName}")]
        private partial void LogGetPermissions(string serviceName);

        [LoggerMessage(
            LogLevel.Error,
            $"Error while opening handle to service {{serviceName}} with desired access {nameof(ServiceAccessRights.READ_CONTROL)}"
        )]
        private partial void LogGetPermissionsInvalidHandle(string serviceName, Exception ex);

        [LoggerMessage(LogLevel.Error, "Error while getting security descriptor from service {serviceName}")]
        private partial void LogGetPermissionsError(string serviceName, Exception ex);

        [LoggerMessage(
            LogLevel.Error,
            "Error while converting security descriptor from service {serviceName} to string"
        )]
        private partial void LogGetPermissionsConvertError(string serviceName, Exception ex);

        [LoggerMessage(
            LogLevel.Error,
            "Error while converting string security description handle from service {serviceName} into string"
        )]
        private partial void LogGetPermissionsToStringError(string serviceName);

        [LoggerMessage(LogLevel.Debug, "Successfully retrieved security descriptor from service {serviceName}: {sddl}")]
        private partial void LogGetPermissionsSuccess(string serviceName, string sddl);

        #endregion

        #region SetPermissions Logging

        [LoggerMessage(
            LogLevel.Debug,
            $"Setting permissions {nameof(ServiceAccessRights.SERVICE_START)}, {nameof(ServiceAccessRights.SERVICE_STOP)} "
                + $"and {nameof(ServiceAccessRights.SERVICE_CHANGE_CONFIG)} for account {{sid}} on service {{serviceName}}"
        )]
        private partial void LogSetPermissions(string serviceName, SecurityIdentifier sid);

        [LoggerMessage(LogLevel.Debug, "Current DACL for service {serviceName}: {sddl}")]
        private partial void LogSetPermissionsCurrentSDDL(string serviceName, string sddl);

        [LoggerMessage(LogLevel.Debug, "New DACL for service {serviceName}: {sddl}")]
        private partial void LogSetPermissionsNewSDDL(string serviceName, string sddl);

        [LoggerMessage(
            LogLevel.Trace,
            "Adding the required permissions for account {sid} on service {serviceName} would not modify DACL. Skipping"
        )]
        private partial void LogSetPermissionsSkip(string serviceName, SecurityIdentifier sid);

        [LoggerMessage(LogLevel.Trace, "Setting new DACL for service {serviceName}")]
        private partial void LogSetPermissionsPerform(string serviceName);

        [LoggerMessage(
            LogLevel.Debug,
            $"Permissions {nameof(ServiceAccessRights.SERVICE_START)}, {nameof(ServiceAccessRights.SERVICE_STOP)} "
                + $"and {nameof(ServiceAccessRights.SERVICE_CHANGE_CONFIG)} for account {{sid}} on service "
                + "{serviceName} successfully set"
        )]
        private partial void LogSetPermissionsSuccess(string serviceName, SecurityIdentifier sid);

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
