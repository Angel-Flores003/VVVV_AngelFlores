using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Compilation;

namespace AtomicRealm.WorldBuilder.Guard
{
    /// <summary>
    /// Keeps the newest World Builder when a package with an older copy is imported. World Builder ships inside every
    /// Atomic Realm pack, so importing an older pack would otherwise replace a newer World Builder. When a package
    /// import starts, this saves a copy of the World Builder folder in the project's Library; when an import completes
    /// and the World Builder on disk is older than the saved one, the saved one is put back. The copy is kept until
    /// the last of several queued imports is done. After every import it also deletes files that World Builder no
    /// longer has (listed as "removed" in version.json), since a package import never deletes anything by itself.
    ///
    /// Unity compiles an imported older World Builder once before the import counts as completed, so a downgrading
    /// import costs one extra compile. This code is small, uses only public editor APIs, has no dependencies and
    /// should practically never change: every pack carries the same copy.
    /// </summary>
    [InitializeOnLoad]
    internal static class WorldBuilderImportGuard
    {
        internal const string Root = "Assets/Atomic Realm/World Builder";
        internal const string VersionFile = Root + "/version.json";
        const string Snapshot = "Library/AtomicRealm/WorldBuilderSnapshot";
        const string SnapshotTemp = Snapshot + ".tmp";
        const string SessionKey = "AtomicRealm.WorldBuilder.Guard.Session";
        const string PendingKey = "AtomicRealm.WorldBuilder.Guard.Pending";
        const string MovedKey = "AtomicRealm.WorldBuilder.Guard.MovedReported";
        const string DisableKey = "AtomicRealm.WorldBuilder.Guard.Disabled";

        /// <summary>Set to keep whatever World Builder version an import brings (e.g. to go back on purpose).</summary>
        internal static bool Disabled => EditorPrefs.GetBool(DisableKey, false);

        /// <summary>
        /// True while an import has put an older World Builder on disk that the guard is about to replace with the
        /// saved newer one (the version check stays quiet meanwhile).
        /// </summary>
        internal static bool Restoring
        {
            get
            {
                if (Disabled) return false;
                var saved = WorldBuilderVersion.Read(Snapshot + "/version.json");
                var now = WorldBuilderVersion.Read(VersionFile);
                return saved != null && now != null && now < saved;
            }
        }

        static WorldBuilderImportGuard()
        {
            AssetDatabase.importPackageStarted += package => Safely(() => OnStarted(package));
            AssetDatabase.importPackageCompleted += package => Safely(() => OnCompleted(package));
            AssetDatabase.importPackageCancelled += package => Safely(OnEnded);
            AssetDatabase.importPackageFailed += (package, error) => Safely(OnEnded);
            // a snapshot from an earlier editor session belongs to an import that never reported back
            // (dialog closed with its X, crash): it's stale
            if (!SessionState.GetBool(SessionKey, false))
            {
                SessionState.SetBool(SessionKey, true);
                Safely(Drop);
            }
            Safely(ReportMoved);
        }

        // the guard must never break an import or the editor: problems (e.g. read-only files) become one log line
        static void Safely(Action action)
        {
            try { action(); }
            catch (Exception e) { UnityEngine.Debug.LogWarning($"Atomic Realm World Builder guard: {e.Message}"); }
        }

        // Several queued imports report "started" first, then complete one by one, each writing its files only after
        // the one before completed. So the snapshot is kept until every started import has ended. An import whose
        // dialog is closed with its X never reports back, so its count stays until the editor restarts; that only
        // keeps the snapshot longer, and every start refreshes it.
        static int Pending => SessionState.GetInt(PendingKey, 0);

        static void AddPending(int delta) => SessionState.SetInt(PendingKey, Math.Max(0, Pending + delta));

        static void OnStarted(string package)
        {
            AddPending(1);
            if (Disabled) return;
            var current = WorldBuilderVersion.Read(VersionFile);
            if (current == null) return;
            // keep a snapshot newer than the disk: an import queued earlier has already put an older copy there
            var saved = WorldBuilderVersion.Read(Snapshot + "/version.json");
            if (saved != null && saved > current) return;
            TakeSnapshot();
        }

        static void OnCompleted(string package)
        {
            bool changed = false;
            try
            {
                var saved = WorldBuilderVersion.Read(Snapshot + "/version.json");
                var now = WorldBuilderVersion.Read(VersionFile);
                if (!Disabled && saved != null && now != null && now < saved)
                {
                    changed = true;
                    Restore();
                    UnityEngine.Debug.Log($"Atomic Realm: kept World Builder {saved}; \"{package}\" contained the older version {now}.");
                }
                OnEnded();
                changed |= DeleteRemovedFiles();
            }
            finally
            {
                // also after a failure halfway, so Unity picks up whatever was put back
                if (changed) AssetDatabase.Refresh();
            }
        }

        static void OnEnded()
        {
            AddPending(-1);
            if (Pending == 0) Drop();
        }

        // the snapshot is written next to its place and moved there only once complete, so a failure halfway
        // (e.g. a locked file) never leaves a partial snapshot that a restore would trust
        static void TakeSnapshot()
        {
            DeleteTree(SnapshotTemp);
            CopyTree(Root, SnapshotTemp);
            DeleteTree(Snapshot);
            Directory.Move(SnapshotTemp, Snapshot);
        }

        static void Drop()
        {
            DeleteTree(SnapshotTemp);
            DeleteTree(Snapshot);
        }

        static void DeleteTree(string dir)
        {
            if (!Directory.Exists(dir)) return;
            foreach (var file in Directory.GetFiles(dir, "*", SearchOption.AllDirectories)) File.SetAttributes(file, FileAttributes.Normal);
            Directory.Delete(dir, true);
        }

        // read-only files (e.g. under version control that locks files) can't be overwritten or deleted as they are
        static void Writable(string file)
        {
            if (File.Exists(file)) File.SetAttributes(file, FileAttributes.Normal);
        }

        static string Relative(string path, string root) =>
            path.Substring(root.Length + 1).Replace(Path.DirectorySeparatorChar, '/');

        static void CopyTree(string from, string to)
        {
            foreach (var file in Directory.GetFiles(from, "*", SearchOption.AllDirectories))
            {
                var target = Path.Combine(to, Relative(file, from));
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Copy(file, target, true);
                File.SetAttributes(target, FileAttributes.Normal);
            }
        }

        // the World Builder folder becomes exactly the snapshot: changed files are put back first, then extra files go
        // (so a failure halfway leaves the saved files in place rather than a folder with files missing)
        static void Restore()
        {
            var keep = new HashSet<string>(Directory.GetFiles(Snapshot, "*", SearchOption.AllDirectories).Select(p => Relative(p, Snapshot)));
            foreach (var rel in keep)
            {
                string source = Path.Combine(Snapshot, rel), target = Path.Combine(Root, rel);
                if (File.Exists(target) && File.ReadAllBytes(target).SequenceEqual(File.ReadAllBytes(source))) continue; // unchanged
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                // a file that is there keeps its attributes (e.g. read-only under version control)
                var attributes = File.Exists(target) ? File.GetAttributes(target) : FileAttributes.Normal;
                Writable(target);
                File.Copy(source, target, true);
                File.SetAttributes(target, attributes);
            }
            foreach (var file in Directory.GetFiles(Root, "*", SearchOption.AllDirectories))
                if (!keep.Contains(Relative(file, Root))) { Writable(file); File.Delete(file); }
            foreach (var dir in Directory.GetDirectories(Root, "*", SearchOption.AllDirectories).OrderByDescending(d => d.Length))
                if (!Directory.Exists(Path.Combine(Snapshot, Relative(dir, Root))) && !Directory.EnumerateFileSystemEntries(dir).Any())
                    Directory.Delete(dir);
        }

        // files World Builder dropped in a newer version (an import adds and overwrites, but never deletes)
        static bool DeleteRemovedFiles()
        {
            bool changed = false;
            foreach (var rel in WorldBuilderVersion.ReadRemoved(VersionFile))
            {
                if (rel.Contains("..")) continue; // only ever inside World Builder's folder
                if (Directory.Exists(Root + "/" + rel)) continue; // files only: a folder may hold the user's own files
                foreach (var path in new[] { Root + "/" + rel, Root + "/" + rel + ".meta" })
                    if (File.Exists(path)) { Writable(path); File.Delete(path); changed = true; }
                changed |= DeleteEmptyFolders(Path.GetDirectoryName(Root + "/" + rel));
            }
            return changed;
        }

        // folders left empty by removed files go too, with their .meta (an empty folder holds none of the user's files)
        static bool DeleteEmptyFolders(string dir)
        {
            bool changed = false;
            string root = Path.GetFullPath(Root);
            while (Directory.Exists(dir) && Path.GetFullPath(dir).Length > root.Length && !Directory.EnumerateFileSystemEntries(dir).Any())
            {
                Directory.Delete(dir);
                if (File.Exists(dir + ".meta")) { Writable(dir + ".meta"); File.Delete(dir + ".meta"); }
                changed = true;
                dir = Path.GetDirectoryName(dir);
            }
            return changed;
        }

        // the guard, the version check and pack updates all expect World Builder in its own place: a moved copy is
        // not guarded, and the next pack import adds a second copy
        static void ReportMoved()
        {
            if (File.Exists(VersionFile) || SessionState.GetBool(MovedKey, false)) return;
            string asmdef = CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName("AtomicRealm.WorldBuilder.Guard");
            if (string.IsNullOrEmpty(asmdef) || asmdef.Replace('\\', '/').StartsWith(Root + "/")) return;
            SessionState.SetBool(MovedKey, true);
            UnityEngine.Debug.LogWarning($"Atomic Realm: World Builder must stay in \"{Root}\" (found in \"{Path.GetDirectoryName(Path.GetDirectoryName(asmdef))}\"). Move it back, or pack updates will add a second copy.");
        }
    }
}
