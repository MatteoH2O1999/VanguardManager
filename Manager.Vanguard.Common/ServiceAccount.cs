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

using System.Security.Principal;

namespace Manager.Vanguard.Common
{
    public sealed class ServiceAccount
    {
        public SecurityIdentifier SID { get; }

        public ServiceAccount()
        {
            NTAccount serviceAccount = new("NT SERVICE", "VanguardManager");
            try
            {
                SID = (SecurityIdentifier)serviceAccount.Translate(typeof(SecurityIdentifier));
            }
            catch (IdentityNotMappedException ex)
            {
                throw new ServiceAccountNotFoundException(ex);
            }
        }
    }

    public sealed class ServiceAccountNotFoundException(IdentityNotMappedException ex)
        : Exception("Could not find service account 'NT SERVICE/VanguardManager'", ex);
}
