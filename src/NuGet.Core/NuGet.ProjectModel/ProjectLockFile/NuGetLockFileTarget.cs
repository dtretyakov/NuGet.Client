// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using NuGet.Frameworks;
using NuGet.Shared;

namespace NuGet.ProjectModel
{
    /// <summary>
    /// FrameworkName/RuntimeIdentifier combination
    /// </summary>
    public class NuGetLockFileTarget : IEquatable<NuGetLockFileTarget>
    {
        /// <summary>
        /// Target framework.
        /// </summary>
        public NuGetFramework TargetFramework { get; set; }

        /// <summary>
        /// 
        /// </summary>
        public IList<LockFileDependency> Dependencies { get; set; } = new List<LockFileDependency>();

        /// <summary>
        /// Full framework name.
        /// </summary>
        public string Name => TargetFramework.DotNetFrameworkName;

        public bool Equals(NuGetLockFileTarget other)
        {
            if (other == null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return NuGetFramework.Comparer.Equals(TargetFramework, other.TargetFramework)
                && EqualityUtility.SequenceEqualWithNullCheck(Dependencies, other.Dependencies);
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as NuGetLockFileTarget);
        }

        public override int GetHashCode()
        {
            var combiner = new HashCodeCombiner();

            combiner.AddObject(TargetFramework);
            combiner.AddSequence(Dependencies);

            return combiner.CombinedHash;
        }
    }
}