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

using System.Diagnostics;
using System.Security.AccessControl;
using System.Security.Principal;
using System.ServiceProcess;
using WixToolset.Dtf.WindowsInstaller;
using static Vanara.PInvoke.AdvApi32;

namespace InstallerEventSourceCA
{
    public class CustomActions
    {
        [CustomAction]
        public static ActionResult InstallerEventSourceCA(Session session)
        {
            try
            {
                session.Log("Begin InstallerEventSourceCA");

                string action = session.CustomActionData["Action"];
                string appName = session.CustomActionData["AppName"];
                string company = session.CustomActionData["CompanyName"];
                string dialogString = session.CustomActionData["DialogString"];
                string serviceName = session.CustomActionData["ServiceName"];
                string serviceCleanupString = session.CustomActionData["ServiceCleanupString"];
                string serviceCleanupTemplate = serviceCleanupString.Replace("...", ": [1]");
                string kernelLevelServiceName = session.CustomActionData["KernelDriverServiceName"];
                string userLevelServiceName = session.CustomActionData["UserLevelService"];

                switch (action)
                {
                    case "Install":
                    {
                        session.Log("Performing 'Install' action");

                        string[] services =
                        [
                            .. ServiceController.GetServices().Select(s => s.ServiceName),
                            .. ServiceController.GetDevices().Select(s => s.ServiceName),
                        ];

                        if (services.Contains(kernelLevelServiceName) && services.Contains(userLevelServiceName))
                        {
                            session.Log("Assigning permissions to service account");

                            NTAccount serviceAccount = new("NT SERVICE", serviceName);
                            SecurityIdentifier sid;
                            try
                            {
                                sid = (SecurityIdentifier)serviceAccount.Translate(typeof(SecurityIdentifier));
                            }
                            catch (IdentityNotMappedException)
                            {
                                session.Log("Service is not installed");
                                return ActionResult.Failure;
                            }

                            using SafeSC_HANDLE scm = OpenSCManager(
                                null,
                                null,
                                ScManagerAccessTypes.SC_MANAGER_CONNECT
                            );
                            if (scm.IsInvalid)
                            {
                                session.Log("Could not connect to service manager");
                                return ActionResult.Failure;
                            }

                            CommonSecurityDescriptor kernelLevelServiceSecurity = GetServiceSecurityDescriptor(
                                kernelLevelServiceName,
                                scm
                            );
                            DiscretionaryAcl kernelLevelServiceDACL =
                                kernelLevelServiceSecurity.DiscretionaryAcl
                                ?? throw new Exception("DACL cannot be null");
                            kernelLevelServiceDACL.Purge(sid);
                            kernelLevelServiceDACL.AddAccess(
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

                            CommonSecurityDescriptor userLevelServiceSecurity = GetServiceSecurityDescriptor(
                                userLevelServiceName,
                                scm
                            );
                            DiscretionaryAcl userLevelServiceDACL =
                                userLevelServiceSecurity.DiscretionaryAcl ?? throw new Exception("DACL cannot be null");
                            userLevelServiceDACL.Purge(sid);
                            userLevelServiceDACL.AddAccess(
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

                            SetServiceSecurityDescriptor(kernelLevelServiceName, scm, kernelLevelServiceSecurity);
                            SetServiceSecurityDescriptor(userLevelServiceName, scm, userLevelServiceSecurity);
                        }
                        else
                        {
                            session.Log("Vanguard is not installed. Skipping permissions");
                        }

                        if (EventLog.SourceExists(appName))
                        {
                            session.Log(
                                $"Failed InstallerEventSourceCA: source '{appName}' already exists. "
                                    + $"{appName} is probably already installed or the installation was corrupted."
                            );
                            return ActionResult.Failure;
                        }
                        session.Log($"Creating event source '{appName}'");
                        EventLog.CreateEventSource(appName, "Application");

                        break;
                    }
                    case "UninstallInit":
                    {
                        using (Record rec = new(3))
                        {
                            rec[1] = "Clean service permissions";
                            rec[2] = serviceCleanupString;
                            rec[3] = serviceCleanupTemplate;
                            session.Message(InstallMessage.ActionStart, rec);
                        }

                        NTAccount serviceAccount = new("NT SERVICE", serviceName);
                        SecurityIdentifier sid;
                        try
                        {
                            sid = (SecurityIdentifier)serviceAccount.Translate(typeof(SecurityIdentifier));
                        }
                        catch (IdentityNotMappedException)
                        {
                            session.Log("Service is not installed");
                            return ActionResult.Failure;
                        }

                        using (Record rec = new(4))
                        {
                            rec[1] = 0;
                            rec[2] = 1;
                            rec[3] = 0;
                            rec[4] = 0;
                            session.Message(InstallMessage.Progress, rec);
                        }

                        string[] services =
                        [
                            .. ServiceController.GetServices().Select(s => s.ServiceName),
                            .. ServiceController.GetDevices().Select(s => s.ServiceName),
                        ];

                        using (Record rec = new(2))
                        {
                            rec[1] = 2;
                            rec[2] = 1;
                            session.Message(InstallMessage.Progress, rec);
                        }

                        using (Record rec = new(4))
                        {
                            rec[1] = 0;
                            rec[2] = services.Length;
                            rec[3] = 0;
                            rec[4] = 0;
                            session.Message(InstallMessage.Progress, rec);
                        }

                        using (Record rec = new(3))
                        {
                            rec[1] = 1;
                            rec[2] = 1;
                            rec[3] = 1;
                            session.Message(InstallMessage.Progress, rec);
                        }

                        using SafeSC_HANDLE scm = OpenSCManager(null, null, ScManagerAccessTypes.SC_MANAGER_CONNECT);
                        if (scm.IsInvalid)
                        {
                            session.Log("Could not connect to service manager");
                            return ActionResult.Failure;
                        }

                        foreach (string service in services)
                        {
                            CommonSecurityDescriptor permissions = GetServiceSecurityDescriptor(service, scm);
                            string oldSddl = permissions.GetSddlForm(AccessControlSections.All);
                            permissions.DiscretionaryAcl.Purge(sid);
                            string newSddl = permissions.GetSddlForm(AccessControlSections.All);
                            if (oldSddl != newSddl)
                            {
                                session.Log($"Clean permissions for service {service}");
                                SetServiceSecurityDescriptor(service, scm, permissions);
                            }
                            else
                            {
                                session.Log($"Permissions for service {service} are already clean");
                            }

                            using Record rec = new(1);
                            rec[1] = service;
                            session.Message(InstallMessage.ActionData, rec);
                        }

                        break;
                    }
                    case "UninstallFinalize":
                    {
                        session.Log("Performing 'UninstallFinalize' action");

                        string manufacturerPath = Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                            company
                        );
                        string localFilesPath = Path.Combine(manufacturerPath, appName);
                        DirectoryInfo localFiles = new(localFilesPath);

                        if (
                            localFiles.Exists
                            && session.Message(
                                InstallMessage.User
                                    | (InstallMessage)MessageBoxButtons.YesNo
                                    | (InstallMessage)MessageIcon.None
                                    | (InstallMessage)MessageDefaultButton.Button2,
                                new() { FormatString = dialogString }
                            ) == MessageResult.Yes
                        )
                        {
                            session.Log($"Deleting local files from {localFilesPath}");
                            if (!localFiles.Exists)
                            {
                                session.Log("Directory does not exist. Already deleted");
                            }
                            else
                            {
                                localFiles.Delete(true);
                            }
                            session.Log("Deleting manufacturer folder if empty");
                            DirectoryInfo manufacturerDir = new(manufacturerPath);
                            manufacturerDir.Delete();
                        }

                        if (!EventLog.SourceExists(appName))
                        {
                            session.Log(
                                $"Failed InstallerEventSourceCA: source '{appName}' does not exist. "
                                    + $"{appName} is probably already uninstalled or the installation was corrupted."
                            );
                            return ActionResult.Failure;
                        }
                        session.Log($"Deleting event source '{appName}'");
                        EventLog.DeleteEventSource(appName);

                        break;
                    }
                    default:
                    {
                        session.Log($"Invalid action: '{action}'");
                        return ActionResult.Failure;
                    }
                }

                session.Log("End InstallerEventSourceCA");
            }
            catch (Exception ex)
            {
                session.Log($"Failed InstallerEventSourceCA: {ex}");
                return ActionResult.Failure;
            }

            return ActionResult.Success;
        }

        private static CommonSecurityDescriptor GetServiceSecurityDescriptor(string serviceName, SafeSC_HANDLE scm)
        {
            using var service = OpenService(scm, serviceName, (ServiceAccessTypes)ServiceAccessRights.READ_CONTROL);
            if (service.IsInvalid)
            {
                throw new Exception("Could not open service with READ_CONTROL");
            }

            if (
                !QueryServiceObjectSecurity(
                    service,
                    Vanara.PInvoke.SECURITY_INFORMATION.DACL_SECURITY_INFORMATION,
                    out var res
                )
            )
            {
                throw new Exception("Could not query service object security");
            }

            using var securityObject = res;

            string sddl = ConvertSecurityDescriptorToStringSecurityDescriptor(
                securityObject,
                Vanara.PInvoke.SECURITY_INFORMATION.DACL_SECURITY_INFORMATION
            );

            return new(false, false, sddl);
        }

        private static void SetServiceSecurityDescriptor(
            string serviceName,
            SafeSC_HANDLE scm,
            CommonSecurityDescriptor securityDescriptor
        )
        {
            using var service = OpenService(scm, serviceName, (ServiceAccessTypes)ServiceAccessRights.WRITE_DAC);
            if (service.IsInvalid)
            {
                throw new Exception("Could not open service with WRITE_DAC");
            }

            using var securityObject = ConvertStringSecurityDescriptorToSecurityDescriptor(
                securityDescriptor.GetSddlForm(AccessControlSections.All)
            );

            if (
                !SetServiceObjectSecurity(
                    service,
                    Vanara.PInvoke.SECURITY_INFORMATION.DACL_SECURITY_INFORMATION,
                    securityObject
                )
            )
            {
                throw new Exception("Could not set service object security");
            }
        }
    }
}
