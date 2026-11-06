using UnityEngine;
using UnityEngine.Tilemaps;

namespace AtomicRealm.WorldBuilder
{
    /// <summary>
    /// A <see cref="TerrainPieceTile"/> that plays a frame animation (e.g. a wall piece with flickering lights).
    /// Its side tags work exactly like the static piece's, so autotiled neighbours join it the same way.
    /// The Tile's own sprite is shown where tiles don't animate (e.g. the Tile Palette) and should be frame 0.
    /// </summary>
    [CreateAssetMenu(fileName = "New Animated Terrain Piece Tile", menuName = "2D/Tiles/Atomic Realm/Animated Terrain Piece Tile")]
    public class AnimatedTerrainPieceTile : TerrainPieceTile
    {
        [Tooltip("Animation frames, played in order and looped.")]
        public Sprite[] frames;

        [Tooltip("Playback speed in frames per second (multiplied by the Tilemap's Animation Frame Rate).")]
        public float framesPerSecond = 8f;

        public override bool GetTileAnimationData(Vector3Int position, ITilemap tilemap, ref TileAnimationData tileAnimationData)
        {
            if (frames == null || frames.Length == 0) return false;
            tileAnimationData.animatedSprites = frames;
            tileAnimationData.animationSpeed = framesPerSecond;
            tileAnimationData.animationStartTime = 0f;
            return true;
        }
    }
}
