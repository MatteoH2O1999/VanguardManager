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

using Microsoft.Extensions.Logging;

namespace Manager.Vanguard.Common
{
    public sealed class RequestManager(ILogger<RequestManager> Logger)
    {
        private const string REQUEST_FILE_NAME = ".playsession_req";
        private static readonly string requestFileAbsolutePath = Path.Combine(
            ApplicationData.AppData,
            REQUEST_FILE_NAME
        );
        private static FileInfo RequestFile => new(requestFileAbsolutePath);

        private readonly ILogger logger = Logger;

        public void CreateRequest(string executable)
        {
            this.CreateRequest(executable, true);
        }

        public void CreateRequest(string executable, bool overwrite)
        {
            throw new NotImplementedException();
        }

        public void DeleteRequest()
        {
            throw new NotImplementedException();
        }

        public string? CheckRequest()
        {
            throw new NotImplementedException();
        }

        public bool RequestExists()
        {
            return this.CheckRequest() != null;
        }
    }

    public sealed class RequestManagerException : Exception
    {
        public RequestManagerException(string message)
            : base(message) { }

        public RequestManagerException(string message, Exception ex)
            : base($"Error in {nameof(RequestManager)}: {message}", ex) { }
    }
}
