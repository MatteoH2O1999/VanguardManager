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
    public sealed record Request(string Executable, string[] Args)
    {
        public override string ToString() =>
            Executable + (Args.Length > 0 ? " with args " + string.Join(' ', Args) : string.Empty);
    };

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

        public Request? CheckRequest()
        {
            this.LogCheckingRequest();

            Request? request = null;
            if (this.RequestExists())
            {
                this.LogReadingRequestFile();
                string[] lines;
                try
                {
                    lines = File.ReadAllLines(requestFileAbsolutePath);
                }
                catch (Exception ex)
                {
                    this.LogFailedReadingRequestFile(ex);
                    throw new RequestManagerException("Could not read from request file", ex);
                }

                request = new(lines[0], lines[1..]);

                this.LogCheckingRequestedExecutable(request);
                if (!File.Exists(request.Executable))
                {
                    this.LogInvalidRequestedExecutable(request.Executable);
                    throw new RequestManagerException("Requested executable could not be found");
                }
                this.LogValidRequestFound(request);
            }
            else
            {
                this.LogNoRequestFound();
            }

            return request;
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

        [LoggerMessage(2000, LogLevel.Debug, "Creating request for executable {executable}")]
        private partial void LogCreatingRequest(string executable);

        [LoggerMessage(2001, LogLevel.Debug, "Creating or overwriting request for executable {executable}")]
        private partial void LogCreatingRequestWithOverwrite(string executable);

        [LoggerMessage(2002, LogLevel.Error, "Request file already exists")]
        private partial void LogRequestAlreadyExists();

        [LoggerMessage(2003, LogLevel.Debug, "Writing request to file")]
        private partial void LogWritingToFile();

        [LoggerMessage(2004, LogLevel.Error, "Error while writing request to file")]
        private partial void LogFailedWritingToFile(Exception ex);

        [LoggerMessage(2005, LogLevel.Debug, "Request for executable {executable} created successfully")]
        private partial void LogCreatedRequest(string executable);

        #endregion

        #region DeleteRequest Logging

        [LoggerMessage(2010, LogLevel.Debug, "Deleting request file")]
        private partial void LogDeletingRequest();

        [LoggerMessage(2011, LogLevel.Error, "Request file not found")]
        private partial void LogRequestDoesNotExist();

        [LoggerMessage(2012, LogLevel.Debug, "Deleting request file")]
        private partial void LogDeletingFile();

        [LoggerMessage(2013, LogLevel.Error, "Could not delete request file")]
        private partial void LogFailedDeletingFile(Exception ex);

        [LoggerMessage(2014, LogLevel.Debug, "Request successfully deleted")]
        private partial void LogDeletedRequest();

        #endregion

        #region CheckRequest Logging

        [LoggerMessage(2020, LogLevel.Debug, "Checking request status")]
        private partial void LogCheckingRequest();

        [LoggerMessage(2021, LogLevel.Debug, "Reading request file")]
        private partial void LogReadingRequestFile();

        [LoggerMessage(2022, LogLevel.Error, "Could not read request file")]
        private partial void LogFailedReadingRequestFile(Exception ex);

        [LoggerMessage(2023, LogLevel.Debug, "Checking whether requested executable {executable} is valid")]
        private partial void LogCheckingRequestedExecutable(Request executable);

        [LoggerMessage(2024, LogLevel.Error, "Executable {executable} is invalid")]
        private partial void LogInvalidRequestedExecutable(string executable);

        [LoggerMessage(2025, LogLevel.Debug, "Executable {executable} requested")]
        private partial void LogValidRequestFound(Request executable);

        [LoggerMessage(2026, LogLevel.Debug, "No request found")]
        private partial void LogNoRequestFound();

        #endregion

        #region RequestExists Logging

        [LoggerMessage(2030, LogLevel.Debug, "Checking whether request file exists")]
        private partial void LogCheckingRequestExists();

        [LoggerMessage(2031, LogLevel.Debug, "Request file exists")]
        private partial void LogRequestExists();

        [LoggerMessage(2032, LogLevel.Debug, "Request file does not exist")]
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
