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

#:property UseWindowsForms=false
#:property NoWarn=CA2201

using System.Diagnostics;
using System.Reflection;
using System.Xml.Linq;

const string MSI_FOLDER = "msi";
const string MSI_BASE_NAME = "VanguardManagerInstaller";

Version version =
    Assembly.GetExecutingAssembly().GetName().Version ?? throw new Exception("Could not derive assembly version");

DirectoryInfo msiDir = new(Path.Combine(Directory.GetCurrentDirectory(), MSI_FOLDER));

Trace.Assert(msiDir.GetFiles().Length == 0);

foreach (DirectoryInfo dir in msiDir.GetDirectories())
{
    string culture = dir.Name;

    Trace.Assert(dir.GetDirectories().Length == 0);

    FileInfo[] files = dir.GetFiles();

    Trace.Assert(files.Length == 1);

    FileInfo msi = files[0];

    string newName = $"{MSI_BASE_NAME}-{culture}.msi";

    File.Move(msi.FullName, Path.Combine(msiDir.FullName, newName));

    Directory.Delete(dir.FullName);

    XDocument versionDoc = new(
        new XDeclaration("1.0", "UTF-8", null),
        new XElement(
            "item",
            new XElement("version", version),
            new XElement("mandatory", false),
            new XElement(
                "url",
                $"https://github.com/MatteoH2O1999/VanguardManager/releases/download/{version}/{newName}"
            ),
            new XElement("changelog", $"https://github.com/MatteoH2O1999/VanguardManager/releases/tag/{version}")
        )
    );

    versionDoc.Save(Path.Combine(msiDir.FullName, $"version-{culture}.xml"));
}
