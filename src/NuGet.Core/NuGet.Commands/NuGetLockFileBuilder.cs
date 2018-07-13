using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Packaging.Core;
using NuGet.ProjectModel;
using NuGet.Shared;

namespace NuGet.Commands
{
    public class NuGetLockFileBuilder
    {
        private readonly int _lockFileVersion;
        public NuGetLockFileBuilder(int lockFileVersion)
        {
            _lockFileVersion = lockFileVersion;
        }

        public NuGetLockFile CreateNuGetLockFile(LockFile assetsFile)
        {
            var lockFile = new NuGetLockFile()
            {
                Version = _lockFileVersion
            };

            var libraryLookup = assetsFile.Libraries.Where(e => e.Type == LibraryType.Package)
                .ToDictionary(e => new PackageIdentity(e.Name, e.Version));

            foreach (var target in assetsFile.Targets.Where(target => string.IsNullOrEmpty(target.RuntimeIdentifier)))
            {
                var nuGettarget = new NuGetLockFileTarget()
                {
                    TargetFramework = target.TargetFramework,
                };

                var framework = assetsFile.PackageSpec.TargetFrameworks.FirstOrDefault(
                    f => EqualityUtility.EqualsWithNullCheck(f.FrameworkName, target.TargetFramework));

                foreach (var library in target.Libraries.Where(e => e.Type == LibraryType.Package))
                {
                    var identity = new PackageIdentity(library.Name, library.Version);

                    var dependency = new LockFileDependency()
                    {
                        Id = library.Name,
                        ResolvedVersion = library.Version,
                        Sha512 = libraryLookup[identity].Sha512,
                        Dependencies = library.Dependencies
                    };

                    var framework_dep = framework.Dependencies.FirstOrDefault(
                        dep => PathUtility.GetStringComparerBasedOnOS().Equals(dep.Name, library.Name));

                    if (framework_dep != null)
                    {
                        dependency.Type = PackageInstallationType.Direct;
                        dependency.RequestedVersion = framework_dep.LibraryRange.VersionRange;
                    }
                    else
                    {
                        dependency.Type = PackageInstallationType.Transitive;
                    }

                    nuGettarget.Dependencies.Add(dependency);
                }

                foreach (var projectRef in target.Libraries.Where(e => e.Type == LibraryType.Project || e.Type == LibraryType.ExternalProject))
                {
                    var dependency = new LockFileDependency()
                    {
                        Id = projectRef.Name,
                        Dependencies = projectRef.Dependencies,
                        Type = PackageInstallationType.Project
                    };

                    nuGettarget.Dependencies.Add(dependency);
                }

                nuGettarget.Dependencies = nuGettarget.Dependencies.OrderBy(d => d.Type).ToList();

                lockFile.Targets.Add(nuGettarget);
            }

            return lockFile;
        }

    }
}
