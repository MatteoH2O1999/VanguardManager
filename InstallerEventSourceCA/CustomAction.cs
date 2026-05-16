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

                switch (action)
                {
                    case "Install":
                        session.Log("Performing 'Install' action");
                        if (EventLog.SourceExists(appName))
                        {
                            session.Log(
                                $"Failed InstallerEventSourceCA: source '{appName}' already exists. "
                                    + $"{appName} is probably already installed or the previous installation was corrupted."
                            );
                            return ActionResult.Failure;
                        }
                        session.Log($"Creating event source '{appName}'");
                        EventLog.CreateEventSource(appName, "Application");
                        break;
                    case "Uninstall":
                        session.Log("Performing 'Uninstall' action");
                        if (!EventLog.SourceExists(appName))
                        {
                            session.Log(
                                $"Failed InstallerEventSourceCA: source '{appName}' does not exists. "
                                    + $"{appName} is probably already uninstalled or the installation was corrupted."
                            );
                            return ActionResult.Failure;
                        }
                        session.Log($"Deleting event source '{appName}'");
                        EventLog.DeleteEventSource(appName);
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
    }
}
