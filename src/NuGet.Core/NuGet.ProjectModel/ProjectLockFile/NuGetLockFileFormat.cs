// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.Versioning;

namespace NuGet.ProjectModel
{
    public class NuGetLockFileFormat
    {
        public static readonly int Version = 1;

        public static readonly string LockFileName = "packages.lock.json";

        private const string VersionProperty = "version";
        private const string ResolvedProperty = "resolved";
        private const string RequestedProperty = "requested";
        private const string Sha512Property = "sha512";
        private const string DependenciesProperty = "dependencies";
        private const string TypeProperty = "type";

        public static NuGetLockFile Read(string filePath)
        {
            return Read(filePath, NullLogger.Instance);
        }

        public static NuGetLockFile Read(string filePath, ILogger log)
        {
            using (var stream = File.OpenRead(filePath))
            {
                return Read(stream, log, filePath);
            }
        }

        public static NuGetLockFile Read(Stream stream, ILogger log, string path)
        {
            using (var textReader = new StreamReader(stream))
            {
                return Read(textReader, log, path);
            }
        }

        public static NuGetLockFile Read(TextReader reader, ILogger log, string path)
        {
            try
            {
                var json = JsonUtility.LoadJson(reader);
                var lockFile = ReadLockFile(json);
                lockFile.Path = path;
                return lockFile;
            }
            catch (Exception ex)
            {
                log.LogWarning(string.Format(CultureInfo.CurrentCulture,
                    Strings.Log_ErrorReadingLockFile,
                    path, ex.Message));

                // Ran into parsing errors, mark it as unlocked and out-of-date
                return new NuGetLockFile
                {
                    Version = int.MinValue,
                    Path = path
                };
            }
        }

        private static NuGetLockFile ReadLockFile(JObject cursor)
        {
            var lockFile = new NuGetLockFile()
            {
                Version = LockFileFormat.ReadInt(cursor, VersionProperty, defaultValue: int.MinValue),
                Targets = LockFileFormat.ReadObject(cursor[DependenciesProperty] as JObject, ReadDependency),
            };

            return lockFile;
        }

        public static void Write(string filePath, NuGetLockFile lockFile)
        {
            // Create the directory if it does not exist
            var fileInfo = new FileInfo(filePath);
            fileInfo.Directory.Create();

            using (var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                Write(stream, lockFile);
            }
        }

        public static void Write(Stream stream, NuGetLockFile lockFile)
        {
            using (var textWriter = new StreamWriter(stream))
            {
                Write(textWriter, lockFile);
            }
        }

        public static void Write(TextWriter textWriter, NuGetLockFile lockFile)
        {
            using (var jsonWriter = new JsonTextWriter(textWriter))
            {
                jsonWriter.Formatting = Formatting.Indented;

                var json = WriteLockFile(lockFile);
                json.WriteTo(jsonWriter);
            }
        }

        private static JObject WriteLockFile(NuGetLockFile lockFile)
        {
            var json = new JObject
            {
                [VersionProperty] = new JValue(lockFile.Version),
                [DependenciesProperty] = LockFileFormat.WriteObject(lockFile.Targets, WriteTarget),
            };

            return json;
        }

        private static NuGetLockFileTarget ReadDependency(string property, JToken json)
        {
            var target = new NuGetLockFileTarget();

            var parts = property.Split(LockFileFormat.PathSplitChars, 2);
            target.TargetFramework = NuGetFramework.Parse(parts[0]);

            if (parts.Length == 2)
            {
                target.RuntimeIdentifier = parts[1];
            }

            target.Dependencies = LockFileFormat.ReadObject(json as JObject, ReadTargetDependency);

            return target;
        }

        private static LockFileDependency ReadTargetDependency(string property, JToken json)
        {
            var dependency = new LockFileDependency();

            dependency.Id = property;

            var jObject = json as JObject;

            var typeString = LockFileFormat.ReadProperty<string>(jObject, TypeProperty);

            if (!string.IsNullOrEmpty(typeString)
                && Enum.TryParse<PackageInstallationType>(typeString, ignoreCase: true, result: out var installationType))
            {
                dependency.Type = installationType;
            }

            var resolvedString = LockFileFormat.ReadProperty<string>(jObject, ResolvedProperty);

            if (!string.IsNullOrEmpty(resolvedString))
            {
                dependency.ResolvedVersion = NuGetVersion.Parse(resolvedString);
            }

            var requestedString = LockFileFormat.ReadProperty<string>(jObject, RequestedProperty);

            if (!string.IsNullOrEmpty(requestedString))
            {
                dependency.RequestedVersion = VersionRange.Parse(requestedString);
            }

            dependency.Sha512 = LockFileFormat.ReadProperty<string>(jObject, Sha512Property);
            dependency.Dependencies = LockFileFormat.ReadObject(json[DependenciesProperty] as JObject, LockFileFormat.ReadPackageDependency);

            return dependency;
        }

        private static JProperty WriteTarget(NuGetLockFileTarget target)
        {
            var json = LockFileFormat.WriteObject(target.Dependencies, WriteTargetDependency);

            var key = target.Name;

            return new JProperty(key, json);
        }

        private static JProperty WriteTargetDependency(LockFileDependency dependency)
        {
            var json = new JObject();

            json[TypeProperty] = dependency.Type.ToString();

            if (dependency.RequestedVersion != null)
            {
                json[RequestedProperty] = dependency.RequestedVersion.ToNormalizedString();
            }

            if (dependency.ResolvedVersion != null)
            {
                json[ResolvedProperty] = dependency.ResolvedVersion.ToNormalizedString();
            }

            if (dependency.Sha512 != null)
            {
                json[Sha512Property] = dependency.Sha512;
            }

            if (dependency.Dependencies?.Count > 0)
            {
                var ordered = dependency.Dependencies.OrderBy(dep => dep.Id, StringComparer.Ordinal);

                json[DependenciesProperty] = LockFileFormat.WriteObject(ordered, LockFileFormat.WritePackageDependency);
            }

            return new JProperty(dependency.Id, json);
        }

    }
}
