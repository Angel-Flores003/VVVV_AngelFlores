using UnityEngine;
using UnityEngine.Tilemaps;

namespace AtomicRealm.WorldBuilder
{
    /// <summary>
    /// Keeps the camera centred on a target (e.g. the player), optionally smoothed, with the position snapped to
    /// whole pixels so a Pixel Perfect Camera stays crisp. Starts centred on the target. Runs before
    /// <see cref="ParallaxLayer"/> (in Start and every frame), so parallax layers anchor to that start position and
    /// follow the camera's final position in each frame.
    /// With "Confine To" the view stays inside a tilemap's painted area, e.g. a border frame around the level; when
    /// that tilemap moves with a ParallaxLayer, the limit moves with it, so the screen edge stops at the frame's edge.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class CameraFollow2D : MonoBehaviour
    {
        [Tooltip("What to follow.")]
        public Transform target;

        [Tooltip("Offset from the target, in world units.")]
        public Vector2 offset;

        [Tooltip("Time to catch up with the target, in seconds. 0 = locked to the target.")]
        public float smoothTime = 0.12f;

        [Tooltip("Snap the camera to whole pixels (match your sprites' Pixels Per Unit). 0: no snapping.")]
        public int pixelsPerUnit = 32;

        [Tooltip("Keep the view inside this tilemap's painted area (e.g. a border frame around the level). If the " +
                 "tilemap has a ParallaxLayer, the limit follows it. Empty: no limit.")]
        public Tilemap confineTo;

        Vector3 _velocity;
        Vector3 _smooth;   // the unsnapped position, so smoothing isn't disturbed by the snapping
        Camera _camera;
        bool _confined;
        Rect _area;                          // the confining tilemap's painted area at the start, in world units
        Vector2 _factor = Vector2.one;       // its parallax factor (1: moves with the world)
        Vector2 _start;                      // the camera's start position, where the area is as painted

        void Start()
        {
            _smooth = transform.position;
            if (target == null) return;
            // start on the target, not where the camera object happens to sit in the scene: this runs before the
            // parallax layers' Start, so they anchor to this position (layers show as painted around the target)
            var goal = Goal();
            if (confineTo != null && TryGetComponent(out _camera))
            {
                confineTo.CompressBounds();
                var b = confineTo.cellBounds;
                if (b.size.x > 0 && b.size.y > 0)
                {
                    Vector3 min = confineTo.CellToWorld(b.min), max = confineTo.CellToWorld(new Vector3Int(b.xMax, b.yMax, 0));
                    _area = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
                    if (confineTo.TryGetComponent(out ParallaxLayer layer) && layer.enabled) _factor = layer.factor;
                    // at the start the area is where it was painted, whatever its parallax
                    goal.x = Limit(goal.x, _area.xMin, _area.xMax, HalfWidth(), 1f, 0f);
                    goal.y = Limit(goal.y, _area.yMin, _area.yMax, _camera.orthographicSize, 1f, 0f);
                    _start = goal;
                    _confined = true;
                }
            }
            _smooth = goal;
            transform.position = Snap(_smooth);
        }

        void LateUpdate()
        {
            if (target == null) return;
            var goal = Confine(Goal());
            _smooth = smoothTime > 0f ? Vector3.SmoothDamp(_smooth, goal, ref _velocity, smoothTime) : goal;
            // Snap the target and the camera's lag behind it separately (not the smoothed position as a whole):
            // otherwise camera and target cross pixel boundaries at different moments and the target wobbles by a
            // pixel on screen while moving. At steady speed the lag is constant, so both step together.
            transform.position = Snap(goal) + Snap(_smooth - goal);
        }

        Vector3 Confine(Vector3 p)
        {
            if (!_confined) return p;
            // a pixel inside the area: the camera and a parallax area snap to whole pixels separately, and their
            // roundings could otherwise open a one-pixel gap at the edge
            float inset = pixelsPerUnit > 0 ? 1f / pixelsPerUnit : 0f;
            p.x = Limit(p.x, _area.xMin + inset, _area.xMax - inset, HalfWidth(), _factor.x, _start.x);
            p.y = Limit(p.y, _area.yMin + inset, _area.yMax - inset, _camera.orthographicSize, _factor.y, _start.y);
            return p;
        }

        float HalfWidth() => _camera.orthographicSize * _camera.aspect;

        // The camera position c along one axis that keeps the view [c - half, c + half] inside the area [min, max],
        // which a ParallaxLayer with `factor` shifts by (c - start) * (1 - factor): solved for c. A view larger than
        // the area is centred on it.
        static float Limit(float c, float min, float max, float half, float factor, float start)
        {
            if (factor <= 0.01f) return c;                    // an area fixed to the camera can't be left anyway
            float shift = start * (1f - factor);
            float lo = (min + half - shift) / factor, hi = (max - half - shift) / factor;
            return lo > hi ? (lo + hi) * 0.5f : Mathf.Clamp(c, lo, hi);
        }

        Vector3 Snap(Vector3 p) => pixelsPerUnit <= 0 ? p
            : new Vector3(Mathf.Round(p.x * pixelsPerUnit) / pixelsPerUnit, Mathf.Round(p.y * pixelsPerUnit) / pixelsPerUnit, p.z);

        Vector3 Goal() => new Vector3(target.position.x + offset.x, target.position.y + offset.y, transform.position.z);
    }
}
