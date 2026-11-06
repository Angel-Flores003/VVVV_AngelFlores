using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace AtomicRealm.WorldBuilder.Guard
{
    /// <summary>
    /// Reads the small JSON files the guard and the version check rely on, without depending on any other code:
    /// World Builder's version.json ({"version": "x.y.z", "removed": ["Runtime/Old.cs", ...]}) and each pack's
    /// AtomicRealmPack.json ({"name": "...", "version": "x.y.z", "worldBuilder": "x.y.z"}).
    /// </summary>
    internal static class WorldBuilderVersion
    {
        internal static Version Read(string file) => ReadField(file, "version");

        internal static Version ReadField(string file, string field)
        {
            if (!File.Exists(file)) return null;
            var m = Regex.Match(File.ReadAllText(file), "\"" + Regex.Escape(field) + "\"\\s*:\\s*\"(\\d+(\\.\\d+){1,3})\"");
            return m.Success ? new Version(m.Groups[1].Value) : null;
        }

        internal static string ReadString(string file, string field)
        {
            if (!File.Exists(file)) return null;
            var m = Regex.Match(File.ReadAllText(file), "\"" + Regex.Escape(field) + "\"\\s*:\\s*\"([^\"]*)\"");
            return m.Success ? m.Groups[1].Value : null;
        }

        internal static IEnumerable<string> ReadRemoved(string file)
        {
            if (!File.Exists(file)) yield break;
            var list = Regex.Match(File.ReadAllText(file), "\"removed\"\\s*:\\s*\\[([^\\]]*)\\]");
            if (!list.Success) yield break;
            foreach (Match m in Regex.Matches(list.Groups[1].Value, "\"([^\"]+)\"")) yield return m.Groups[1].Value;
        }
    }
}
