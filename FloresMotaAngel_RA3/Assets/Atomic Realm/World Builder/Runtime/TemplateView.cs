using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace AtomicRealm.WorldBuilder
{
    /// <summary>
    /// Editor-only overlay: while on, the Scene view and the
    /// Tile Palette (there with its Gizmos toggle on) draw each Atomic Realm tile's template
    /// sprite on top of it (see <see cref="TileTemplateMap"/>). A tile whose edge doesn't match the neighbour across
    /// it (different solid/frame/empty profile) gets the red variant, so broken seams stand out. In the Scene view only
    /// the tilemaps selected in the Hierarchy are overlaid (all tilemaps when none is selected). The overlay is only
    /// drawn, never stored in the tilemaps, so scenes, prefabs, play mode and builds are unaffected. In a Tile
    /// Palette, the sheet blocks show their template without the red check (their neighbours aren't meant to
    /// connect), so red only marks the example structures. On/off is saved per user and project (EditorPrefs).
    /// Toggle via Atomic Realm > Template View or the "Template View" button in the Scene view toolbar. Tilemaps hidden
    /// with the Hierarchy eye get no overlay, even while the Scene view's visibility switch shows them.
    /// </summary>
    public static class TemplateView
    {
#if UNITY_EDITOR
        const string EmptyEdge = "-";

        // The changeable state lives in one object instead of static fields, which the Asset Store validator flags
        // (a reminder that they keep their values when Play Mode starts without reloading scripts; this state should).
        class State
        {
            public bool on;
            public Dictionary<Sprite, TileTemplateMap.Pair> map; // keyed by art sprite
            public double rescanAt;                              // when an empty result may be looked up again
            public Action changed;
            public Material material;
            public bool warnedDrawError;
        }
        static readonly State S = new State();

        // EditorPrefs are shared by all projects on the machine, so the switch is stored per project
        static string PrefKey => "AtomicRealm.TemplateView." + PlayerSettings.productGUID;

        /// <summary>Called when <see cref="On"/> changes.</summary>
        public static void AddChangedListener(Action listener) => S.changed += listener;
        public static void RemoveChangedListener(Action listener) => S.changed -= listener;

        [InitializeOnLoadMethod]
        static void Init()
        {
            S.on = EditorPrefs.GetBool(PrefKey, false);
            SceneView.duringSceneGui += OnSceneGUI;
            Selection.selectionChanged += () => { if (S.on) SceneView.RepaintAll(); };
        }

        public static bool On => S.on;

        public static void SetOn(bool value)
        {
            if (S.on == value) return;
            S.on = value;
            EditorPrefs.SetBool(PrefKey, value);
            UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
            S.changed?.Invoke();
        }

        // the cached pairs follow any Template Map that is (re)imported or deleted; Inspector edits to a map show
        // once the asset is saved
        class MapWatcher : AssetPostprocessor
        {
            static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
            {
                foreach (var p in imported)
                    if (AssetDatabase.GetMainAssetTypeAtPath(p) == typeof(TileTemplateMap)) { Invalidate(); return; }
                if (deleted.Length > 0) Invalidate();
            }
        }

        /// <summary>Forget the cached pairs (called automatically when a TileTemplateMap is imported or deleted).</summary>
        public static void Invalidate()
        {
            S.map = null;
            if (S.on) UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        }

        static bool TryGet(Sprite art, out TileTemplateMap.Pair pair)
        {
            pair = default;
            if (art == null) return false;
            // nothing found (no Template Map in the project, or asked mid-import): keep that answer for a moment
            // instead of searching the asset database again for every cell of every redraw
            if (S.map == null || (S.map.Count == 0 && EditorApplication.timeSinceStartup >= S.rescanAt))
            {
                S.map = new Dictionary<Sprite, TileTemplateMap.Pair>();
                foreach (var guid in AssetDatabase.FindAssets("t:" + nameof(TileTemplateMap)))
                {
                    var map = AssetDatabase.LoadAssetAtPath<TileTemplateMap>(AssetDatabase.GUIDToAssetPath(guid));
                    if (map == null) continue;
                    foreach (var p in map.pairs) if (p.art != null) S.map[p.art] = p;
                }
                S.rescanAt = EditorApplication.timeSinceStartup + 2.0;
            }
            return S.map.TryGetValue(art, out pair);
        }

        /// <summary>The template sprite for an art sprite, or null if it has none.</summary>
        public static Sprite For(Sprite art) => TryGet(art, out var p) ? p.template : null;

        // the sprite a cell currently shows, including the brush preview while painting
        static Sprite Shown(Tilemap map, Vector3Int cell) =>
            map.HasEditorPreviewTile(cell) ? map.GetEditorPreviewSprite(cell) : map.GetSprite(cell);

        // what the neighbour shows on the edge it shares with the cell; null when unknown (not an Atomic Realm tile,
        // or no edge given in its map entry)
        static string NeighbourEdge(Tilemap map, Vector3Int cell, Vector3Int dir)
        {
            var s = Shown(map, cell + dir);
            if (s == null) return EmptyEdge;
            if (!TryGet(s, out var p)) return null;
            string edge = dir == Vector3Int.up ? p.south : dir == Vector3Int.down ? p.north : dir == Vector3Int.right ? p.west : p.east;
            return string.IsNullOrEmpty(edge) ? null : edge;
        }

        /// <summary>True when one of the cell's edges doesn't match the neighbour across it. Editor only.</summary>
        public static bool IsBroken(Tilemap map, Vector3Int cell)
        {
            if (!TryGet(Shown(map, cell), out var p)) return false;
            bool Bad(string mine, Vector3Int dir)
            {
                if (string.IsNullOrEmpty(mine)) return false;
                var theirs = NeighbourEdge(map, cell, dir);
                return theirs != null && mine != theirs;
            }
            return Bad(p.north, Vector3Int.up) || Bad(p.south, Vector3Int.down) || Bad(p.east, Vector3Int.right) || Bad(p.west, Vector3Int.left);
        }

        // ---- drawing ----
        // Each redraw collects the cells to show into one temporary mesh per texture (template / red sheets) and
        // draws each mesh once, so a tilemap costs a few draw calls however many cells are visible, like the
        // Tilemap's own renderer. Only the reusable buffers, meshes and material are kept between redraws.

        /// <summary>Below this many screen pixels per cell the templates are unreadable, so nothing is drawn.</summary>
        const float MinCellPixels = 6f;

        class Batch
        {
            public readonly List<Vector3> vertices = new List<Vector3>();
            public readonly List<Vector2> uvs = new List<Vector2>();
            public readonly List<int> triangles = new List<int>();
            public Mesh mesh;
        }

        static readonly Dictionary<Texture2D, Batch> _batches = new Dictionary<Texture2D, Batch>();

        [InitializeOnLoadMethod]
        static void HookCleanup()
        {
            // the material and meshes aren't saved anywhere; free them before scripts reload
            AssemblyReloadEvents.beforeAssemblyReload += () =>
            {
                foreach (var b in _batches.Values) if (b.mesh != null) UnityEngine.Object.DestroyImmediate(b.mesh);
                _batches.Clear();
                if (S.material != null) UnityEngine.Object.DestroyImmediate(S.material);
            };
        }

        static Material SpriteMaterial()
        {
            if (S.material != null) return S.material;
            var shader = Shader.Find("Hidden/AtomicRealm/TemplateOverlay");
            if (shader == null)
            {
                Debug.LogWarning("Atomic Realm Template View: overlay shader 'Hidden/AtomicRealm/TemplateOverlay' not found, " +
                                 "using Unity's sprite shader instead (colours may look darker).");
                shader = Shader.Find("Sprites/Default");
            }
            S.material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            return S.material;
        }

        enum CellResult { NoTemplate, Drawn, Unreadable }

        // adds one cell's template (or red variant) to its texture's batch
        static CellResult AddCell(Tilemap map, Vector3Int cell, bool checkSeams, Vector3 cellSize)
        {
            if (!TryGet(Shown(map, cell), out var p) || p.template == null) return CellResult.NoTemplate;
            var sprite = checkSeams && p.broken != null && IsBroken(map, cell) ? p.broken : p.template;
            var tex = sprite.texture;
            Rect r;
            // a template sprite packed into a Sprite Atlas (e.g. a whole folder added to one) can't be read this
            // way: skip the cell instead of failing every redraw; the readout says so
            try { r = sprite.textureRect; }
            catch (Exception) { return CellResult.Unreadable; }
            if (tex == null || r.width <= 0f) return CellResult.Unreadable;
            if (!_batches.TryGetValue(tex, out var b)) _batches[tex] = b = new Batch();
            var o = map.CellToLocal(cell);
            float w = tex.width, h = tex.height;
            float u0 = r.xMin / w, u1 = r.xMax / w, v0 = r.yMin / h, v1 = r.yMax / h;
            int i = b.vertices.Count;
            b.vertices.Add(o); b.vertices.Add(o + new Vector3(cellSize.x, 0));
            b.vertices.Add(o + new Vector3(cellSize.x, cellSize.y)); b.vertices.Add(o + new Vector3(0, cellSize.y));
            b.uvs.Add(new Vector2(u0, v0)); b.uvs.Add(new Vector2(u1, v0)); b.uvs.Add(new Vector2(u1, v1)); b.uvs.Add(new Vector2(u0, v1));
            b.triangles.Add(i); b.triangles.Add(i + 2); b.triangles.Add(i + 1);
            b.triangles.Add(i); b.triangles.Add(i + 3); b.triangles.Add(i + 2);
            return CellResult.Drawn;
        }

        static void ClearBatches()
        {
            foreach (var b in _batches.Values) { b.vertices.Clear(); b.uvs.Clear(); b.triangles.Clear(); }
        }

        // draws everything collected for one tilemap (in its local space), then empties the batches;
        // `toGamma`: convert colours for display ourselves (Scene view GUI in a Linear colour space project)
        static void Flush(Tilemap map, bool toGamma)
        {
            var mat = SpriteMaterial();
            mat.SetFloat("_ToGamma", toGamma ? 1f : 0f);
            foreach (var kv in _batches)
            {
                var b = kv.Value;
                if (b.vertices.Count == 0) continue;
                if (b.mesh == null) b.mesh = new Mesh { hideFlags = HideFlags.HideAndDontSave, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                b.mesh.Clear();
                b.mesh.SetVertices(b.vertices); b.mesh.SetUVs(0, b.uvs); b.mesh.SetTriangles(b.triangles, 0);
                mat.mainTexture = kv.Key;
                mat.SetPass(0);
                Graphics.DrawMeshNow(b.mesh, map.transform.localToWorldMatrix);
            }
            ClearBatches();
        }

        // Collects and draws one tilemap's overlay. Anything going wrong with one tilemap is reported once per
        // session and skipped, so it can't flood the console, hide the other tilemaps' overlays or leave stray cells.
        static void DrawMap(Tilemap map, IEnumerable<Vector3Int> cells, Func<Vector3Int, bool> checkSeams, bool toGamma, Stats stats)
        {
            if (map.layoutGrid == null) return; // a tilemap outside a Grid can't place its cells
            try
            {
                var size = map.layoutGrid.cellSize;
                foreach (var cell in cells)
                {
                    var result = AddCell(map, cell, checkSeams(cell), size);
                    if (stats == null) continue;
                    stats.tiles++;
                    if (result == CellResult.Drawn) stats.atomicRealm++;
                    else if (result == CellResult.Unreadable) { stats.atomicRealm++; stats.unreadable++; }
                    else if (IsAtomicRealmTile(map, cell)) { stats.atomicRealm++; stats.withoutTemplate++; }
                }
                Flush(map, toGamma);
            }
            catch (Exception e)
            {
                ClearBatches();
                if (stats != null) stats.failed = true;
                if (!S.warnedDrawError)
                {
                    S.warnedDrawError = true;
                    Debug.LogWarning($"Atomic Realm Template View: skipped tilemap '{map.name}' ({e.Message}).");
                }
            }
        }

        static bool IsAtomicRealmTile(Tilemap map, Vector3Int cell)
        {
            var t = map.HasEditorPreviewTile(cell) ? map.GetEditorPreviewTile(cell) : map.GetTile(cell);
            if (t is RuleOverrideTile o) t = o.m_Tile;
            return t is TerrainRuleTile || t is TerrainPieceTile;
        }

        // ---- readout numbers, per Scene view (shown next to the Template View button) ----

        /// <summary>What the overlay found in a Scene view's last redraw.</summary>
        public class Stats
        {
            /// <summary>Tiles in view.</summary>
            public int tiles;
            /// <summary>Atomic Realm tiles in view with no template.</summary>
            public int withoutTemplate;
            /// <summary>Templates that couldn't be read (e.g. packed in a Sprite Atlas).</summary>
            public int unreadable;
            /// <summary>Atomic Realm tiles in view (with or without a template).</summary>
            public int atomicRealm;
            /// <summary>Drawing some tilemap's overlay failed (details in the console).</summary>
            public bool failed;
            /// <summary>Some tilemap was too small on screen to draw.</summary>
            public bool zoomedOut;
            /// <summary>The selected tilemap(s) the overlay was limited to, or null when all tilemaps were drawn.</summary>
            public string layer;
        }

        // The tilemaps selected in the Hierarchy: a selected Grid (or any other parent) stands for the tilemaps below it,
        // an object inside a tilemap for that tilemap. Null when the selection holds none: then all are overlaid.
        static List<Tilemap> SelectedMaps()
        {
            var maps = new HashSet<Tilemap>();
            var stage = StageUtility.GetCurrentStageHandle();
            foreach (var go in Selection.gameObjects)
            {
                if (StageUtility.GetStageHandle(go) != stage) continue;
                var own = go.GetComponentInParent<Tilemap>();
                if (own != null) maps.Add(own);
                else maps.UnionWith(go.GetComponentsInChildren<Tilemap>());
            }
            maps.RemoveWhere(m => m.GetComponentInParent<PaletteLabels>() != null);
            return maps.Count > 0 ? new List<Tilemap>(maps) : null;
        }

        static readonly Dictionary<SceneView, Stats> _stats = new Dictionary<SceneView, Stats>();

        /// <summary>The numbers from the given Scene view's last redraw, or null when it hasn't drawn the overlay.</summary>
        public static Stats StatsFor(SceneView view) => view != null && _stats.TryGetValue(view, out var s) ? s : null;

        // Drawn after the Scene view has rendered, so it doesn't depend on the Gizmos toggle.
        static void OnSceneGUI(SceneView view)
        {
            if (!S.on || Event.current.type != EventType.Repaint) return;
            var cam = view.camera;
            var stats = new Stats();
            // Scene view GUI is drawn without converting colours for display; in a Linear colour space project our
            // sprites are sampled as linear colour, so the overlay shader converts them itself (else they look too dark)
            bool toGamma = QualitySettings.activeColorSpace == ColorSpace.Linear;
            // Only the selected tilemaps get the overlay, so other layers (backgrounds, decoration) and objects in front
            // stay visible. Without a selected tilemap: all of them. Back to front either way.
            var maps = SelectedMaps();
            if (maps != null) stats.layer = maps.Count == 1 ? maps[0].name : $"{maps.Count} tilemaps";
            else maps = new List<Tilemap>(StageUtility.GetCurrentStageHandle().FindComponentsOfType<Tilemap>());
            maps.Sort((a, b) => Order(a).CompareTo(Order(b)));
            foreach (var map in maps)
            {
                if (!IsShown(map, cam) || map.layoutGrid == null || map.GetComponentInParent<PaletteLabels>() != null) continue;
                if (CellPixels(map, cam, map.layoutGrid.cellSize) < MinCellPixels) { stats.zoomedOut = true; continue; }
                DrawMap(map, VisibleCells(map, cam), _ => true, toGamma, stats);
            }
            // forget closed Scene views
            foreach (var gone in new List<SceneView>(_stats.Keys)) if (gone == null) _stats.Remove(gone);
            _stats[view] = stats;
        }

        // how many screen pixels one cell covers (orthographic Scene view); perspective views always draw
        static float CellPixels(Tilemap map, Camera cam, Vector3 cellSize)
        {
            if (cam == null || !cam.orthographic || cam.orthographicSize <= 0f) return float.MaxValue;
            float worldCell = cellSize.y * Mathf.Abs(map.transform.lossyScale.y);
            return worldCell * cam.pixelHeight / (2f * cam.orthographicSize);
        }

        static long Order(Tilemap map)
        {
            return map.TryGetComponent(out TilemapRenderer r) ? (long)SortingLayer.GetLayerValueFromID(r.sortingLayerID) * 100000 + r.sortingOrder : 0;
        }

        // only tilemaps the Scene view actually shows: renderer on, not hidden with the Hierarchy eye, layer visible
        static bool IsShown(Tilemap map, Camera cam)
        {
            if (!map.isActiveAndEnabled) return false;
            if (!map.TryGetComponent(out TilemapRenderer r) || !r.enabled) return false;
            if (SceneVisibilityManager.instance.IsHidden(map.gameObject)) return false;
            return cam == null || (cam.cullingMask & (1 << map.gameObject.layer)) != 0;
        }

        // the tilemap's cells (painted or previewed) inside the camera view: the view's four corners are projected
        // onto the tilemap's plane, so rotated tilemaps and angled or perspective views are covered too
        static IEnumerable<Vector3Int> VisibleCells(Tilemap map, Camera cam)
        {
            var b = map.cellBounds;
            if (map.editorPreviewSize.x > 0 && map.editorPreviewSize.y > 0) // brush preview, e.g. outside the painted area
            {
                var pMin = map.editorPreviewOrigin; var pMax = pMin + map.editorPreviewSize;
                if (b.size.x <= 0 || b.size.y <= 0) b = new BoundsInt(pMin, map.editorPreviewSize);
                else
                {
                    var min = Vector3Int.Min(b.min, pMin); var max = Vector3Int.Max(b.max, pMax);
                    b = new BoundsInt(min, max - min);
                }
            }
            if (b.size.x <= 0 || b.size.y <= 0) yield break;
            if (cam != null)
            {
                var plane = new Plane(map.transform.forward, map.transform.position);
                int x0 = int.MaxValue, y0 = int.MaxValue, x1 = int.MinValue, y1 = int.MinValue;
                bool all = true;
                foreach (var v in new[] { new Vector3(0, 0), new Vector3(1, 0), new Vector3(0, 1), new Vector3(1, 1) })
                {
                    var ray = cam.ViewportPointToRay(v);
                    // a corner that misses the plane (looking past the horizon): keep the whole tilemap
                    if (!plane.Raycast(ray, out float d)) { all = false; break; }
                    var c = map.WorldToCell(ray.GetPoint(d));
                    // clamp to the tilemap first: near the horizon a corner lands very far away, and adding the
                    // 1-cell margin below to an extreme value would overflow
                    int cx = Mathf.Clamp(c.x, b.xMin - 2, b.xMax + 1), cy = Mathf.Clamp(c.y, b.yMin - 2, b.yMax + 1);
                    x0 = Mathf.Min(x0, cx); y0 = Mathf.Min(y0, cy); x1 = Mathf.Max(x1, cx); y1 = Mathf.Max(y1, cy);
                }
                if (all)
                {
                    x0 = Mathf.Max(b.xMin, x0 - 1); x1 = Mathf.Min(b.xMax - 1, x1 + 1);
                    y0 = Mathf.Max(b.yMin, y0 - 1); y1 = Mathf.Min(b.yMax - 1, y1 + 1);
                    b = new BoundsInt(x0, y0, b.zMin, Mathf.Max(0, x1 - x0 + 1), Mathf.Max(0, y1 - y0 + 1), b.size.z);
                }
            }
            foreach (var cell in b.allPositionsWithin)
                if (map.HasTile(cell) || map.HasEditorPreviewTile(cell)) yield return cell;
        }

        /// <summary>
        /// The overlay for a Tile Palette's tilemap; drawn by <see cref="PaletteLabels"/> from its gizmos, which
        /// passes the cells that belong to sheet blocks (shown without the red check).
        /// </summary>
        internal static void DrawPalette(Tilemap map, ICollection<Vector3Int> sheetCells)
        {
            if (!S.on || Event.current == null || Event.current.type != EventType.Repaint) return;
            var cells = new List<Vector3Int>();
            foreach (var cell in map.cellBounds.allPositionsWithin)
            {
                var tile = map.GetTile(cell);
                if (tile == null) continue;
                // AUTO brushes (also as override brushes) keep their palette icon
                if ((tile is RuleOverrideTile o ? o.m_InstanceTile : tile) is TerrainRuleTile rt && rt.paletteIcon != null) continue;
                cells.Add(cell);
            }
            // drawn in the palette's gizmo pass, which converts colours already
            DrawMap(map, cells, cell => !sheetCells.Contains(cell), false, null);
        }
#else
        public static bool On => false;
#endif
    }
}
