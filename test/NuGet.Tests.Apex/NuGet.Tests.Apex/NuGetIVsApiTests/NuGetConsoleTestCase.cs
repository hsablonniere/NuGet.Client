// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using FluentAssertions;
using Microsoft.Test.Apex.VisualStudio.Solution;
using NuGet.StaFact;
using NuGet.Test.Utility;
using Xunit;

namespace NuGet.Tests.Apex
{
    public class NuGetConsoleTestCase : SharedVisualStudioHostTestClass, IClassFixture<VisualStudioHostFixtureFactory>
    {
        public NuGetConsoleTestCase(VisualStudioHostFixtureFactory visualStudioHostFixtureFactory)
            : base(visualStudioHostFixtureFactory)
        {
        }

        // Verify PR only, packages.config is tested in InstallPackageFromPMCVerifyGetPackageDisplaysPackage
        [NuGetWpfTheory]
        [MemberData(nameof(GetPackageReferenceTemplates))]
        public void InstallPackageFromPMCWithNoAutoRestoreVerifyAssetsFile(ProjectTemplate projectTemplate)
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                // Arrange
                // Turn off auto restore
                pathContext.Settings.DisableAutoRestore();

                EnsureVisualStudioHost();
                var solutionService = VisualStudio.Get<SolutionService>();

                var project = CreateAndInitProject(projectTemplate, pathContext, solutionService);

                var packageName = "TestPackage";
                var packageVersion = "1.0.0";
                Utils.CreatePackageInSource(pathContext.PackageSource, packageName, packageVersion);

                var nugetConsole = GetConsole(project);

                var installed = nugetConsole.InstallPackageFromPMC(packageName, packageVersion);
                installed.Should().BeTrue("Install-Package should pass");

                // Verify install from project.assets.json
                var inAssetsFile = Utils.IsPackageInstalled(nugetConsole, project.FullPath, packageName, packageVersion);
                inAssetsFile.Should().BeTrue("package was installed");

                nugetConsole.Clear();
                solutionService.Save();
            }
        }

        // Verify packages.config and PackageReference
        [NuGetWpfTheory]
        [MemberData(nameof(GetTemplates))]
        public void InstallPackageFromPMCVerifyGetPackageDisplaysPackage(ProjectTemplate projectTemplate)
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                // Arrange
                EnsureVisualStudioHost();
                var solutionService = VisualStudio.Get<SolutionService>();
                var project = CreateAndInitProject(projectTemplate, pathContext, solutionService);

                var packageName = "TestPackage";
                var packageVersion = "1.0.0";
                Utils.CreatePackageInSource(pathContext.PackageSource, packageName, packageVersion);

                var nugetConsole = GetConsole(project);

                var installed = nugetConsole.InstallPackageFromPMC(packageName, packageVersion);
                installed.Should().BeTrue("Install-Package should pass");

                // Build before the install check to ensure that everything is up to date.
                project.Build();

                // Verify install from Get-Package
                nugetConsole.IsPackageInstalled(packageName, packageVersion).Should().BeTrue("package was installed");

                AssertNoErrors();

                nugetConsole.Clear();
                solutionService.Save();
            }
        }

        [NuGetWpfTheory]
        [MemberData(nameof(GetTemplates))]
        public void InstallPackageFromPMCFromNuGetOrg(ProjectTemplate projectTemplate)
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                // Arrange
                pathContext.Settings.DisableAutoRestore();
                EnsureVisualStudioHost();
                var solutionService = VisualStudio.Get<SolutionService>();
                var project = CreateAndInitProject(projectTemplate, pathContext, solutionService);
                var nugetConsole = GetConsole(project);

                var packageName = "newtonsoft.json";
                var packageVersion = "9.0.1";

                nugetConsole.InstallPackageFromPMC(packageName, packageVersion, "https://api.nuget.org/v3/index.json")
                    .Should()
                    .BeTrue("Install-Package should return on time");

                project.Build();

                Utils.IsPackageInstalled(nugetConsole, project.FullPath, packageName, packageVersion).Should().BeTrue("package should exist in the assets file");

                nugetConsole.Clear();
                solutionService.Save();
            }
        }

        [NuGetWpfTheory]
        [MemberData(nameof(GetTemplates))]
        public void UninstallPackageFromPMC(ProjectTemplate projectTemplate)
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                // Arrange
                EnsureVisualStudioHost();
                var solutionService = VisualStudio.Get<SolutionService>();

                solutionService.CreateEmptySolution("TestSolution", pathContext.SolutionRoot);
                var project = CreateAndInitProject(projectTemplate, pathContext, solutionService);

                var packageName = "TestPackage";
                var packageVersion = "1.0.0";
                Utils.CreatePackageInSource(pathContext.PackageSource, packageName, packageVersion);

                var nugetConsole = GetConsole(project);

                Assert.True(nugetConsole.InstallPackageFromPMC(packageName, packageVersion));
                project.Build();

                Assert.True(Utils.IsPackageInstalled(nugetConsole, project.FullPath, packageName, packageVersion));

                Assert.True(nugetConsole.UninstallPackageFromPMC(packageName));
                project.Build();

                Assert.False(Utils.IsPackageInstalled(nugetConsole, project.FullPath, packageName, packageVersion));

                AssertNoErrors();

                nugetConsole.Clear();
                solutionService.Save();
            }
        }

        [NuGetWpfTheory]
        [MemberData(nameof(GetTemplates))]
        public void UpdatePackageFromPMC(ProjectTemplate projectTemplate)
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                // Arrange
                EnsureVisualStudioHost();
                var solutionService = VisualStudio.Get<SolutionService>();

                solutionService.CreateEmptySolution("TestSolution", pathContext.SolutionRoot);
                var project = CreateAndInitProject(projectTemplate, pathContext, solutionService);

                var packageName = "TestPackage";
                var packageVersion1 = "1.0.0";
                var packageVersion2 = "2.0.0";
                Utils.CreatePackageInSource(pathContext.PackageSource, packageName, packageVersion1);
                Utils.CreatePackageInSource(pathContext.PackageSource, packageName, packageVersion2);

                var nugetConsole = GetConsole(project);

                Assert.True(nugetConsole.InstallPackageFromPMC(packageName, packageVersion1));
                project.Build();

                Assert.True(Utils.IsPackageInstalled(nugetConsole, project.FullPath, packageName, packageVersion1));
                
                Assert.True(nugetConsole.UpdatePackageFromPMC(packageName, packageVersion2));
                project.Build();

                Assert.True(Utils.IsPackageInstalled(nugetConsole, project.FullPath, packageName, packageVersion2));
                Assert.False(Utils.IsPackageInstalled(nugetConsole, project.FullPath, packageName, packageVersion1));

                AssertNoErrors();

                nugetConsole.Clear();
                solutionService.Save();
            }
        }

        [NuGetWpfTheory]
        [MemberData(nameof(GetTemplates))]
        public void InstallMultiplePackagesFromPMC(ProjectTemplate projectTemplate)
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                // Arrange
                EnsureVisualStudioHost();
                var solutionService = VisualStudio.Get<SolutionService>();

                solutionService.CreateEmptySolution("TestSolution", pathContext.SolutionRoot);
                var project = CreateAndInitProject(projectTemplate, pathContext, solutionService);

                var packageName1 = "TestPackage1";
                var packageVersion1 = "1.0.0";
                Utils.CreatePackageInSource(pathContext.PackageSource, packageName1, packageVersion1);

                var packageName2 = "TestPackage2";
                var packageVersion2 = "1.2.3";
                Utils.CreatePackageInSource(pathContext.PackageSource, packageName2, packageVersion2);

                var nugetConsole = GetConsole(project);

                Assert.True(nugetConsole.InstallPackageFromPMC(packageName1, packageVersion1));
                Assert.True(nugetConsole.InstallPackageFromPMC(packageName2, packageVersion2));
                project.Build();

                Assert.True(Utils.IsPackageInstalled(nugetConsole, project.FullPath, packageName1, packageVersion1));
                Assert.True(Utils.IsPackageInstalled(nugetConsole, project.FullPath, packageName2, packageVersion2));

                AssertNoErrors();

                nugetConsole.Clear();
                solutionService.Save();
            }
        }

        [NuGetWpfTheory]
        [MemberData(nameof(GetTemplates))]
        public void UninstallMultiplePackagesFromPMC(ProjectTemplate projectTemplate)
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                // Arrange
                EnsureVisualStudioHost();
                var solutionService = VisualStudio.Get<SolutionService>();

                solutionService.CreateEmptySolution("TestSolution", pathContext.SolutionRoot);
                var project = CreateAndInitProject(projectTemplate, pathContext, solutionService);

                var packageName1 = "TestPackage1";
                var packageVersion1 = "1.0.0";
                Utils.CreatePackageInSource(pathContext.PackageSource, packageName1, packageVersion1);

                var packageName2 = "TestPackage2";
                var packageVersion2 = "1.2.3";
                Utils.CreatePackageInSource(pathContext.PackageSource, packageName2, packageVersion2);

                var nugetConsole = GetConsole(project);

                Assert.True(nugetConsole.InstallPackageFromPMC(packageName1, packageVersion1));
                Assert.True(nugetConsole.InstallPackageFromPMC(packageName2, packageVersion2));
                project.Build();

                AssertNoErrors();

                Assert.True(Utils.IsPackageInstalled(nugetConsole, project.FullPath, packageName1, packageVersion1));
                Assert.True(Utils.IsPackageInstalled(nugetConsole, project.FullPath, packageName2, packageVersion2));

                Assert.True(nugetConsole.UninstallPackageFromPMC(packageName1));
                Assert.True(nugetConsole.UninstallPackageFromPMC(packageName2));
                project.Build();
                solutionService.SaveAll();

                Assert.False(Utils.IsPackageInstalled(nugetConsole, project.FullPath, packageName1, packageVersion1));
                Assert.False(Utils.IsPackageInstalled(nugetConsole, project.FullPath, packageName2, packageVersion2));

                nugetConsole.Clear();
                solutionService.Save();
            }
        }

        [NuGetWpfTheory]
        [MemberData(nameof(GetTemplates))]
        public void DowngradePackageFromPMC(ProjectTemplate projectTemplate)
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                // Arrange
                EnsureVisualStudioHost();
                var solutionService = VisualStudio.Get<SolutionService>();

                solutionService.CreateEmptySolution("TestSolution", pathContext.SolutionRoot);
                var project = CreateAndInitProject(projectTemplate, pathContext, solutionService);

                var packageName = "TestPackage";
                var packageVersion1 = "1.0.0";
                var packageVersion2 = "2.0.0";
                Utils.CreatePackageInSource(pathContext.PackageSource, packageName, packageVersion1);
                Utils.CreatePackageInSource(pathContext.PackageSource, packageName, packageVersion2);

                var nugetConsole = GetConsole(project);

                Assert.True(nugetConsole.InstallPackageFromPMC(packageName, packageVersion2));
                project.Build();

                Assert.True(Utils.IsPackageInstalled(nugetConsole, project.FullPath, packageName, packageVersion2));

                Assert.True(nugetConsole.UpdatePackageFromPMC(packageName, packageVersion1));
                project.Build();

                Assert.False(Utils.IsPackageInstalled(nugetConsole, project.FullPath, packageName, packageVersion2));
                Assert.True(Utils.IsPackageInstalled(nugetConsole, project.FullPath, packageName, packageVersion1));

                nugetConsole.Clear();
                solutionService.Save();
            }
        }

        [NuGetWpfTheory]
        [MemberData(nameof(GetNetCoreTemplates))]
        public void NetCoreTransitivePackageReference(ProjectTemplate projectTemplate)
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                // Arrange
                EnsureVisualStudioHost();
                var solutionService = VisualStudio.Get<SolutionService>();


                solutionService.CreateEmptySolution("TestSolution", pathContext.SolutionRoot);
                var project1 = solutionService.AddProject(ProjectLanguage.CSharp, projectTemplate, ProjectTargetFramework.V46, "TestProject1");
                project1.Build();
                var project2 = solutionService.AddProject(ProjectLanguage.CSharp, projectTemplate, ProjectTargetFramework.V46, "TestProject2");
                project2.Build();
                var project3 = solutionService.AddProject(ProjectLanguage.CSharp, projectTemplate, ProjectTargetFramework.V46, "TestProject3");
                project3.Build();
                solutionService.Build();

                project1.References.Dte.AddProjectReference(project2);
                project2.References.Dte.AddProjectReference(project3);
                solutionService.SaveAll();
                solutionService.Build();

                var nugetConsole = GetConsole(project3);
                var packageName = "newtonsoft.json";
                var packageVersion = "9.0.1";

                Assert.True(nugetConsole.InstallPackageFromPMC(packageName, packageVersion, "https://api.nuget.org/v3/index.json"));
                project1.Build();
                project2.Build();
                project3.Build();

                Assert.True(Utils.IsPackageInstalled(nugetConsole, project3.FullPath, packageName, packageVersion));

                Assert.True(project1.References.TryFindReferenceByName("newtonsoft.json", out var result));
                Assert.NotNull(result);
                Assert.True(project2.References.TryFindReferenceByName("newtonsoft.json", out var result2));
                Assert.NotNull(result2);

                nugetConsole.Clear();
                solutionService.Save();
            }
        }

        [NuGetWpfTheory]
        [MemberData(nameof(GetNetCoreTemplates))]
        public void NetCoreTransitivePackageReferenceLimit(ProjectTemplate projectTemplate)
        {
            using (var pathContext = new SimpleTestPathContext())
            {
                // Arrange
                EnsureVisualStudioHost();
                var solutionService = VisualStudio.Get<SolutionService>();

                solutionService.CreateEmptySolution("TestSolution", pathContext.SolutionRoot);
                var project1 = solutionService.AddProject(ProjectLanguage.CSharp, projectTemplate, ProjectTargetFramework.V46, "TestProject1");
                project1.Build();
                var project2 = solutionService.AddProject(ProjectLanguage.CSharp, projectTemplate, ProjectTargetFramework.V46, "TestProject2");
                project2.Build();
                var project3 = solutionService.AddProject(ProjectLanguage.CSharp, projectTemplate, ProjectTargetFramework.V46, "TestProject3");
                project3.Build();
                var projectX = solutionService.AddProject(ProjectLanguage.CSharp, projectTemplate, ProjectTargetFramework.V46, "TestProjectX");
                projectX.Build();
                solutionService.Build();

                project1.References.Dte.AddProjectReference(project2);
                project1.References.Dte.AddProjectReference(projectX);
                project2.References.Dte.AddProjectReference(project3);
                solutionService.SaveAll();
                solutionService.Build();

                var nugetConsole = GetConsole(project3);

                var packageName = "newtonsoft.json";
                var packageVersion = "9.0.1";

                Assert.True(nugetConsole.InstallPackageFromPMC(packageName, packageVersion, "https://api.nuget.org/v3/index.json"));
                project1.Build();
                project2.Build();
                project3.Build();
                projectX.Build();
                solutionService.Build();

                Assert.True(Utils.IsPackageInstalled(nugetConsole, project3.FullPath, packageName, packageVersion));

                Assert.True(project1.References.TryFindReferenceByName("newtonsoft.json", out var result));
                Assert.NotNull(result);
                Assert.True(project2.References.TryFindReferenceByName("newtonsoft.json", out var result2));
                Assert.NotNull(result2);
                Assert.False(projectX.References.TryFindReferenceByName("newtonsoft.json", out var resultX));
                Assert.Null(resultX);

                nugetConsole.Clear();
                solutionService.Save();
            }
        }

        private NuGetConsoleTestExtension GetConsole(ProjectTestExtension project)
        {
            var nugetTestService = GetNuGetTestService();
            nugetTestService.EnsurePackageManagerConsoleIsOpen().Should().BeTrue("Console was opened");
            var nugetConsole = nugetTestService.GetPackageManagerConsole(project.Name);
            return nugetConsole;
        }

        private static ProjectTestExtension CreateAndInitProject(ProjectTemplate projectTemplate, SimpleTestPathContext pathContext, SolutionService solutionService)
        {
            solutionService.CreateEmptySolution("TestSolution", pathContext.SolutionRoot);
            var project = solutionService.AddProject(ProjectLanguage.CSharp, projectTemplate, ProjectTargetFramework.V46, "TestProject");
            solutionService.Save();
            project.Build();

            return project;
        }

        public static IEnumerable<object[]> GetNetCoreTemplates()
        {
            for (var i = 0; i < GetIterations(); i++)
            {
                yield return new object[] { ProjectTemplate.NetCoreConsoleApp };
                yield return new object[] { ProjectTemplate.NetStandardClassLib };
            }
        }

        public static IEnumerable<object[]> GetPackageReferenceTemplates()
        {
            for (var i = 0; i < GetIterations(); i++)
            {
                yield return new object[] { ProjectTemplate.NetCoreConsoleApp };
                yield return new object[] { ProjectTemplate.NetStandardClassLib };
            }
        }

        public static IEnumerable<object[]> GetTemplates()
        {
            for (var i = 0; i < GetIterations(); i++)
            {
                yield return new object[] { ProjectTemplate.ClassLibrary };
                yield return new object[] { ProjectTemplate.NetCoreConsoleApp };
            }
        }

        private static int GetIterations()
        {
            var iterations = 1;

            if (int.TryParse(Environment.GetEnvironmentVariable("NUGET_APEX_TEST_ITERATIONS"), out var x) && x > 0)
            {
                iterations = x;
            }

            return iterations;
        }

        private void AssertNoErrors()
        {
            VisualStudio.HasNoErrorsInErrorList().Should().BeTrue("Errors should not exist in the error list: " + string.Join(", ", VisualStudio.ObjectModel.Shell.ToolWindows.ErrorList.Messages.Select(e => e.Description)));
            VisualStudio.HasNoErrorsInOutputWindows().Should().BeTrue("Errors should not exist in the output window");
        }
    }
}
