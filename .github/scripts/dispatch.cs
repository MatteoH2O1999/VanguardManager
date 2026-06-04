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
string sha = Environment.GetEnvironmentVariable("GITHUB_SHA") ?? throw new Exception("GITHUB_SHA not found");

Console.WriteLine($"Checking if tag {tag} already exists...");

GitHubClient github = new(new ProductHeaderValue("VanguardManagerCI")) { Credentials = new(GITHUB_TOKEN) };

Repository repo = await github.Repository.Get("MatteoH2O1999", "VanguardManager");

IReadOnlyList<RepositoryTag> tags = await github.Repository.GetAllTags(repo.Id);

RepositoryTag[] filteredTags = [.. tags.Where(t => t.Name == tag)];

Trace.Assert(filteredTags.Length <= 1);

if (filteredTags.Length == 1)
{
    Console.WriteLine($"Tag {tag} already exist. No new release is needed.");
}
else
{
    Trace.Assert(filteredTags.Length == 0);

    Console.WriteLine($"Creating tag {tag}...");
    string tagSha = (
        await github.Git.Tag.Create(
            repo.Id,
            new()
            {
                Object = sha,
                Tag = tag,
                Tagger = new("github-actions[bot]", "github-actions[bot]@users.noreply.github.com", DateTimeOffset.Now),
                Type = TaggedType.Commit,
                Message = tag,
            }
        )
    ).Sha;
    Reference tagRef = await github.Git.Reference.Create(repo.Id, new($"refs/tags/{tag}", tagSha));

    Console.WriteLine($"Dispatching release job...");
    await github.Actions.Workflows.CreateDispatch(
        repo.Id,
        ".github/workflows/release.yml",
        new(tag)
        {
            Inputs = new Dictionary<string, object>()
            {
                ["major"] = $"{version.Major}",
                ["minor"] = $"{version.Minor}",
                ["patch"] = $"{version.Build}",
                ["dotnet-version"] =
                    Environment.GetEnvironmentVariable("DOTNET_VERSION")
                    ?? throw new Exception("Could not find DOTNET_VERSION"),
            },
        }
    );
}
