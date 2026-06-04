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

#:package Octokit@*
#:property UseWindowsForms=false
#:property NoWarn=CA2201

using System.Diagnostics;
using System.Reflection;
using Octokit;

string GITHUB_TOKEN =
    Environment.GetEnvironmentVariable("GITHUB_TOKEN") ?? throw new Exception("GITHUB_TOKEN not found");
Version version =
    Assembly.GetExecutingAssembly().GetName().Version ?? throw new Exception("Could not derive assembly version");
string tag = $"{version.Major}.{version.Minor}.{version.Build}";
string argsTag = $"{args[0]}.{args[1]}.{args[2]}";

GitHubClient github = new(new ProductHeaderValue("VanguardManagerCI")) { Credentials = new(GITHUB_TOKEN) };

Repository repo = await github.Repository.Get("MatteoH2O1999", "VanguardManager");

IReadOnlyList<RepositoryTag> tags = await github.Repository.GetAllTags(repo.Id);

RepositoryTag[] filteredTags = [.. tags.Where(t => t.Name == tag)];

IReadOnlyList<Release> releases = await github.Repository.Release.GetAll(repo.Id);

Release[] filteredReleases = [.. releases.Where(r => r.TagName == tag)];

Console.WriteLine("Performing sanity checks...");

Trace.Assert(Environment.GetEnvironmentVariable("GITHUB_REF") == $"refs/tags/{tag}");
Trace.Assert(filteredReleases.Length == 0);
Trace.Assert(filteredTags.Length == 1);
Trace.Assert(tag == argsTag);

Console.WriteLine("Chacks completed successfully. Release may proceed.");
