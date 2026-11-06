using UnityEngine;
using UnityEngine.Tilemaps;

namespace AtomicRealm.WorldBuilder
{
    /// <summary>
    /// A plain (fixed-sprite) tile that knows what each of its sides and corners shows (solid, frame or nothing),
    /// so <see cref="TerrainRuleTile"/> neighbours join it only on matching sides. Refreshes the cells around it when
    /// placed or removed, so they re-resolve immediately.
    /// </summary>
    [CreateAssetMenu(fileName = "New Terrain Piece Tile", menuName = "2D/Tiles/Atomic Realm/Terrain Piece Tile")]
    public class TerrainPieceTile : Tile
    {
        public SideMaterial north, south, east, west;
        public SideMaterial northWest, northEast, southWest, southEast;

        [Tooltip("Shown by a Terrain Rule Tile (AUTO brush) cell painted directly on top of this piece, instead of what " +
                 "its rules pick, e.g. the sky block holding this skyline piece's cliff tops. That cell then counts as " +
                 "empty for its neighbours. Empty: the cell above autotiles as usual.")]
        public Sprite capAbove;

        /// <summary>The side or corner this tile turns towards a cell that sees it at `offset` (asker to this tile).</summary>
        public SideMaterial Facing(Vector3Int offset)
        {
            int dx = offset.x, dy = offset.y;
            if (dy == 0) return dx > 0 ? west : east;
            if (dx == 0) return dy > 0 ? south : north;
            if (dx > 0) return dy > 0 ? southWest : northWest;
            return dy > 0 ? southEast : northEast;
        }

        // two cells out: frame rules look that far (e.g. "is my neighbour a T-junction?")
        public override void RefreshTile(Vector3Int position, ITilemap tilemap)
        {
            for (int y = -2; y <= 2; y++)
                for (int x = -2; x <= 2; x++)
                    tilemap.RefreshTile(position + new Vector3Int(x, y, 0));
        }
    }
}
