using UnityEngine;
using UnityEngine.Tilemaps;

namespace AtomicRealm.WorldBuilder
{
    /// <summary>
    /// Moves a layer (e.g. a background Tilemap) a fraction of the camera's movement, so it looks farther away.
    /// Factor 1 = moves with the world (no parallax), 0 = stays with the camera; backgrounds use about 0.3-0.6,
    /// far backgrounds about 0-0.15, foreground layers in front of the player above 1. Runs in play mode only:
    /// in the editor the layer stays where it was painted, lined up as seen from the camera's start position (with
    /// a <see cref="CameraFollow2D"/>: centred on its target, e.g. the player's start).
    /// With "Repeat Horizontally" a second copy is made when play starts and the two leapfrog, so the layer never
    /// ends; its left and right edges must connect (as backgrounds from the Atomic Realm generator do).
    /// Positions are snapped to whole pixels, so the layer doesn't shimmer with a Pixel Perfect Camera.
    /// Don't use it on layers with colliders.
    /// </summary>
    [DisallowMultipleComponent]
    public class ParallaxLayer : MonoBehaviour
    {
        [Tooltip("The camera to follow. Empty: the main camera.")]
        public Camera targetCamera;

        [Tooltip("How much the layer moves with the world, per axis: 1 = like the level (no parallax), 0 = fixed to the camera.")]
        public Vector2 factor = new Vector2(0.5f, 0.5f);

        [Tooltip("Repeat the layer endlessly to the left and right (a second copy is made in play mode).")]
        public bool repeatHorizontally;

        [Tooltip("Width of one repeat in world units. 0: the Tilemap's painted width.")]
        public float repeatWidth;

        [Tooltip("Snap the position to whole pixels (match your sprites' Pixels Per Unit). 0: no snapping.")]
        public int pixelsPerUnit = 32;

        Vector3 _start, _cameraStart;
        float _width, _left;        // repeat width and the layer's left edge (world, relative to its position)
        Transform _copy;
        bool _isCopy;

        void Start()
        {
            if (_isCopy) return;
            if (targetCamera == null) targetCamera = Camera.main;
            if (targetCamera == null) { enabled = false; return; }
            _start = transform.position;
            _cameraStart = targetCamera.transform.position;

            if (repeatHorizontally)
            {
                // optional: the layer may also be a plain sprite, so no [RequireComponent(typeof(Tilemap))]
                if (TryGetComponent(out Tilemap map))
                {
                    map.CompressBounds();
                    var b = map.cellBounds;
                    float cell = map.layoutGrid != null ? map.layoutGrid.cellSize.x * transform.lossyScale.x : transform.lossyScale.x;
                    _left = map.CellToWorld(new Vector3Int(b.xMin, 0, 0)).x - transform.position.x;
                    _width = repeatWidth > 0f ? repeatWidth : b.size.x * cell;
                }
                else _width = repeatWidth;
                if (_width > 0f)
                {
                    // the copy is a plain duplicate: its own ParallaxLayer is turned off before it can start
                    var go = Instantiate(gameObject, transform.parent);
                    go.name = name + " (repeat)";
                    if (go.TryGetComponent(out ParallaxLayer layer)) { layer._isCopy = true; layer.enabled = false; }
                    _copy = go.transform;
                }
            }
        }

        void LateUpdate()
        {
            if (_isCopy || targetCamera == null) return;
            var camera = targetCamera.transform.position;
            var moved = camera - _cameraStart;
            var pos = _start + new Vector3(moved.x * (1f - factor.x), moved.y * (1f - factor.y), 0f);

            if (_copy != null)
            {
                // put the main copy on the repeat the camera is in, and the second copy on the side the camera is
                // closer to, so the screen is always covered
                float rel = camera.x - (pos.x + _left);
                int k = Mathf.FloorToInt(rel / _width);
                pos.x += k * _width;
                bool rightHalf = rel - k * _width > _width * 0.5f;
                _copy.position = Snap(pos + new Vector3(rightHalf ? _width : -_width, 0f, 0f));
            }
            transform.position = Snap(pos);
        }

        Vector3 Snap(Vector3 p)
        {
            if (pixelsPerUnit <= 0) return p;
            return new Vector3(Mathf.Round(p.x * pixelsPerUnit) / pixelsPerUnit, Mathf.Round(p.y * pixelsPerUnit) / pixelsPerUnit, p.z);
        }

        void OnDestroy()
        {
            if (_copy != null) Destroy(_copy.gameObject);
        }
    }
}
