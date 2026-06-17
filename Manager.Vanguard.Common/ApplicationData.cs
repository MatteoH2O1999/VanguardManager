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
using System.Reflection;

namespace Manager.Vanguard.Common
{
    /// <summary>
    /// Static class that contains various data about this product.
    /// </summary>
    public static class ApplicationData
    {
        private const string SERVICE_NAME_METADATA_KEY = "ServiceName";
        private const string KERNEL_LEVEL_SERVICE_NAME_METADATA_KEY = "KernelDriverServiceName";
        private const string USER_LEVEL_SERVICE_NAME_METADATA_KEY = "UserLevelService";
        private const string GAME_PROCESSES_NAME_METADATA_KEY = "GameProcesses";

        private const string DEBUG_FILE_NAME = "debug";
        private const string AUTO_RESTART_FILE_NAME = "restart";

        /// <summary>
        /// The path to the shared application data folder.
        /// </summary>
        public static string AppData { get; }

        /// <summary>
        /// The application's shared name.
        /// </summary>
        public static string AppName { get; }

        /// <summary>
        /// The name of the registered service as <c>NT SERVICE\ServiceName</c>.
        /// </summary>
        public static string ServiceName { get; }

        /// <summary>
        /// The name of Vanguard's kernel level service.
        /// </summary>
        public static string KernelLevelServiceName { get; }

        /// <summary>
        /// The name of Vanguard's user level service.
        /// </summary>
        public static string UserLevelServiceName { get; }

        /// <summary>
        /// An array of <see cref="Process.ProcessName"/> related to Riot's games.
        /// </summary>
        public static string[] GameProcesses { get; }

        /// <summary>
        /// Returns <see langword="true"/> if the app is in DEBUG mode.
        /// </summary>
        public static bool Debug => File.Exists(Path.Combine(AppData, DEBUG_FILE_NAME));

        /// <summary>
        /// Returns <see langword="true"/> if the launcher should automatically reboot the system
        /// after creating a request.
        /// </summary>
        public static bool AutoRestart => File.Exists(Path.Combine(AppData, AUTO_RESTART_FILE_NAME));

        static ApplicationData()
        {
            ArgumentNullException.ThrowIfNull(Application.CompanyName);
            ArgumentNullException.ThrowIfNull(Application.ProductName);

            AppName = Application.ProductName;
            AppData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                Application.CompanyName,
                Application.ProductName
            );

            ServiceName = GetMetadata(SERVICE_NAME_METADATA_KEY);
            KernelLevelServiceName = GetMetadata(KERNEL_LEVEL_SERVICE_NAME_METADATA_KEY);
            UserLevelServiceName = GetMetadata(USER_LEVEL_SERVICE_NAME_METADATA_KEY);
            GameProcesses = GetMetadata(GAME_PROCESSES_NAME_METADATA_KEY).Split(';');
        }

        private static string GetMetadata(string key)
        {
            AssemblyMetadataAttribute[] serviceAttributes =
            [
                .. Assembly
                    .GetExecutingAssembly()
                    .GetCustomAttributes<AssemblyMetadataAttribute>()
                    .Where(m => m.Key == key),
            ];

            return serviceAttributes.Length == 0
                    ? throw new InvalidAssemblyMetadataException($"Metadata key {key} not found")
                : serviceAttributes.Length > 1
                    ? throw new InvalidAssemblyMetadataException($"Duplicate metadata found for key {key}")
                : serviceAttributes[0].Value
                    ?? throw new InvalidAssemblyMetadataException($"Metadata value for {key} is null");
        }

        /// <summary>
        /// Sets the app's DEBUG mode.
        /// </summary>
        /// <param name="debug">The app's target DEBUG mode.</param>
        public static void SwitchDebug(bool debug)
        {
            string debugFileName = Path.Combine(AppData, DEBUG_FILE_NAME);
            if (debug)
            {
                File.WriteAllText(debugFileName, string.Empty);
            }
            else if (File.Exists(debugFileName))
            {
                File.Delete(debugFileName);
            }
        }

        /// <summary>
        /// Set's the app auto restart switch.
        /// </summary>
        /// <param name="autoRestart">The app's target auto restart mode.</param>
        public static void SwitchAutoRestart(bool autoRestart)
        {
            string autoRestartFileName = Path.Combine(AppData, AUTO_RESTART_FILE_NAME);
            if (autoRestart)
            {
                File.WriteAllText(autoRestartFileName, string.Empty);
            }
            else if (File.Exists(autoRestartFileName))
            {
                File.Delete(autoRestartFileName);
            }
        }
    }

    public sealed class InvalidAssemblyMetadataException(string error) : Exception(error);
}
