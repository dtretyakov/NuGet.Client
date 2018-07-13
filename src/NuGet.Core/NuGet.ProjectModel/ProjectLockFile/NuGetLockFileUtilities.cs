using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NuGet.Common;
using NuGet.LibraryModel;
using NuGet.Packaging.Core;
using NuGet.Shared;

namespace NuGet.ProjectModel
{
    public static class NuGetLockFileUtilities
    {
        public static bool IsNuGetLockFileSupported(PackageSpec project)
        {
            var restorePackagesWithLockFile = project.RestoreMetadata.RestorePackagesWithLockFile;
            return MSBuildStringUtility.IsTrue(restorePackagesWithLockFile) || File.Exists(GetNuGetLockFilePath(project));
        }

        public static string GetNuGetLockFilePath(PackageSpec project)
        {
            var path = project.RestoreMetadata.NuGetLockFilePath;

            if (string.IsNullOrEmpty(path))
            {
                path = Path.Combine(project.BaseDirectory, "packages." + project.RestoreMetadata.ProjectName.Replace(' ', '_') + ".lock.json");

                if (!File.Exists(path))
                {
                    path = Path.Combine(project.BaseDirectory, NuGetLockFileFormat.LockFileName);
                }
            }

            return path;
        }

        public static bool IsLockFileStillValid(DependencyGraphSpec dgSpec, NuGetLockFile nuGetLockFile)
        {
            var uniqueName = dgSpec.Restore.First();
            var project = dgSpec.GetProjectSpec(uniqueName);

            // Validate all the direct dependencies
            foreach (var framework in project.TargetFrameworks)
            {
                var target = nuGetLockFile.Targets.FirstOrDefault(
                    t => EqualityUtility.EqualsWithNullCheck(t.TargetFramework, framework.FrameworkName));

                if (target != null)
                {
                    var directDependencies = target.Dependencies.Where(dep => dep.Type == PackageInstallationType.Direct);

                    if (HasProjectDependencyChanged(framework.Dependencies, directDependencies))
                    {
                        // lock file is out of sync
                        return false;
                    }
                }
            }

            // Validate all P2P references
            foreach (var p2p in dgSpec.Projects)
            {
                if (PathUtility.GetStringComparerBasedOnOS().Equals(p2p.RestoreMetadata.ProjectUniqueName, uniqueName))
                {
                    continue;
                }

                foreach (var framework in p2p.TargetFrameworks)
                {
                    var target = nuGetLockFile.Targets.FirstOrDefault(
                    t => EqualityUtility.EqualsWithNullCheck(t.TargetFramework, framework.FrameworkName));

                    if (target != null)
                    {
                        var projectDependency = target.Dependencies.FirstOrDefault(
                            dep => dep.Type == PackageInstallationType.Project &&
                            PathUtility.GetStringComparerBasedOnOS().Equals(dep.Id, p2p.RestoreMetadata.ProjectName));

                        if (HasP2PDependencyChanged(framework.Dependencies, projectDependency))
                        {
                            // lock file is out of sync
                            return false;
                        }
                    }
                }
            }

            return true;
        }

        private static bool HasProjectDependencyChanged(IEnumerable<LibraryDependency> newDependencies, IEnumerable<LockFileDependency> lockFileDependencies)
        {
            foreach (var dependency in newDependencies.Where(dep => dep.LibraryRange.TypeConstraint == LibraryDependencyTarget.Package))
            {
                var lockFileDependency = lockFileDependencies.FirstOrDefault(d => PathUtility.GetStringComparerBasedOnOS().Equals(d.Id, dependency.Name));

                if (lockFileDependency == null || !EqualityUtility.EqualsWithNullCheck(lockFileDependency.RequestedVersion, dependency.LibraryRange.VersionRange))
                {
                    // dependency has changed and lock file is out of sync.
                    return true;
                }
            }

            // no dependency changed. Lock file is still valid.
            return false;
        }

        private static bool HasP2PDependencyChanged(IEnumerable<LibraryDependency> newDependencies, LockFileDependency projectDependency)
        {
            if (projectDependency == null)
            {
                // project dependency doesn't exists in lock file so it's out of sync.
                return true;
            }

            foreach (var dependency in newDependencies.Where(dep => dep.LibraryRange.TypeConstraint == LibraryDependencyTarget.Package))
            {
                var matchedP2PLibrary = projectDependency.Dependencies.FirstOrDefault(dep => PathUtility.GetStringComparerBasedOnOS().Equals(dep.Id, dependency.Name));

                if (matchedP2PLibrary == null || !EqualityUtility.EqualsWithNullCheck(matchedP2PLibrary.VersionRange, dependency.LibraryRange.VersionRange))
                {
                    // P2P dependency has changed and lock file is out of sync.
                    return true;
                }
            }

            // no dependency changed. Lock file is still valid.
            return false;
        }
    }
}
