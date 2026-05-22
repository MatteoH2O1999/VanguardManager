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

using System.Diagnostics;
using System.Security.Principal;
using System.ServiceProcess;
using WixToolset.Dtf.WindowsInstaller;

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

                switch (action)
                {
                    case "Install":
                        session.Log("Performing 'Install' action");

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
                    case "UninstallInit":
                        NTAccount serviceAccount = new("NT SERVICE", serviceName);
                        SecurityIdentifier serviceAccountId = (SecurityIdentifier)
                            serviceAccount.Translate(typeof(SecurityIdentifier));
                        string sid = serviceAccountId.Value;

                        string[] services =
                        [
                            .. ServiceController.GetServices().Select(s => s.ServiceName),
                            .. ServiceController.GetDevices().Select(s => s.ServiceName),
                        ];

                        foreach (string service in services)
                        {
                            string sddl = GetSDDL(service);
                        }

                        break;
                    case "UninstallFinalize":
                        session.Log("Performing 'UninstallFinalize' action");

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

                        if (
                            session.Message(
                                InstallMessage.User
                                    | (InstallMessage)MessageBoxButtons.YesNo
                                    | (InstallMessage)MessageIcon.None
                                    | (InstallMessage)MessageDefaultButton.Button2,
                                new() { FormatString = dialogString }
                            ) == MessageResult.Yes
                        )
                        {
                            string localFilesPath = Path.Combine(
                                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                                company,
                                appName
                            );
                            session.Log($"Deleting local files from {localFilesPath}");
                            DirectoryInfo localFiles = new(localFilesPath);
                            localFiles.Delete(true);
                        }

                        break;
                    default:
                        session.Log($"Invalid action: '{action}'");
                        return ActionResult.Failure;
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

        private static string GetSDDL(string serviceName)
        {
            ProcessStartInfo processStartInfo = new()
            {
                FileName = "sc.exe",
                Arguments = $"sdshow {serviceName}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using Process process = new() { StartInfo = processStartInfo };

            process.Start();

            string output = process.StandardOutput.ReadToEnd();
            string errors = process.StandardError.ReadToEnd();

            process.WaitForExit();

            return process.ExitCode != 0
                ? throw new Exception($"Could not acquire SDDL for service {serviceName}: {errors}")
                : output.Trim();
        }

        private static void SetSDDL(string serviceName, string sddl)
        {
            ProcessStartInfo processStartInfo = new()
            {
                FileName = "sc.exe",
                Arguments = $"sdset {serviceName} {sddl}",
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using Process process = new() { StartInfo = processStartInfo };

            process.Start();

            string errors = process.StandardError.ReadToEnd();

            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                throw new Exception($"Could not set SDDL for service {serviceName}: {errors}");
            }
        }
    }
}
