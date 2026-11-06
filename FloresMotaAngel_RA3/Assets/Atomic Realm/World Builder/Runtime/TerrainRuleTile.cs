using UnityEngine;
using UnityEngine.Tilemaps;

namespace AtomicRealm.WorldBuilder
{
    /// <summary>
    /// What a tile shows on one of its sides or corners, in the pack templates' terms:
    /// Solid = the magenta layer (ground, walls, blocks); Frame = the yellow layer (poles, beams, girders, supports);
    /// FrameWide = a wide frame girder side (thin beams are plain Frame). Values are serialized as numbers.
    /// </summary>
    public enum SideMaterial { None = 0, Solid = 1, Frame = 2, FrameWide = 3 }

    /// <summary>
    /// A Rule Tile for one material (Solid or Frame). A neighbour counts as "This" when the side it turns towards
    /// this cell is of the same material: autotiled cells of that material, or plain <see cref="TerrainPieceTile"/>s
    /// whose facing side is. So hand-placed pieces and connector pieces (solid on one side, frame on another)
    /// join correctly. Rules are edited in the Inspector like a regular Rule Tile, but only rules with the
    /// Fixed transform know which side a neighbour is on; Rotated / Mirror rules can't tell sides apart.
    /// Rule Override Tiles of this tile (e.g. a recoloured variant) join like the original.
    /// </summary>
    [CreateAssetMenu(fileName = "New Terrain Rule Tile", menuName = "2D/Tiles/Atomic Realm/Terrain Rule Tile")]
    public class TerrainRuleTile : RuleTile
    {
        /// <summary>
        /// Neighbour conditions usable in rules: Unity's This = 1 / NotThis = 2 plus ours. Kept in a nested class
        /// (Unity's pattern for custom Rule Tiles) so the Inspector lists them by name and Rule Override Tiles,
        /// which copy the tile's public fields, don't trip over constants.
        /// </summary>
        public class Neighbor : TilingRuleOutput.Neighbor
        {
            /// <summary>The neighbour turns a frame side (thin or wide) towards this cell.</summary>
            public const int Frame = 3;
            /// <summary>The neighbour is a frame cell (frame brush, or a piece with any frame side).</summary>
            public const int AnyFrame = 4;
            /// <summary>The neighbour is not a frame cell.</summary>
            public const int NotAnyFrame = 5;
            /// <summary>The neighbour turns a wide frame girder side towards this cell.</summary>
            public const int Wide = 6;
            /// <summary>The neighbour is solid (solid brush, or a piece turning a solid side towards this cell).</summary>
            public const int SolidCell = 7;
            /// <summary>The neighbour is painted with a frame brush (hand-placed pieces don't count).</summary>
            public const int FrameBrush = 8;
            /// <summary>The neighbour joins this cell (like This) and is solid: a frame beam running into ground.</summary>
            public const int JoinsSolid = 9;
            /// <summary>The neighbour joins this cell (like This) and turns a wide frame girder side towards it.</summary>
            public const int JoinsWide = 10;
        }

        public override System.Type m_NeighborType => typeof(Neighbor);

        // [DontOverride]: a Rule Override Tile (e.g. a recoloured variant) copies these from the tile it overrides,
        // instead of starting from the defaults, so an override of the Frame brush still paints frame
        [Tooltip("Material this brush paints. Neighbours join when the side they turn towards this cell is of this material.")]
        [DontOverride] public SideMaterial material = SideMaterial.Solid;

        [Tooltip("Shown instead of the resolved sprite when this tile sits in a Tile Palette (a prefab with PaletteLabels), " +
                 "so the autotiling brush is easy to spot. Never used in scenes.")]
        public Sprite paletteIcon;

        [Tooltip("Mark this tile in the palette with the yellow AUTO frame. Turn off for a rule tile that should not look like a main brush there.")]
        [DontOverride] public bool autoBrush = true;

        static bool IsFrame(SideMaterial m) => m == SideMaterial.Frame || m == SideMaterial.FrameWide;

        // a Rule Override Tile paints through a hidden instance of the tile it overrides
        static TileBase Unwrap(TileBase t) => t is RuleOverrideTile o && o.m_InstanceTile != null ? o.m_InstanceTile : t;

        /// <summary>The material `other` shows towards a cell that sees it at `offset`.</summary>
        public static SideMaterial Facing(TileBase other, Vector3Int offset)
        {
            other = Unwrap(other);
            if (other is TerrainRuleTile rt) return rt.material;
            if (other is TerrainPieceTile piece) return piece.Facing(offset);
            return SideMaterial.None;
        }

        static bool IsFrameCell(TileBase t)
        {
            t = Unwrap(t);
            return t is TerrainRuleTile rt ? IsFrame(rt.material) :
                t is TerrainPieceTile p && (IsFrame(p.north) || IsFrame(p.south) || IsFrame(p.east) || IsFrame(p.west));
        }

        static bool IsSolidBrush(TileBase t) => Unwrap(t) is TerrainRuleTile rt && rt.material == SideMaterial.Solid;

        static bool IsFrameBrush(TileBase t) => Unwrap(t) is TerrainRuleTile rt && IsFrame(rt.material);

        static bool IsSolidCell(TileBase t, Vector3Int offset) => Facing(t, offset) == SideMaterial.Solid;

        // An autotiled cell painted on a piece with a cap (TerrainPieceTile.capAbove) shows that cap: sky, as far as
        // its neighbours are concerned.
        static Sprite CapAt(Vector3Int position, ITilemap tilemap) =>
            Unwrap(tilemap.GetTile(position + Vector3Int.down)) is TerrainPieceTile piece ? piece.capAbove : null;

        bool Joins(TileBase other, Vector3Int offset, Vector3Int position, ITilemap tilemap)
        {
            other = Unwrap(other);
            if (other == null) return false;
            if (tilemap != null && other is TerrainRuleTile && CapAt(position + offset, tilemap) != null) return false;
            if (other == this) return true;
            var m = Facing(other, offset);
            if (m != SideMaterial.None && (m == material || (IsFrame(material) && IsFrame(m)))) return true;
            // Frame plugs into autotiled solid only where that solid cell can turn into a connector piece:
            // sideways when nothing solid sits on top of it; from above only for a pole (no frame to the left or
            // right, so a beam lying on the ground stays a beam) standing on solid with solid on both sides.
            if (IsFrame(material) && IsSolidBrush(other) && tilemap != null)
            {
                var at = position + offset;
                if (offset.y == 0 && (offset.x == 1 || offset.x == -1))
                    return !IsSolidCell(tilemap.GetTile(at + Vector3Int.up), Vector3Int.up);
                if (offset == Vector3Int.down)
                    return !IsFrameCell(tilemap.GetTile(position + Vector3Int.left)) && !IsFrameCell(tilemap.GetTile(position + Vector3Int.right))
                        && IsSolidCell(tilemap.GetTile(at + Vector3Int.left), Vector3Int.left) && IsSolidCell(tilemap.GetTile(at + Vector3Int.right), Vector3Int.right);
            }
            return false;
        }

        // Does `other` (seen at `offset`) turn a frame side towards this cell? A painted frame cell is asked the
        // same question the other way round, so ground becomes a connector only where the frame plugs into it.
        bool FrameFaces(TileBase other, Vector3Int offset, Vector3Int position, ITilemap tilemap)
        {
            if (material == SideMaterial.Solid && Unwrap(other) is TerrainRuleTile fb && IsFrame(fb.material))
                return fb.Joins(this, -offset, position + offset, tilemap);
            return IsFrame(Facing(other, offset));
        }

        // Rotated / Mirror rules: Unity doesn't pass the neighbour's direction here, so sides can't be told apart.
        public override bool RuleMatch(int neighbor, TileBase other)
        {
            switch (neighbor)
            {
                case Neighbor.This: return Joins(other, Vector3Int.zero, Vector3Int.zero, null);
                case Neighbor.NotThis: return !Joins(other, Vector3Int.zero, Vector3Int.zero, null);
                case Neighbor.Frame: return IsFrame(Facing(other, Vector3Int.zero));
                case Neighbor.AnyFrame: return IsFrameCell(other);
                case Neighbor.NotAnyFrame: return !IsFrameCell(other);
                case Neighbor.Wide: return Facing(other, Vector3Int.zero) == SideMaterial.FrameWide;
                case Neighbor.SolidCell: return IsSolidCell(other, Vector3Int.zero);
                case Neighbor.FrameBrush: return IsFrameBrush(other);
                case Neighbor.JoinsSolid: return IsSolidCell(other, Vector3Int.zero);
                case Neighbor.JoinsWide: return Facing(other, Vector3Int.zero) == SideMaterial.FrameWide;
            }
            return base.RuleMatch(neighbor, other);
        }

        // Fixed rules are matched here so the neighbour's direction and surroundings are known.
        public override bool RuleMatches(TilingRule rule, Vector3Int position, ITilemap tilemap, ref Matrix4x4 transform)
        {
            if (rule.m_RuleTransform != TilingRuleOutput.Transform.Fixed)
                return base.RuleMatches(rule, position, tilemap, ref transform);

            int count = Mathf.Min(rule.m_Neighbors.Count, rule.m_NeighborPositions.Count);
            for (int i = 0; i < count; i++)
            {
                var offset = rule.m_NeighborPositions[i];
                var other = tilemap.GetTile(GetOffsetPosition(position, offset));
                bool ok;
                switch (rule.m_Neighbors[i])
                {
                    case Neighbor.This: ok = Joins(other, offset, position, tilemap); break;
                    case Neighbor.NotThis: ok = !Joins(other, offset, position, tilemap); break;
                    case Neighbor.Frame: ok = FrameFaces(other, offset, position, tilemap); break;
                    case Neighbor.AnyFrame: ok = IsFrameCell(other); break;
                    case Neighbor.NotAnyFrame: ok = !IsFrameCell(other); break;
                    case Neighbor.Wide: ok = Facing(other, offset) == SideMaterial.FrameWide; break;
                    case Neighbor.SolidCell: ok = IsSolidCell(other, offset); break;
                    case Neighbor.FrameBrush: ok = IsFrameBrush(other); break;
                    case Neighbor.JoinsSolid: ok = IsSolidCell(other, offset) && Joins(other, offset, position, tilemap); break;
                    case Neighbor.JoinsWide: ok = Facing(other, offset) == SideMaterial.FrameWide && Joins(other, offset, position, tilemap); break;
                    default: ok = base.RuleMatch(rule.m_Neighbors[i], other); break;
                }
                if (!ok) return false;
            }
            transform = Matrix4x4.identity;
            return true;
        }

        public override void GetTileData(Vector3Int position, ITilemap tilemap, ref TileData tileData)
        {
            base.GetTileData(position, tilemap, ref tileData);
            var cap = CapAt(position, tilemap);
            if (cap != null) tileData.sprite = cap;
#if UNITY_EDITOR
            // palettes only exist in the editor
            if (paletteIcon != null && IsPalette(tilemap)) tileData.sprite = paletteIcon;
#endif
        }

        // Frame rules look up to two cells away, so refresh that far when a frame cell changes.
        public override void RefreshTile(Vector3Int position, ITilemap tilemap)
        {
            base.RefreshTile(position, tilemap);
            if (!IsFrame(material)) return;
            for (int y = -2; y <= 2; y++)
                for (int x = -2; x <= 2; x++)
                    if (x != 0 || y != 0) tilemap.RefreshTile(position + new Vector3Int(x, y, 0));
        }

#if UNITY_EDITOR
        static bool IsPalette(ITilemap tilemap)
        {
            var map = tilemap.GetComponent<Tilemap>();
            // the Tile Palette window shows its palette in a preview scene; scene tilemaps can skip the parent walk
            if (map == null || !UnityEditor.SceneManagement.EditorSceneManager.IsPreviewScene(map.gameObject.scene)) return false;
            return map.GetComponentInParent<PaletteLabels>() != null;
        }
#endif
    }
}
