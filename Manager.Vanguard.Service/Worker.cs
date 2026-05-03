// Copyright (C) 2026 Matteo Dell'Acqua
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY

namespace Manager.Vanguard.Service
{
    public class Worker(ILogger<Worker> Logger) : BackgroundService
    {
        private readonly ILogger logger = Logger;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            throw new NotImplementedException();
        }
    }
}
