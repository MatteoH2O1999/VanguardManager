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
    public sealed partial class RequestManager(ILogger<RequestManager> Logger)
    {
        private const string REQUEST_FILE_NAME = ".playsession_req";
        private static readonly string requestFileAbsolutePath = Path.Combine(
            ApplicationData.AppData,
            REQUEST_FILE_NAME
        );

        private readonly ILogger logger = Logger;

        public void CreateRequest(string executable)
        {
            this.CreateRequest(executable, true);
        }

        public void CreateRequest(string executable, bool overwrite)
        {
            if (overwrite)
            {
                this.LogCreatingRequestWithOverwrite(executable);
            }
            else
            {
                this.LogCreatingRequest(executable);
            }

            if (!overwrite && this.RequestExists())
            {
                this.LogRequestAlreadyExists();
                throw new RequestManagerException("A playsession request already exists");
            }

            this.LogWritingToFile();
            try
            {
                File.WriteAllText(requestFileAbsolutePath, executable);
            }
            catch (Exception ex)
            {
                this.LogFailedWritingToFile(ex);
                throw new RequestManagerException("Failed to create and write to request file", ex);
            }
            this.LogCreatedRequest(executable);
        }

        public void DeleteRequest()
        {
            this.LogDeletingRequest();

            if (!this.RequestExists())
            {
                this.LogRequestDoesNotExist();
                throw new RequestManagerException("There is no request to delete");
            }

            this.LogDeletingFile();
            try
            {
                File.Delete(requestFileAbsolutePath);
            }
            catch (Exception ex)
            {
                this.LogFailedDeletingFile(ex);
                throw new RequestManagerException("Failed to delete request file", ex);
            }
            this.LogDeletedRequest();
        }

        public string? CheckRequest()
        {
            this.LogCheckingRequest();

            string? requestedExecutable = null;
            if (this.RequestExists())
            {
                this.LogReadingRequestFile();
                try
                {
                    requestedExecutable = File.ReadAllText(requestFileAbsolutePath);
                }
                catch (Exception ex)
                {
                    this.LogFailedReadingRequestFile(ex);
                    throw new RequestManagerException("Could not read from request file", ex);
                }

                this.LogCheckingRequestedExecutable(requestedExecutable);
                if (!File.Exists(requestedExecutable))
                {
                    this.LogInvalidRequestedExecutable(requestedExecutable);
                    throw new RequestManagerException("Requested executable could not be found");
                }
                this.LogValidRequestFound(requestedExecutable);
            }
            else
            {
                this.LogNoRequestFound();
            }

            return requestedExecutable;
        }

        public bool RequestExists()
        {
            this.LogCheckingRequestExists();
            if (File.Exists(requestFileAbsolutePath))
            {
                this.LogRequestExists();
                return true;
            }
            this.LogRequestNotExists();
            return false;
        }

        #region CreateRequest Logging

        [LoggerMessage(1100, LogLevel.Debug, "Creating request for executable {executable}")]
        private partial void LogCreatingRequest(string executable);

        [LoggerMessage(1101, LogLevel.Debug, "Creating or overwriting request for executable {executable}")]
        private partial void LogCreatingRequestWithOverwrite(string executable);

        [LoggerMessage(1102, LogLevel.Error, "Request file already exists")]
        private partial void LogRequestAlreadyExists();

        [LoggerMessage(1103, LogLevel.Debug, "Writing request to file")]
        private partial void LogWritingToFile();

        [LoggerMessage(1104, LogLevel.Error, "Error while writing request to file")]
        private partial void LogFailedWritingToFile(Exception ex);

        [LoggerMessage(1105, LogLevel.Debug, "Request for executable {executable} created successfully")]
        private partial void LogCreatedRequest(string executable);

        #endregion

        #region DeleteRequest Logging

        [LoggerMessage(1110, LogLevel.Debug, "Deleting request file")]
        private partial void LogDeletingRequest();

        [LoggerMessage(1111, LogLevel.Error, "Request file not found")]
        private partial void LogRequestDoesNotExist();

        [LoggerMessage(1112, LogLevel.Debug, "Deleting request file")]
        private partial void LogDeletingFile();

        [LoggerMessage(1113, LogLevel.Error, "Could not delete request file")]
        private partial void LogFailedDeletingFile(Exception ex);

        [LoggerMessage(1114, LogLevel.Debug, "Request successfully deleted")]
        private partial void LogDeletedRequest();

        #endregion

        #region CheckRequest Logging

        [LoggerMessage(1120, LogLevel.Debug, "Checking request status")]
        private partial void LogCheckingRequest();

        [LoggerMessage(1121, LogLevel.Debug, "Reading request file")]
        private partial void LogReadingRequestFile();

        [LoggerMessage(1122, LogLevel.Error, "Could not read request file")]
        private partial void LogFailedReadingRequestFile(Exception ex);

        [LoggerMessage(1123, LogLevel.Debug, "Checking whether requested executable {executable} is valid")]
        private partial void LogCheckingRequestedExecutable(string executable);

        [LoggerMessage(1124, LogLevel.Error, "Executable {executable} is invalid")]
        private partial void LogInvalidRequestedExecutable(string executable);

        [LoggerMessage(1125, LogLevel.Debug, "Executable {executable} requested")]
        private partial void LogValidRequestFound(string executable);

        [LoggerMessage(1126, LogLevel.Debug, "No request found")]
        private partial void LogNoRequestFound();

        #endregion

        #region RequestExists Logging

        [LoggerMessage(1130, LogLevel.Debug, "Checking whether request file exists")]
        private partial void LogCheckingRequestExists();

        [LoggerMessage(1131, LogLevel.Debug, "Request file exists")]
        private partial void LogRequestExists();

        [LoggerMessage(1132, LogLevel.Debug, "Request file does not exist")]
        private partial void LogRequestNotExists();

        #endregion
    }

    public sealed class RequestManagerException : Exception
    {
        public RequestManagerException(string message)
            : base(message) { }

        public RequestManagerException(string message, Exception ex)
            : base($"Error in {nameof(RequestManager)}: {message}", ex) { }
    }
}
