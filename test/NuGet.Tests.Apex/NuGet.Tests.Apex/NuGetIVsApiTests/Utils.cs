using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using NuGet.Common;
using NuGet.ProjectModel;
using NuGet.Test.Utility;
using NuGet.Versioning;

namespace NuGet.Tests.Apex
{
    public class Utils
    {
        public static void CreatePackageInSource(string packageSource, string packageName, string packageVersion)
        {
            var package = new SimpleTestPackageContext(packageName, packageVersion);
            package.Files.Clear();
            package.AddFile("lib/net45/_._");
            SimpleTestPackageUtility.CreatePackages(packageSource, package);
        }

        public static bool IsPackageInstalled(NuGetConsoleTestExtension nuGetConsole, string projectPath, string packageName, string packageVersion)
        {
            var isInstalled = false;

            var assetsFile = GetAssetsFilePath(projectPath);
            var packagesConfig = GetPackagesConfigPath(projectPath);
            var packagesConfigExists = File.Exists(packagesConfig);

            if (packagesConfigExists)
            {
                isInstalled = nuGetConsole.IsPackageInstalled(packageName, packageVersion);
            }
            else
            {
                isInstalled = PackageExistsInLockFile(assetsFile, packageName, packageVersion);
            }

            return isInstalled;
        }

        private static bool PackageExistsInLockFile(string pathToAssetsFile, string packageName, string packageVersion)
        {
            var version = NuGetVersion.Parse(packageVersion);
            var lockFile = GetAssetsFileWithRetry(pathToAssetsFile);
            var lockFileLibrary = lockFile.Libraries
                .SingleOrDefault(p => StringComparer.OrdinalIgnoreCase.Equals(p.Name, packageName)
                                    && p.Version.Equals(version));

            return lockFileLibrary != null;
        }

        private static LockFile GetAssetsFileWithRetry(string path)
        {
            var timeout = TimeSpan.FromSeconds(10);
            var timer = Stopwatch.StartNew();
            string content = null;

            do
            {
                if (File.Exists(path))
                {
                    try
                    {
                        content = File.ReadAllText(path);
                        var format = new LockFileFormat();
                        return format.Parse(content, path);
                    }
                    catch
                    {
                        // Ignore errors from conflicting writes.
                    }
                }

                Thread.Sleep(100);
            }
            while (timer.Elapsed < timeout);

            // File cannot be read
            if (File.Exists(path))
            {
                throw new InvalidOperationException("Unable to read: " + path);
            }
            else
            {
                throw new FileNotFoundException("Not found: " + path);
            }
        }

        private static string GetAssetsFilePath(string projectPath)
        {
            var projectDirectory = Path.GetDirectoryName(projectPath);
            return Path.Combine(projectDirectory, "obj", "project.assets.json");
        }

        private static string GetPackagesConfigPath(string projectPath)
        {
            var projectDirectory = Path.GetDirectoryName(projectPath);
            return Path.Combine(projectDirectory, "packages.config");
        }
    }
}
