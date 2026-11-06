using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace AtomicRealm.WorldBuilder
{
    /// <summary>
    /// Editor-only helper for a Tile Palette prefab: draws a red line on every tile side whose art
    /// continues (no border drawn) but has no neighbour in the palette, i.e. an unfinished side.
    /// Visible in the Tile Palette window when its Gizmos toggle is on. Does nothing in builds.
    /// </summary>
    public class PaletteOpenSides : MonoBehaviour
    {
        // bit flags: which sides / corners each tile's art expects to be connected
        public const int N = 1, S = 2, E = 4, W = 8, NW = 16, NE = 32, SW = 64, SE = 128;

        [System.Serializable]
        public struct Entry
        {
            public TileBase tile;
            public int connected;
        }

        public List<Entry> tiles = new List<Entry>();
        public Color color = new Color(1f, 0.15f, 0.15f, 1f);
        [Range(1f, 10f)] public float lineWidth = 5f;

#if UNITY_EDITOR
        readonly Dictionary<TileBase, int> _lookup = new Dictionary<TileBase, int>();

        void OnDrawGizmos()
        {
            // rebuilt each draw (a few dozen entries), so edits from the Inspector or from scripts show at once
            _lookup.Clear();
            foreach (var e in tiles) if (e.tile != null) _lookup[e.tile] = e.connected;

            var prevColor = Handles.color;
            Handles.color = color;
            foreach (var map in GetComponentsInChildren<Tilemap>())
            {
                foreach (var p in map.cellBounds.allPositionsWithin)
                {
                    var tile = map.GetTile(p);
                    if (tile == null || !_lookup.TryGetValue(tile, out int c)) continue;

                    bool Has(int dx, int dy) => map.GetTile(p + new Vector3Int(dx, dy, 0)) != null;
                    Vector3 At(float x, float y) => map.CellToWorld(p) + Vector3.Scale(map.layoutGrid.cellSize, new Vector3(x, y, 0));

                    // lines sit exactly on the tile's edge
                    if ((c & N) != 0 && !Has(0, 1)) Line(At(0, 1), At(1, 1));
                    if ((c & S) != 0 && !Has(0, -1)) Line(At(0, 0), At(1, 0));
                    if ((c & E) != 0 && !Has(1, 0)) Line(At(1, 0), At(1, 1));
                    if ((c & W) != 0 && !Has(-1, 0)) Line(At(0, 0), At(0, 1));

                    // corners: both sides connected, but the diagonal neighbour the art expects is missing
                    const float k = 0.22f;
                    if ((c & NW) != 0 && Has(0, 1) && Has(-1, 0) && !Has(-1, 1)) Corner(At(0, 1), At(k, 1), At(k, 1 - k), At(0, 1 - k));
                    if ((c & NE) != 0 && Has(0, 1) && Has(1, 0) && !Has(1, 1)) Corner(At(1, 1), At(1 - k, 1), At(1 - k, 1 - k), At(1, 1 - k));
                    if ((c & SW) != 0 && Has(0, -1) && Has(-1, 0) && !Has(-1, -1)) Corner(At(0, 0), At(k, 0), At(k, k), At(0, k));
                    if ((c & SE) != 0 && Has(0, -1) && Has(1, 0) && !Has(1, -1)) Corner(At(1, 0), At(1 - k, 0), At(1 - k, k), At(1, k));
                }
            }
            Handles.color = prevColor;
        }

        void Line(Vector3 a, Vector3 b) => Handles.DrawAAPolyLine(lineWidth, a, b);

        void Corner(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            var fill = color; fill.a = 0.6f;
            var prev = Handles.color;
            Handles.color = Color.white; // Handles tints filled shapes by Handles.color
            Handles.DrawSolidRectangleWithOutline(new[] { a, b, c, d }, fill, color);
            Handles.color = prev;
        }
#endif
    }
}
