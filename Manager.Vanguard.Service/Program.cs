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
using Manager.Vanguard.Service;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddFileLogging("service");
builder.Logging.AddEventLog(options =>
{
    options.SourceName = ApplicationData.AppName;
});
#if DEBUG
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Trace);
#else
builder.Logging.AddFilter("Microsoft.Hosting.Lifetime", LogLevel.Warning);
#endif

builder.Services.AddWindowsService(options =>
{
    options.ServiceName = ApplicationData.ServiceName;
});

builder.Services.AddHostedService<Worker>();

IHost host = builder.Build();
host.Run();
