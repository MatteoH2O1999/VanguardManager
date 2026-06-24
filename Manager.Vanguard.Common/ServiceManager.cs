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
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.Extensions.Logging;
using Vanara.InteropServices;
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
                throw new ServiceManagerException("Invalid handle to SCM", ex);
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
                throw new ServiceManagerException($"Invalid handle to service {serviceName}", ex);
            }

            if (!StartService(service))
            {
                Exception ex =
                    Win32Error.GetExceptionForLastError()
                    ?? throw new ServiceManagerException("Service was not started: last error must be a failure");
                this.LogStartError(serviceName, ex);
                throw new ServiceManagerException($"Failed to start service {serviceName}", ex);
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
                throw new ServiceManagerException($"Invalid handle to service {serviceName}", ex);
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
                throw new ServiceManagerException($"Failed to stop service {serviceName}", ex);
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
                throw new ServiceManagerException($"Invalid handle to service {serviceName}", ex);
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
                throw new ServiceManagerException($"Failed to change config for service {serviceName}", ex);
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
                throw new ServiceManagerException($"Invalid handle to service {serviceName}", ex);
            }

            if (!QueryServiceObjectSecurity(service, SECURITY_INFORMATION.DACL_SECURITY_INFORMATION, out var secRes))
            {
                Exception ex =
                    Win32Error.GetExceptionForLastError()
                    ?? throw new ServiceManagerException(
                        "Could not query service object security: last error must be a failure"
                    );
                this.LogGetPermissionsError(serviceName, ex);
                throw new ServiceManagerException($"Failed to query security object for service {serviceName}", ex);
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
                throw new ServiceManagerException($"Failed to convert security description to string", ex);
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
                this.LogSetPermissionsConversionError(sddl, ex);
                throw new ServiceManagerException("Failed to get DACL from security descriptor", ex);
            }

            if (!isDaclPresent)
            {
                this.LogSetPermissionsDaclNotPresent(sddl);
                throw new ArgumentException("sddl was null");
            }

            Win32Error result = SetNamedSecurityInfo(
                serviceName,
                SE_OBJECT_TYPE.SE_SERVICE,
                SECURITY_INFORMATION.DACL_SECURITY_INFORMATION,
                ppDacl: dacl
            );
            this.LogSetPermissionsApiResult(serviceName, result);

            if (!result.Succeeded)
            {
                this.LogSetPermissionsApiResultFail(serviceName);

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
                this.LogSetPermissionsProcessStartInfo(processStartInfo);

                using Process p = new() { StartInfo = processStartInfo };

                this.LogSetPermissionsStartProcess();
                try
                {
                    p.Start();
                }
                catch (Win32Exception ex)
                {
                    this.LogSetPermissionsStartProcessError(serviceName, ex);
                    throw new ServiceManagerException("Failed to start process for sc.exe", ex);
                }
                this.LogSetPermissionsProcessStarted();

                this.LogSetPermissionsProcessWait();
                p.WaitForExit();
                this.LogSetPermissionsProcessExit(p.ExitCode);

                if (p.ExitCode != 0)
                {
                    this.LogSetPermissionsProcessError(serviceName, p.ExitCode);
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

        public ServiceState CheckStatus(string serviceName)
        {
            this.LogCheckingStatus(serviceName);

            using var service = OpenService(this.scm, serviceName, ServiceAccessTypes.SERVICE_QUERY_STATUS);
            if (service.IsInvalid)
            {
                Exception ex =
                    Win32Error.GetExceptionForLastError()
                    ?? throw new ServiceManagerException("Service handle is invalid: last error must be a failure");
                this.LogCheckStatusInvalidHandle(serviceName, ex);
                throw new ServiceManagerException($"Invalid handle to service {serviceName}", ex);
            }

            SERVICE_STATUS_PROCESS status;
            try
            {
                status = QueryServiceStatusEx<SERVICE_STATUS_PROCESS>(service);
            }
            catch (Win32Exception ex)
            {
                this.LogCheckStatusError(serviceName, ex);
                throw new ServiceManagerException($"Could not query status of service {serviceName}", ex);
            }

            this.LogCheckStatus(serviceName, status.dwCurrentState);
            return status.dwCurrentState;
        }

        public ServiceStartType CheckStartupMode(string serviceName)
        {
            this.LogCheckingStartupMode(serviceName);

            using var service = OpenService(this.scm, serviceName, ServiceAccessTypes.SERVICE_QUERY_CONFIG);
            if (service.IsInvalid)
            {
                Exception ex =
                    Win32Error.GetExceptionForLastError()
                    ?? throw new ServiceManagerException("Service handle is invalid: last error must be a failure");
                this.LogCheckStartupModeInvalidHandle(serviceName, ex);
                throw new ServiceManagerException($"Invalid handle to service {serviceName}", ex);
            }

            if (
                QueryServiceConfig(service, nint.Zero, 0, out uint bytesNeeded)
                || Win32Error.GetLastError() != Win32Error.ERROR_INSUFFICIENT_BUFFER
            )
            {
                this.LogCheckStartupModeGetSizeError(serviceName);
                throw new ServiceManagerException("Could not get needed bytes to query service config");
            }

            this.LogCheckStartupModeSize(serviceName, bytesNeeded);

            using SafeCoTaskMemHandle buffer = new(bytesNeeded);
            if (!QueryServiceConfig(service, buffer, bytesNeeded, out _))
            {
                Exception ex =
                    Win32Error.GetExceptionForLastError()
                    ?? throw new ServiceManagerException("QueryServiceConfig failed. Last error must be a failure");
                this.LogCheckStartupModeError(serviceName, ex);
                throw new ServiceManagerException("Could not query service config", ex);
            }

            QUERY_SERVICE_CONFIG config = buffer.ToStructure<QUERY_SERVICE_CONFIG>();

            ServiceStartType startupMode = config.dwStartType;
            this.LogStartupMode(serviceName, startupMode);

            return startupMode;
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

        [LoggerMessage(1000, LogLevel.Debug, $"{nameof(ServiceManager)} already disposed")]
        private partial void LogDisposed();

        [LoggerMessage(1001, LogLevel.Debug, $"Disposing {nameof(ServiceManager)} instance")]
        private partial void LogDisposing();

        [LoggerMessage(1002, LogLevel.Debug, $"Successfully disposed {nameof(ServiceManager)} instance")]
        private partial void LogEndDispose();

        #endregion

        #region Constructor Logging

        [LoggerMessage(1010, LogLevel.Debug, "Opening handle to SCM")]
        private partial void LogOpenSCM();

        [LoggerMessage(1011, LogLevel.Error, "Error while opening handle to SCM")]
        private partial void LogErrorOpenSCM(Exception ex);

        [LoggerMessage(1012, LogLevel.Debug, "Handle to SCM successfully opened")]
        private partial void LogOpenedSCM();

        #endregion

        #region Start Logging

        [LoggerMessage(1020, LogLevel.Debug, "Starting service {serviceName}")]
        private partial void LogStart(string serviceName);

        [LoggerMessage(
            1021,
            LogLevel.Error,
            $"Error while opening handle to service {{serviceName}} with desired access {nameof(ServiceAccessTypes.SERVICE_START)}"
        )]
        private partial void LogStartInvalidHandle(string serviceName, Exception ex);

        [LoggerMessage(1022, LogLevel.Error, "Error while starting service {serviceName}")]
        private partial void LogStartError(string serviceName, Exception ex);

        [LoggerMessage(1023, LogLevel.Debug, "Service {serviceName} successfully started")]
        private partial void LogStarted(string serviceName);

        #endregion

        #region Stop Logging

        [LoggerMessage(1030, LogLevel.Debug, "Stopping service {serviceName}")]
        private partial void LogStop(string serviceName);

        [LoggerMessage(
            1031,
            LogLevel.Error,
            $"Error while opening handle to service {{serviceName}} with desired access {nameof(ServiceAccessTypes.SERVICE_STOP)}"
        )]
        private partial void LogStopInvalidHandle(string serviceName, Exception ex);

        [LoggerMessage(1032, LogLevel.Error, "Error while stopping service {serviceName}")]
        private partial void LogStopError(string serviceName, Exception ex);

        [LoggerMessage(1033, LogLevel.Debug, "Service {serviceName} successfully stopped")]
        private partial void LogStopped(string serviceName);

        #endregion

        #region SetStart Logging

        [LoggerMessage(1040, LogLevel.Debug, "Setting start mode of service {serviceName} to {startType}")]
        private partial void LogSetStart(string serviceName, ServiceStartType startType);

        [LoggerMessage(
            1041,
            LogLevel.Error,
            $"Error while opening handle to service {{serviceName}} with desired access {nameof(ServiceAccessTypes.SERVICE_CHANGE_CONFIG)}"
        )]
        private partial void LogSetStartInvalidHandle(string serviceName, Exception ex);

        [LoggerMessage(1042, LogLevel.Error, "Error while setting start mode of service {serviceName} to {startType}")]
        private partial void LogSetStartError(string serviceName, ServiceStartType startType, Exception ex);

        [LoggerMessage(1043, LogLevel.Debug, "Start mode of service {serviceName} successfully set to {startType}")]
        private partial void LogSetStartCompleted(string serviceName, ServiceStartType startType);

        #endregion

        #region CheckPermissions Logging

        [LoggerMessage(
            1050,
            LogLevel.Debug,
            "Checking whether current account can open handle to service {serviceName} with permissions "
                + $"{nameof(ServiceAccessTypes.SERVICE_START)}, {nameof(ServiceAccessTypes.SERVICE_STOP)} "
                + $"and {nameof(ServiceAccessTypes.SERVICE_CHANGE_CONFIG)}"
        )]
        private partial void LogCheckPermissions(string serviceName);

        [LoggerMessage(
            1051,
            LogLevel.Debug,
            "Current account can open handle to service {serviceName} with permissions"
                + $"{nameof(ServiceAccessTypes.SERVICE_START)}, {nameof(ServiceAccessTypes.SERVICE_STOP)} "
                + $"and {nameof(ServiceAccessTypes.SERVICE_CHANGE_CONFIG)}"
        )]
        private partial void LogCheckedPermissionsTrue(string serviceName);

        [LoggerMessage(
            1052,
            LogLevel.Debug,
            "Current account can't open handle to service {serviceName} with permissions"
                + $"{nameof(ServiceAccessTypes.SERVICE_START)}, {nameof(ServiceAccessTypes.SERVICE_STOP)} "
                + $"and {nameof(ServiceAccessTypes.SERVICE_CHANGE_CONFIG)}"
        )]
        private partial void LogCheckedPermissionsFalse(string serviceName);

        #endregion

        #region GetPermissions Logging

        [LoggerMessage(1060, LogLevel.Debug, "Getting security descriptor from service {serviceName}")]
        private partial void LogGetPermissions(string serviceName);

        [LoggerMessage(
            1061,
            LogLevel.Error,
            $"Error while opening handle to service {{serviceName}} with desired access {nameof(ServiceAccessRights.READ_CONTROL)}"
        )]
        private partial void LogGetPermissionsInvalidHandle(string serviceName, Exception ex);

        [LoggerMessage(1062, LogLevel.Error, "Error while getting security descriptor from service {serviceName}")]
        private partial void LogGetPermissionsError(string serviceName, Exception ex);

        [LoggerMessage(
            1063,
            LogLevel.Error,
            "Error while converting security descriptor from service {serviceName} to string"
        )]
        private partial void LogGetPermissionsConvertError(string serviceName, Exception ex);

        [LoggerMessage(
            1064,
            LogLevel.Error,
            "Error while converting string security description handle from service {serviceName} into string"
        )]
        private partial void LogGetPermissionsToStringError(string serviceName);

        [LoggerMessage(
            1065,
            LogLevel.Debug,
            "Successfully retrieved security descriptor from service {serviceName}: {sddl}"
        )]
        private partial void LogGetPermissionsSuccess(string serviceName, string sddl);

        #endregion

        #region SetPermissions Logging

        [LoggerMessage(
            1070,
            LogLevel.Debug,
            $"Setting permissions {nameof(ServiceAccessRights.SERVICE_START)}, {nameof(ServiceAccessRights.SERVICE_STOP)} "
                + $"and {nameof(ServiceAccessRights.SERVICE_CHANGE_CONFIG)} for account {{sid}} on service {{serviceName}}"
        )]
        private partial void LogSetPermissions(string serviceName, SecurityIdentifier sid);

        [LoggerMessage(1071, LogLevel.Debug, "Current DACL for service {serviceName}: {sddl}")]
        private partial void LogSetPermissionsCurrentSDDL(string serviceName, string sddl);

        [LoggerMessage(1072, LogLevel.Debug, "New DACL for service {serviceName}: {sddl}")]
        private partial void LogSetPermissionsNewSDDL(string serviceName, string sddl);

        [LoggerMessage(
            1073,
            LogLevel.Debug,
            "Adding the required permissions for account {sid} on service {serviceName} would not modify DACL. Skipping"
        )]
        private partial void LogSetPermissionsSkip(string serviceName, SecurityIdentifier sid);

        [LoggerMessage(1074, LogLevel.Debug, "Setting new DACL for service {serviceName}")]
        private partial void LogSetPermissionsPerform(string serviceName);

        [LoggerMessage(1075, LogLevel.Error, "Could not convert SDDL {sddl} into security descriptor")]
        private partial void LogSetPermissionsConversionError(string sddl, Exception ex);

        [LoggerMessage(1076, LogLevel.Error, "Converted security descriptor from {sddl} did not include a DACL")]
        private partial void LogSetPermissionsDaclNotPresent(string sddl);

        [LoggerMessage(
            1077,
            LogLevel.Debug,
            $"API result from {nameof(SetNamedSecurityInfo)} on service {{serviceName}}: {{result}}"
        )]
        private partial void LogSetPermissionsApiResult(string serviceName, Win32Error result);

        [LoggerMessage(
            1078,
            LogLevel.Debug,
            "Failed setting permissions for service {serviceName}. Fallback to sc.exe"
        )]
        private partial void LogSetPermissionsApiResultFail(string serviceName);

        [LoggerMessage(1079, LogLevel.Debug, "Using process parameters: {startInfo}")]
        private partial void LogSetPermissionsProcessStartInfo(ProcessStartInfo startInfo);

        [LoggerMessage(1080, LogLevel.Debug, "Starting process")]
        private partial void LogSetPermissionsStartProcess();

        [LoggerMessage(1081, LogLevel.Error, "Error while starting sc.exe for service {serviceName}")]
        private partial void LogSetPermissionsStartProcessError(string serviceName, Exception ex);

        [LoggerMessage(1082, LogLevel.Debug, "Process started successfully")]
        private partial void LogSetPermissionsProcessStarted();

        [LoggerMessage(1083, LogLevel.Debug, "Waiting for process exit")]
        private partial void LogSetPermissionsProcessWait();

        [LoggerMessage(1084, LogLevel.Debug, "sc.exe exited with exit code {exitCode}")]
        private partial void LogSetPermissionsProcessExit(int exitCode);

        [LoggerMessage(1085, LogLevel.Error, "sc.exe for service {serviceName} exited with code {exitCode}")]
        private partial void LogSetPermissionsProcessError(string serviceName, int exitCode);

        [LoggerMessage(
            1086,
            LogLevel.Debug,
            $"Permissions {nameof(ServiceAccessRights.SERVICE_START)}, {nameof(ServiceAccessRights.SERVICE_STOP)} "
                + $"and {nameof(ServiceAccessRights.SERVICE_CHANGE_CONFIG)} for account {{sid}} on service "
                + "{serviceName} successfully set"
        )]
        private partial void LogSetPermissionsSuccess(string serviceName, SecurityIdentifier sid);

        #endregion

        #region CheckStatus Logging

        [LoggerMessage(1090, LogLevel.Debug, "Checking status for service {serviceName}")]
        private partial void LogCheckingStatus(string serviceName);

        [LoggerMessage(
            1091,
            LogLevel.Error,
            $"Error while opening handle to service {{serviceName}} with desired access {nameof(ServiceAccessTypes.SERVICE_QUERY_STATUS)}"
        )]
        private partial void LogCheckStatusInvalidHandle(string serviceName, Exception ex);

        [LoggerMessage(1092, LogLevel.Error, "Error while checking status of service {serviceName}")]
        private partial void LogCheckStatusError(string serviceName, Win32Exception ex);

        [LoggerMessage(1093, LogLevel.Debug, "Current state of service {serviceName}: {currentState}")]
        private partial void LogCheckStatus(string serviceName, ServiceState currentState);

        #endregion

        #region CheckStartupMode Logging

        [LoggerMessage(1100, LogLevel.Debug, "Checking startup mode for servce {serviceName}")]
        private partial void LogCheckingStartupMode(string serviceName);

        [LoggerMessage(
            1101,
            LogLevel.Error,
            $"Error while opening handle to service {{serviceName}} with desired access {nameof(ServiceAccessTypes.SERVICE_QUERY_CONFIG)}"
        )]
        private partial void LogCheckStartupModeInvalidHandle(string serviceName, Exception ex);

        [LoggerMessage(
            1102,
            LogLevel.Error,
            "Error while getting size of configuration object for service {serviceName}"
        )]
        private partial void LogCheckStartupModeGetSizeError(string serviceName);

        [LoggerMessage(1103, LogLevel.Debug, "Size of configuration for service {serviceName}: {size} bytes")]
        private partial void LogCheckStartupModeSize(string serviceName, uint size);

        [LoggerMessage(1104, LogLevel.Error, "Error while getting configuration of service {serviceName}")]
        private partial void LogCheckStartupModeError(string serviceName, Exception ex);

        [LoggerMessage(1105, LogLevel.Debug, "Startup mode for service {serviceName}: {startupMode}")]
        private partial void LogStartupMode(string serviceName, ServiceStartType startupMode);

        #endregion
    }

    public sealed class ServiceManagerException : Exception
    {
        public ServiceManagerException(string message)
            : base(message) { }

        public ServiceManagerException(string message, Exception ex)
            : base($"Error in {nameof(ServiceManager)}: {message}", ex) { }
    }
}
