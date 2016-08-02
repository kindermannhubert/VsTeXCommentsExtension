﻿using Microsoft.VisualStudio.Text;
using System;
using System.Collections.Generic;

namespace VsTeXCommentsExtension.Integration
{
    internal class TextSnapshotValuesPerVersionCache<T>
        where T : IDisposable
    {
        private const int VersionsToCache = 16;
        private const int CachedVersionsToRemoveOnCleanUp = 13; //clean up will run when we have VersionsToCache+1 versions

        private readonly Dictionary<int, T> valuesPerVersion = new Dictionary<int, T>();
        private readonly List<int> versions = new List<int>();
        private readonly Func<ITextSnapshot, T> getValueForSnapshot;

        public TextSnapshotValuesPerVersionCache(Func<ITextSnapshot, T> getValueForSnapshot)
        {
            this.getValueForSnapshot = getValueForSnapshot;
        }

        public T GetValue(ITextSnapshot snapshot)
        {
            lock (versions)
            {
                var version = snapshot.Version.VersionNumber;

                T value;
                if (!valuesPerVersion.TryGetValue(version, out value))
                {
                    value = getValueForSnapshot(snapshot);
                    valuesPerVersion.Add(version, value);
                    versions.Add(-version);

                    DismissOldVersions();
                }

                return value;
            }
        }

        private void DismissOldVersions()
        {
            if (versions.Count > VersionsToCache)
            {
                versions.Sort();
                var lastIndex = versions.Count - CachedVersionsToRemoveOnCleanUp;
                for (int i = versions.Count - 1; i >= lastIndex; i--)
                {
                    var versionToRemove = -versions[i];
                    var removedValue = valuesPerVersion[versionToRemove];
                    valuesPerVersion.Remove(versionToRemove);

                    removedValue?.Dispose();
                }
                versions.RemoveRange(versions.Count - CachedVersionsToRemoveOnCleanUp, CachedVersionsToRemoveOnCleanUp);
            }
        }
    }
}
