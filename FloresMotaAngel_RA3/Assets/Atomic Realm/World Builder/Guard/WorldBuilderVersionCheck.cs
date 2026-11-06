using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;

namespace AtomicRealm.WorldBuilder.Guard
{
    /// <summary>
    /// Tells the user when the installed World Builder is older than an installed pack needs. Every pack folder
    /// (Assets/Atomic Realm/&lt;Pack&gt;/) has an AtomicRealmPack.json with the World Builder version it needs.
    /// Checked when the editor starts or reloads and after every package import; a given shortfall is reported
    /// once per editor session. This catches what the import guard can't put right, e.g. several packs imported
    /// at once into a new project.
    /// </summary>
    [InitializeOnLoad]
    internal static class WorldBuilderVersionCheck
    {
        const string PackFile = "AtomicRealmPack.json";
        const string ReportedKey = "AtomicRealm.WorldBuilder.VersionCheck.Reported";

        static WorldBuilderVersionCheck()
        {
            EditorApplication.delayCall += Check;
            AssetDatabase.importPackageCompleted += package => EditorApplication.delayCall += Check;
        }

        internal static void Check()
        {
            // mid-import the older copy is on disk only until the guard puts the newer one back
            if (WorldBuilderImportGuard.Restoring) return;
            var installed = WorldBuilderVersion.Read(WorldBuilderImportGuard.VersionFile);
            var root = Path.GetDirectoryName(WorldBuilderImportGuard.Root);
            if (!Directory.Exists(root)) return;

            var missing = new List<string>();
            foreach (var dir in Directory.GetDirectories(root))
            {
                var file = Path.Combine(dir, PackFile);
                var needs = WorldBuilderVersion.ReadField(file, "worldBuilder");
                if (needs == null || (installed != null && installed >= needs)) continue;
                var name = WorldBuilderVersion.ReadString(file, "name") ?? Path.GetFileName(dir);
                missing.Add($"{name} needs World Builder {needs}");
            }
            if (missing.Count == 0) return;

            string have = installed == null ? "No World Builder is installed" : $"World Builder {installed} is installed";
            string advice = "Update or re-import the pack (it includes World Builder).";
            string message = string.Join("\n", missing) + $"\n\n{have}. {advice}";
            if (SessionState.GetString(ReportedKey, "") == message) return;
            SessionState.SetString(ReportedKey, message);
            UnityEngine.Debug.LogWarning($"Atomic Realm: {string.Join("; ", missing)}. {have}. {advice}");
            EditorUtility.DisplayDialog("Atomic Realm World Builder", message, "OK");
        }
    }
}
