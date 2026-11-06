using UnityEngine;
using UnityEngine.Tilemaps;

namespace AtomicRealm.WorldBuilder
{
    /// <summary>
    /// An AUTO brush for drawing a frame as a line (e.g. the foreground debris frame): paint the frame's outline and
    /// each cell shows the matching 9-slice piece. A corner follows from the two directions the line takes there.
    /// A straight cell can't tell its side on its own, so it follows its stretch to where the line turns: a
    /// horizontal stretch that turns down is a top edge, one that turns up a bottom edge (vertical stretches: turns
    /// right = left edge, turns left = right edge). Without a turn, or with both ends disagreeing, it's a top /
    /// left edge. Joins only itself and hand-placed pieces from its own sheet.
    /// </summary>
    [CreateAssetMenu(fileName = "New Frame Outline Tile", menuName = "2D/Tiles/Atomic Realm/Frame Outline Tile")]
    public class FrameOutlineTile : TerrainRuleTile
    {
        public Sprite topLeft, top, topRight, left, right, bottomLeft, bottom, bottomRight;

        const int MaxRun = 512;                          // how far a stretch is followed

        bool Connected(TileBase t)
        {
            if (t == null) return false;
            if (t == this) return true;
            // a hand-placed piece from the same sheet is part of the line
            return t is TerrainPieceTile piece && piece.sprite != null && top != null && piece.sprite.texture == top.texture;
        }

        bool At(ITilemap map, Vector3Int p) => Connected(map.GetTile(p));

        // +1: the line turns towards `inside` at an end of the stretch through p along `axis`; -1: it turns away;
        // 0: no turn, or the two ends disagree
        int Side(ITilemap map, Vector3Int p, Vector3Int axis, Vector3Int inside)
        {
            int votes = 0;
            foreach (var dir in new[] { axis, -axis })
            {
                var end = p;
                for (int i = 0; i < MaxRun && At(map, end + dir); i++) end += dir;
                bool toward = At(map, end + inside), away = At(map, end - inside);
                if (toward != away) votes += toward ? 1 : -1;
            }
            return votes > 0 ? 1 : votes < 0 ? -1 : 0;
        }

        Sprite Resolve(Vector3Int p, ITilemap map)
        {
            bool l = At(map, p + Vector3Int.left), r = At(map, p + Vector3Int.right);
            bool u = At(map, p + Vector3Int.up), d = At(map, p + Vector3Int.down);
            bool horizontal = l || r, vertical = u || d;
            if (horizontal && vertical)
            {
                // a corner, or a T-junction / crossing: the edge facing away from the extra branch
                if (l && r) return d && !u ? top : u && !d ? bottom : top;
                if (u && d) return r ? left : right;
                if (r) return d ? topLeft : bottomLeft;
                return d ? topRight : bottomRight;
            }
            if (horizontal) return Side(map, p, Vector3Int.right, Vector3Int.down) < 0 ? bottom : top;
            if (vertical) return Side(map, p, Vector3Int.up, Vector3Int.right) < 0 ? right : left;
            return top;                                   // a single cell
        }

        public override void GetTileData(Vector3Int position, ITilemap tilemap, ref TileData tileData)
        {
            base.GetTileData(position, tilemap, ref tileData);  // flags, colliders, the palette icon
            if (paletteIcon == null || tileData.sprite != paletteIcon) tileData.sprite = Resolve(position, tilemap);
        }

        // A cell's piece depends on the ends of its stretches, so refresh the whole stretches through it and its
        // neighbours.
        static readonly Vector3Int[] Sides = { Vector3Int.left, Vector3Int.right, Vector3Int.up, Vector3Int.down };
        static readonly System.Collections.Generic.HashSet<Vector3Int> Refreshed = new System.Collections.Generic.HashSet<Vector3Int>();

        public override void RefreshTile(Vector3Int position, ITilemap tilemap)
        {
            // each cell once: the stretches of neighbouring cells overlap
            Refreshed.Clear();
            void Refresh(Vector3Int q) { if (Refreshed.Add(q)) tilemap.RefreshTile(q); }
            Refresh(position);
            foreach (var n in Sides)
            {
                var start = position + n;
                Refresh(start);
                if (!At(tilemap, start)) continue;
                foreach (var dir in Sides)
                {
                    var q = start;
                    for (int i = 0; i < MaxRun && At(tilemap, q + dir); i++) { q += dir; Refresh(q); }
                }
            }
#if UNITY_EDITOR
            // A large refresh can show in the Scene view only on its next redraw (in the editor: the next mouse move),
            // leaving the layer blank meanwhile: ask for one more redraw right after the edit.
            if (Refreshed.Count > 9 && !Repaint.queued)
            {
                Repaint.queued = true;
                UnityEditor.EditorApplication.delayCall += () => { Repaint.queued = false; UnityEditor.SceneView.RepaintAll(); };
            }
#endif
        }

#if UNITY_EDITOR
        // in an object, not a static field, which the Asset Store validator flags (see TemplateView)
        class RepaintState { public bool queued; }
        static readonly RepaintState Repaint = new RepaintState();
#endif
    }
}
