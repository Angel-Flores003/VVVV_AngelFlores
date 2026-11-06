using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
#endif

namespace AtomicRealm.WorldBuilder
{
    /// <summary>
    /// Editor-only helper for a Tile Palette prefab (visible when the Tile Palette's Gizmos toggle is on):
    ///  - frames every Rule Tile, writes "AUTO" above it and (optionally) its material (SOLID / FRAME) below it,
    ///  - captions every block of tiles laid out like a source sheet (e.g. "TILESET 1"), also when examples touch it,
    ///  - highlights every copy of the tile(s) currently selected or loaded in the brush.
    /// Everything is drawn in tile space and recomputed on each redraw, so it follows layout and zoom.
    /// Its presence also marks the prefab as a palette for <see cref="TerrainRuleTile.paletteIcon"/>.
    /// </summary>
    public class PaletteLabels : MonoBehaviour
    {
        [System.Serializable]
        public struct SheetTile
        {
            public TileBase tile;
            public string sheet;   // caption, e.g. "TILESET 1"
            public int col, row;   // position in the sheet, row 0 = top
        }

        public List<SheetTile> sheetTiles = new List<SheetTile>();
        [Tooltip("Write each AUTO brush's material (SOLID / FRAME) below it. Off for palettes with a single brush.")]
        public bool materialLabels = true;
        public Color color = new Color(1f, 0.82f, 0.2f, 1f);
        public Color highlight = new Color(1f, 0.85f, 0.1f, 0.35f);
        [Range(1f, 10f)] public float frameWidth = 4f;

#if UNITY_EDITOR
        // 3x5 pixel glyphs, top row first
        static readonly Dictionary<char, string[]> Font = new Dictionary<char, string[]>
        {
            ['A'] = new[] { ".#.", "#.#", "###", "#.#", "#.#" },
            ['U'] = new[] { "#.#", "#.#", "#.#", "#.#", "###" },
            ['T'] = new[] { "###", ".#.", ".#.", ".#.", ".#." },
            ['O'] = new[] { "###", "#.#", "#.#", "#.#", "###" },
            ['I'] = new[] { "###", ".#.", ".#.", ".#.", "###" },
            ['L'] = new[] { "#..", "#..", "#..", "#..", "###" },
            ['E'] = new[] { "###", "#..", "##.", "#..", "###" },
            ['S'] = new[] { "###", "#..", "###", "..#", "###" },
            ['1'] = new[] { ".#.", "##.", ".#.", ".#.", "###" },
            ['2'] = new[] { "##.", "..#", ".#.", "#..", "###" },
            ['3'] = new[] { "##.", "..#", ".#.", "..#", "##." },
            ['D'] = new[] { "##.", "#.#", "#.#", "#.#", "##." },
            ['F'] = new[] { "###", "#..", "##.", "#..", "#.." },
            ['R'] = new[] { "##.", "#.#", "##.", "#.#", "#.#" },
            ['M'] = new[] { "#.#", "###", "###", "#.#", "#.#" },
            ['N'] = new[] { "##.", "#.#", "#.#", "#.#", "#.#" },
            ['P'] = new[] { "##.", "#.#", "##.", "#..", "#.." },
            ['B'] = new[] { "##.", "#.#", "##.", "#.#", "##." },
            ['C'] = new[] { "###", "#..", "#..", "#..", "###" },
            ['G'] = new[] { "###", "#..", "#.#", "#.#", "###" },
            ['K'] = new[] { "#.#", "#.#", "##.", "#.#", "#.#" },
            ['H'] = new[] { "#.#", "#.#", "###", "#.#", "#.#" },
            ['W'] = new[] { "#.#", "#.#", "###", "###", "#.#" },
            [' '] = new[] { "...", "...", "...", "...", "..." },
        };
        const float Px = 1f / 16f; // one glyph pixel = 1/16 of a cell

        void OnDrawGizmos()
        {
            var selected = SelectedTiles();

            var prevColor = Handles.color;
            foreach (var map in GetComponentsInChildren<Tilemap>())
            {
                // which cells belong to sheet blocks, worked out once per redraw: they get a caption, and the
                // Template View overlay shows them without the red check
                var sheetCells = new HashSet<Vector3Int>();
                var captions = new Dictionary<Vector3Int, string>(); // sheet block top-left cell -> sheet name
                foreach (var cell in map.cellBounds.allPositionsWithin)
                    if (SheetBlockAt(map, cell, out var sheet, out var topLeft)) { sheetCells.Add(cell); captions[topLeft] = sheet; }

                TemplateView.DrawPalette(map, sheetCells); // under the labels and highlights
                foreach (var start in map.cellBounds.allPositionsWithin)
                {
                    var tile = map.GetTile(start);
                    if (tile == null) continue;

                    // highlight the other copies (not the selected palette cell itself); for a scene selection also
                    // the plain tiles showing the same piece the selected AUTO cell resolved to
                    // an override brush (e.g. a theme's recoloured brush) counts as the rule tile it wraps
                    var rule = tile is RuleOverrideTile o ? o.m_InstanceTile : tile as RuleTile;
                    bool same = selected.Contains(tile)
                                || (_selectedSprite != null && rule == null && map.GetSprite(start) == _selectedSprite);
                    if (same && !(map == _selectedMap && start == _selectedCell)) Cell(map, start, highlight);
                    if (rule != null && !(rule is TerrainRuleTile trt && !trt.autoBrush))
                    {
                        Handles.color = color;
                        Handles.DrawAAPolyLine(frameWidth, At(map, start, 0, 0), At(map, start, 1, 0), At(map, start, 1, 1), At(map, start, 0, 1), At(map, start, 0, 0));
                        float w = TextWidth("AUTO");
                        Text(map, start, 0.5f - w / 2f, 1f + 2 * Px, "AUTO");
                        // material name below the cell
                        if (materialLabels && rule is TerrainRuleTile brush && brush.material != SideMaterial.None)
                        {
                            string mat = brush.material == SideMaterial.Solid ? "SOLID" : "FRAME";
                            Text(map, start, 0.5f - TextWidth(mat) / 2f, -7 * Px, mat);
                        }
                    }

                }
                // caption each sheet block once, above its top-left cell
                foreach (var kv in captions) Text(map, kv.Key, 0f, 1f + 2 * Px, kv.Value);
            }
            Handles.color = prevColor;
        }

        // True when the cell belongs to one of the palette's sheet blocks: (nearly) the whole sheet is laid out
        // around it at the positions the sheet gives; also returns the sheet and the block's top-left cell. Sheet
        // blocks get a caption, and Template View doesn't check their seams (sheet neighbours aren't meant to connect).
        bool SheetBlockAt(Tilemap map, Vector3Int p, out string sheet, out Vector3Int topLeft)
        {
            sheet = null; topLeft = default;
            var tile = map.GetTile(p);
            if (tile == null) return false;
            foreach (var s in sheetTiles)
            {
                if (s.tile != tile) continue;
                var origin = new Vector3Int(p.x - s.col, p.y + s.row, 0);
                int total = 0, inPlace = 0;
                foreach (var o in sheetTiles)
                {
                    if (o.sheet != s.sheet) continue;
                    total++;
                    if (map.GetTile(origin + new Vector3Int(o.col, -o.row, 0)) == o.tile) inPlace++;
                }
                // most of the sheet in place; at least 4 tiles, so a short run of pieces that happens to be in sheet
                // order isn't a block, but a sheet with fewer tiles than that must be complete
                if (inPlace >= Mathf.Min(total, Mathf.Max(4, total * 0.8f))) { sheet = s.sheet; topLeft = origin; return true; }
            }
            return false;
        }

        // The tile in the active Grid Selection when that selection is a single tile cell, either in this palette
        // or in a scene tilemap. For a scene cell, the sprite it currently shows is kept too.
        Tilemap _selectedMap;
        Vector3Int _selectedCell;
        Sprite _selectedSprite;

        HashSet<TileBase> SelectedTiles()
        {
            var result = new HashSet<TileBase>();
            _selectedMap = null;
            _selectedSprite = null;
            // only while the Select tool is active: other tools leave a stale selection behind
            if (UnityEditor.EditorTools.ToolManager.activeToolType?.Name != "SelectTool") return result;
            try
            {
                var sel = FindType("UnityEditor.Tilemaps.GridSelection");
                if (sel != null && (bool)sel.GetProperty("active", BindingFlags.Public | BindingFlags.Static).GetValue(null))
                {
                    var target = sel.GetProperty("target", BindingFlags.Public | BindingFlags.Static).GetValue(null) as GameObject;
                    var bounds = (BoundsInt)sel.GetProperty("position", BindingFlags.Public | BindingFlags.Static).GetValue(null);
                    if (target != null && target.TryGetComponent(out Tilemap map))
                    {
                        bool inPalette = map.transform.IsChildOf(transform);
                        int cellsWithTiles = 0;
                        foreach (var p in bounds.allPositionsWithin)
                        {
                            var t = map.GetTile(p);
                            if (t == null) continue;
                            result.Add(t); cellsWithTiles++;
                            if (inPalette) { _selectedMap = map; _selectedCell = p; }
                            else _selectedSprite = map.GetSprite(p); // scene: also match the piece an AUTO tile is showing
                        }
                        if (cellsWithTiles > 1) { _selectedSprite = null; return new HashSet<TileBase>(); }
                    }
                }
                // the brush is ignored on purpose: it keeps its last tile even when nothing is selected
            }
            catch (Exception) { /* editor API changed: just don't highlight */ }
            // only highlight a single tile; a multi-tile selection highlights nothing
            return result.Count == 1 ? result : new HashSet<TileBase>();
        }

        // Repaint open Tile Palette windows whenever the grid selection changes (e.g. a cell picked in the Scene
        // view), so the highlight updates without hovering the palette. Hooked by reflection (editor package type).
        [InitializeOnLoadMethod]
        static void HookSelectionRepaint()
        {
            UnityEditor.EditorTools.ToolManager.activeToolChanged -= RepaintPalettes;
            UnityEditor.EditorTools.ToolManager.activeToolChanged += RepaintPalettes;
            try
            {
                var ev = FindType("UnityEditor.Tilemaps.GridSelection")?.GetEvent("gridSelectionChanged", BindingFlags.Public | BindingFlags.Static);
                if (ev == null) return;
                var handler = Delegate.CreateDelegate(ev.EventHandlerType, typeof(PaletteLabels).GetMethod(nameof(RepaintPalettes), BindingFlags.NonPublic | BindingFlags.Static));
                ev.RemoveEventHandler(null, handler);
                ev.AddEventHandler(null, handler);
            }
            catch (Exception) { /* editor API changed: palette updates on its next redraw instead */ }
        }

        static void RepaintPalettes()
        {
            foreach (var w in Resources.FindObjectsOfTypeAll<EditorWindow>())
                if (w.GetType().Name == "GridPaintPaletteWindow") w.Repaint();
        }

        static readonly Dictionary<string, Type> TypeCache = new Dictionary<string, Type>();
        static Type FindType(string name)
        {
            if (TypeCache.TryGetValue(name, out var t)) return t;
            t = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).FirstOrDefault(x => x != null);
            TypeCache[name] = t;
            return t;
        }

        static Vector3 At(Tilemap map, Vector3Int cell, float x, float y) =>
            map.CellToWorld(cell) + Vector3.Scale(map.layoutGrid.cellSize, new Vector3(x, y, 0));

        // Handles tints filled shapes by Handles.color, so reset it to white to draw the exact colours given
        static void Quad(Tilemap map, Vector3Int cell, float x0, float y0, float x1, float y1, Color fill, Color outline)
        {
            var prev = Handles.color;
            Handles.color = Color.white;
            Handles.DrawSolidRectangleWithOutline(new[] { At(map, cell, x0, y0), At(map, cell, x1, y0), At(map, cell, x1, y1), At(map, cell, x0, y1) }, fill, outline);
            Handles.color = prev;
        }

        void Cell(Tilemap map, Vector3Int cell, Color c) { var o = c; o.a = Mathf.Min(1f, c.a * 2.5f); Quad(map, cell, 0, 0, 1, 1, c, o); }

        static float TextWidth(string text) => (text.Length * 4 - 1) * Px;

        // pixel text with its bottom-left at (x, y) in cell units relative to `cell`
        void Text(Tilemap map, Vector3Int cell, float x, float y, string text)
        {
            var bg = new Color(0f, 0f, 0f, 0.7f);
            Quad(map, cell, x - Px, y - Px, x + TextWidth(text) + Px, y + 6 * Px, bg, bg);
            for (int i = 0; i < text.Length; i++)
            {
                if (!Font.TryGetValue(char.ToUpperInvariant(text[i]), out var g)) continue;
                for (int r = 0; r < 5; r++) for (int c = 0; c < 3; c++)
                {
                    if (g[r][c] != '#') continue;
                    float px = x + (i * 4 + c) * Px, py = y + (4 - r) * Px;
                    Quad(map, cell, px, py, px + Px, py + Px, color, color);
                }
            }
        }
#endif
    }
}
