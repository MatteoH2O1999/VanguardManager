#:property UseWindowsForms=false

using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

const string MSI_FOLDER = "msi";
const string MSI_BASE_NAME = "VanguardManagerInstaller";

DirectoryInfo msiDir = new(Path.Combine(Directory.GetCurrentDirectory(), MSI_FOLDER));

Trace.Assert(msiDir.GetFiles().Length == 0);

foreach (DirectoryInfo dir in msiDir.GetDirectories())
{
    string culture = dir.Name;

    Trace.Assert(dir.GetDirectories().Length == 0);

    FileInfo[] files = dir.GetFiles();

    Trace.Assert(files.Length == 1);

    FileInfo msi = files[0];

    File.Move(msi.FullName, Path.Combine(msiDir.FullName, $"{MSI_BASE_NAME}-{culture}.msi"));

    Directory.Delete(dir.FullName);
}

Console.WriteLine(Assembly.GetExecutingAssembly().GetName().Version);
