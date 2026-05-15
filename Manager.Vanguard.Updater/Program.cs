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

using Manager.Vanguard.Common;
using Manager.Vanguard.Updater;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddFile($"{ApplicationData.Local}/logs/updater-{{Date}}.log", minimumLevel: LogLevel.Information);
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

Runner runner = app.Services.GetRequiredService<Runner>();
runner.Run();
