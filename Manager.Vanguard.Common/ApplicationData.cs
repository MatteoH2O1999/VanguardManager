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

using System.Reflection;

namespace Manager.Vanguard.Common
{
    /// <summary>
    /// Static class that contains various data about this product.
    /// </summary>
    public static class ApplicationData
    {
        private const string SERVICE_NAME_METADATA_KEY = "ServiceName";

        /// <summary>
        /// The path to the application's folder in <c>%appdata%/Local</c>.
        /// </summary>
        public static string Local { get; }

        /// <summary>
        /// The application's shared name.
        /// </summary>
        public static string AppName { get; }

        /// <summary>
        /// The name of the registered service as <c>NT SERVICE/ServiceName</c>.
        /// </summary>
        public static string ServiceName { get; }

        static ApplicationData()
        {
            ArgumentNullException.ThrowIfNull(Application.CompanyName);
            ArgumentNullException.ThrowIfNull(Application.ProductName);

            AppName = Application.ProductName;
            Local = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                Application.CompanyName,
                Application.ProductName
            );

            IEnumerable<AssemblyMetadataAttribute> metadata = Assembly
                .GetExecutingAssembly()
                .GetCustomAttributes<AssemblyMetadataAttribute>();

            AssemblyMetadataAttribute[] serviceAttributes =
            [
                .. metadata.Where(m => m.Key == SERVICE_NAME_METADATA_KEY),
            ];
            if (serviceAttributes.Length == 0)
            {
                throw new InvalidAssemblyMetadataException($"Metadata key {SERVICE_NAME_METADATA_KEY} not found");
            }
            if (serviceAttributes.Length > 1)
            {
                throw new InvalidAssemblyMetadataException(
                    $"Duplicate metadata found for key {SERVICE_NAME_METADATA_KEY}"
                );
            }
            string serviceName =
                serviceAttributes[0].Value
                ?? throw new InvalidAssemblyMetadataException(
                    $"Metadata value for {SERVICE_NAME_METADATA_KEY} is null"
                );

            ServiceName = serviceName;
        }
    }

    public sealed class InvalidAssemblyMetadataException(string error) : Exception(error);
}
