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

using Manager.Vanguard.Common;
using Manager.Vanguard.Updater;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddFileLogging("updater");
builder.Logging.AddEventLog(options =>
{
    options.Filter = (_, level) => level >= LogLevel.Critical;
    options.SourceName = ApplicationData.AppName;
});
#if DEBUG
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Trace);
#endif

builder.Services.AddTransient<Runner>();

using IHost app = builder.Build();

ILogger logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("UpdaterHost");

Logs.LogOutOfHostMessage(logger, LogLevel.Debug, "Acquiring updater lock");
IDisposable? updaterLock;
try
{
    updaterLock = Locks.UPDATER.TryAcquire();
}
catch (LockException ex)
{
    Logs.LogOutOfHostMessage(logger, LogLevel.Error, "Could not acquire updater lock", ex);
    Environment.ExitCode = -1;
    return;
}

if (updaterLock is null)
{
    Logs.LogOutOfHostMessage(logger, LogLevel.Error, "Updater lock already in use by another process");
    Environment.ExitCode = -1;
    return;
}
else
{
    Logs.LogOutOfHostMessage(logger, LogLevel.Information, "Updater lock acquired");
}

using (updaterLock)
{
    try
    {
        Runner runner = app.Services.GetRequiredService<Runner>();
        runner.Run();
    }
    catch (Exception ex)
    {
        Logs.LogOutOfHostCrash(logger, ex);
    }
}
