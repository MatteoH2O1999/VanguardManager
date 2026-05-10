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

#:package Octokit@*
#:property UseWindowsForms=false
#:property NoWarn=CA2201

using System.Reflection;
using Octokit;

string GITHUB_TOKEN =
    Environment.GetEnvironmentVariable("GITHUB_TOKEN") ?? throw new Exception("GITHUB_TOKEN not found");
Version version =
    Assembly.GetExecutingAssembly().GetName().Version ?? throw new Exception("Could not derive assembly version");
string tag = $"{version.Major}.{version.Minor}.{version.Build}";

GitHubClient github = new(new ProductHeaderValue("VanguardManagerCI")) { Credentials = new(GITHUB_TOKEN) };

Repository repo = await github.Repository.Get("MatteoH2O1999", "VanguardManager");

Console.WriteLine(repo.ToString());

IReadOnlyList<GitHubCommit> commits = await github.Repository.Commit.GetAll(repo.Id);

Console.WriteLine(string.Join(", ", commits.Select(c => c.Sha)));
