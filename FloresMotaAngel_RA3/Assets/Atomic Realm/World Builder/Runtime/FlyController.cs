using UnityEngine;
using UnityEngine.Tilemaps;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace AtomicRealm.WorldBuilder
{
    /// <summary>
    /// "God mode" movement for exploring a level: flies in any direction (arrows / WASD / gamepad left stick, hold
    /// Shift or click the stick for more speed), no gravity, no jumping, but stopped by colliders and sliding along
    /// them. Uses a Kinematic Rigidbody2D moved with <see cref="Rigidbody2D.Slide"/>, which casts the body's
    /// colliders along the movement, so there is no bounce or jitter against walls. Needs a collider on the same
    /// object. Knows when it touches the ground (<see cref="IsGrounded"/>); with an Animator on the same object it
    /// sets the parameters "Speed" (horizontal, absolute), "VerticalSpeed", "Grounded" and "WalkAnimSpeed" (a
    /// playback multiplier that grows with the walking speed), and it mirrors the
    /// SpriteRenderer to face the direction of movement (the sprite is drawn facing right).
    /// With "Confine To" it stays inside a tilemap's painted area, e.g. the border frame around the level, wherever
    /// that frame currently is (it may move with a ParallaxLayer).
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class FlyController : MonoBehaviour
    {
        [Tooltip("Top speed in world units (tiles) per second.")]
        public float speed = 8f;

        [Tooltip("Speed multiplier while Shift (or the gamepad left-stick click) is held.")]
        public float sprintMultiplier = 2f;

        [Tooltip("How quickly it reaches top speed and stops, in units per second squared. 0 = instantly.")]
        public float acceleration = 80f;

        [Tooltip("How close below the collider the ground counts as touched, in world units.")]
        public float groundCheckDistance = 0.05f;

        [Tooltip("Walking speed (world units/s) at which the walk animation plays at its normal rate; faster "
               + "walking plays it proportionally faster.")]
        public float walkAnimationSpeed = 3f;

        [Tooltip("Keep the flyer inside this tilemap's painted area, e.g. the border frame around the level (it follows " +
                 "the frame's parallax). Empty: no limit.")]
        public Tilemap confineTo;

        [Tooltip("How far inside the area's edge the flyer stops, in world units (a frame's debris is drawn in front " +
                 "of it near the edge).")]
        public float confineMargin = 1f;

        /// <summary>True while standing on (touching the top of) something solid.</summary>
        public bool IsGrounded { get; private set; }

        /// <summary>The current movement, in world units per second.</summary>
        public Vector2 Velocity => _velocity;

        static readonly int SpeedParam = Animator.StringToHash("Speed");
        static readonly int VerticalSpeedParam = Animator.StringToHash("VerticalSpeed");
        static readonly int GroundedParam = Animator.StringToHash("Grounded");
        static readonly int WalkAnimSpeedParam = Animator.StringToHash("WalkAnimSpeed");

        Rigidbody2D _body;
        Collider2D _collider;
        BoundsInt _confineCells;
        Animator _animator;
        SpriteRenderer _sprite;
        Vector2 _input, _velocity;
        bool _sprint;
        Rigidbody2D.SlideMovement _slide;
        ContactFilter2D _groundFilter;
        readonly RaycastHit2D[] _groundHits = new RaycastHit2D[4];

#if ENABLE_INPUT_SYSTEM
        InputAction _move, _sprintAction;
#endif

        void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            _body.bodyType = RigidbodyType2D.Kinematic;      // moved only by Slide, never pushed around
            _body.interpolation = RigidbodyInterpolation2D.Interpolate;
            TryGetComponent(out _collider);
            TryGetComponent(out _animator);
            TryGetComponent(out _sprite);
            _groundFilter = new ContactFilter2D { useTriggers = false };
            _groundFilter.SetLayerMask(Physics2D.GetLayerCollisionMask(gameObject.layer));
            _slide = new Rigidbody2D.SlideMovement
            {
                gravity = Vector2.zero,                       // the default is (0, -9.81): no falling in god mode
                surfaceAnchor = Vector2.zero,                 // the default (0, -1) snaps the body down onto the ground
                                                              // every step (for walkers on slopes): it would stick there
                surfaceSlideAngle = 90f,                      // the maximum Slide accepts
                maxIterations = 3,
            };

#if ENABLE_INPUT_SYSTEM
            _move = new InputAction("Move");
            _move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s").With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            _move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow").With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            _move.AddBinding("<Gamepad>/leftStick");
            _sprintAction = new InputAction("Sprint", InputActionType.Button);
            _sprintAction.AddBinding("<Keyboard>/shift");
            _sprintAction.AddBinding("<Gamepad>/leftStickPress");
#endif
        }

#if ENABLE_INPUT_SYSTEM
        void OnEnable() { _move?.Enable(); _sprintAction?.Enable(); }
        void OnDisable() { _move?.Disable(); _sprintAction?.Disable(); }
        void OnDestroy() { _move?.Dispose(); _sprintAction?.Dispose(); }
#endif

        void Start()
        {
            if (confineTo != null) { confineTo.CompressBounds(); _confineCells = confineTo.cellBounds; }
        }

        void Update()
        {
#if ENABLE_INPUT_SYSTEM
            _input = _move.ReadValue<Vector2>();
            _sprint = _sprintAction.IsPressed();
#else
            _input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            _sprint = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
#endif
            if (_input.sqrMagnitude > 1f) _input.Normalize(); // diagonals aren't faster

            if (_animator != null)
            {
                _animator.SetFloat(SpeedParam, Mathf.Abs(_velocity.x));
                _animator.SetFloat(VerticalSpeedParam, _velocity.y);
                _animator.SetBool(GroundedParam, IsGrounded);
                if (walkAnimationSpeed > 0f)
                    _animator.SetFloat(WalkAnimSpeedParam, Mathf.Max(0.5f, Mathf.Abs(_velocity.x) / walkAnimationSpeed));
            }
            // face the way it moves; keep the last direction when moving straight up/down or standing
            if (_sprite != null && Mathf.Abs(_velocity.x) > 0.1f) _sprite.flipX = _velocity.x < 0f;
        }

        void FixedUpdate()
        {
            // on the ground: something solid right below, with a top surface facing up
            IsGrounded = false;
            int n = _body.Cast(Vector2.down, _groundFilter, _groundHits, groundCheckDistance);
            for (int i = 0; i < n; i++) if (_groundHits[i].normal.y > 0.7f) { IsGrounded = true; break; }

            var target = _input * speed * (_sprint ? sprintMultiplier : 1f);
            _velocity = acceleration > 0f ? Vector2.MoveTowards(_velocity, target, acceleration * Time.fixedDeltaTime) : target;
            float dt = Time.fixedDeltaTime;
            var move = Confine(_velocity, dt);                // also brings it back in when it starts outside
            if (move == Vector2.zero) return;

            // Each axis is moved separately: Slide only slides along surfaces up to 90 degrees from "up", so walls
            // (right at 90) and ceilings would stop a diagonal move instead of being slid along. Moved one axis at a
            // time, a wall blocks only the horizontal part and the vertical part still goes through (and vice versa).
            // The first move is only computed; the second starts where it ended and makes the actual physics move.
            var step = _slide;
            Vector2 at = _body.position;
            if (move.x != 0f)
            {
                step.useNoMove = true; step.useStartPosition = false;
                at = _body.Slide(new Vector2(move.x, 0f), dt, step).position;
            }
            step.useNoMove = false; step.useSimulationMove = true;  // move through the physics step: smooth interpolation
            step.useStartPosition = true; step.startPosition = at;
            _body.Slide(new Vector2(0f, move.y), dt, step);
        }

        // The velocity limited so the collider ends this step inside the confining area (or moves back into it);
        // pushing into the edge stops that axis, like a wall.
        Vector2 Confine(Vector2 velocity, float dt)
        {
            if (confineTo == null || _confineCells.size.x <= 0 || _confineCells.size.y <= 0) return velocity;
            Vector2 min = confineTo.CellToWorld(_confineCells.min), max = confineTo.CellToWorld(new Vector3Int(_confineCells.xMax, _confineCells.yMax, 0));
            Vector2 pos = _body.position, extents = Vector2.zero, offset = Vector2.zero;
            if (_collider != null) { var b = _collider.bounds; extents = b.extents; offset = (Vector2)b.center - pos; }
            for (int axis = 0; axis < 2; axis++)
            {
                float lo = min[axis] + confineMargin + extents[axis] - offset[axis], hi = max[axis] - confineMargin - extents[axis] - offset[axis];
                if (lo > hi) lo = hi = (lo + hi) * 0.5f;
                float next = pos[axis] + velocity[axis] * dt, limited = Mathf.Clamp(next, lo, hi);
                if (limited == next) continue;
                velocity[axis] = (limited - pos[axis]) / dt;
                if (Mathf.Sign(_velocity[axis]) != Mathf.Sign(velocity[axis]) || Mathf.Abs(velocity[axis]) < Mathf.Abs(_velocity[axis])) _velocity[axis] = 0f;
            }
            return velocity;
        }

        /// <summary>Teleports the flyer, e.g. to a spawn point.</summary>
        public void Warp(Vector2 position)
        {
            _velocity = Vector2.zero;
            _body.position = position;
            transform.position = position;
        }
    }
}
