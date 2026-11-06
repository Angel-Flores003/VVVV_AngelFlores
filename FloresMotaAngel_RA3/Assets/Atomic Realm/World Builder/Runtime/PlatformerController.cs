using UnityEngine;
using UnityEngine.Tilemaps;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace AtomicRealm.WorldBuilder
{
    /// <summary>
    /// Platformer movement: walks left and right like <see cref="FlyController"/> on the ground (arrows / A D /
    /// gamepad left stick, Shift or the stick click for more speed), falls under gravity and jumps with Space (or the
    /// gamepad's south button). Holding the button jumps higher, up to <see cref="jumpHeight"/>; letting go early
    /// cuts the jump short. In the air it can jump again (<see cref="airJumps"/> times): a fresh jump from where it
    /// is, so jumping again at the top of a jump reaches the highest. A short grace after walking off a ledge and a
    /// short buffer before landing make jumps forgiving. Uses a Kinematic Rigidbody2D moved with
    /// <see cref="Rigidbody2D.Slide"/> like the flyer, so it is stopped by colliders and slides along them. With an
    /// Animator it sets "Speed", "VerticalSpeed", "Grounded" and "WalkAnimSpeed" and mirrors the SpriteRenderer, as
    /// the flyer does. A character uses one controller: while this one is enabled, a <see cref="FlyController"/> on
    /// the same object is switched off (so both can sit on a character and the Inspector checkbox picks one).
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class PlatformerController : MonoBehaviour, IGrabbable
    {
        [Header("Walking")]
        [Tooltip("Top walking speed in world units (tiles) per second.")]
        public float speed = 8f;

        [Tooltip("Speed multiplier while Shift (or the gamepad left-stick click) is held.")]
        public float sprintMultiplier = 2f;

        [Tooltip("How quickly it reaches top speed and stops, in units per second squared. 0 = instantly.")]
        public float acceleration = 80f;

        [Tooltip("How quickly it changes speed in the air while rising, in units per second squared. 0 = instantly.")]
        public float airAcceleration = 50f;

        [Tooltip("How quickly it changes speed while falling, in units per second squared (more than on the ground: " +
                 "the landing can be steered). 0 = instantly.")]
        public float fallingAcceleration = 100f;

        [Header("Jumping")]
        [Tooltip("The highest a jump goes (button held), in world units (tiles).")]
        public float jumpHeight = 5f;

        [Tooltip("Seconds from the take-off to the top of a full jump: sets the gravity.")]
        public float timeToApex = 0.4f;

        [Tooltip("How many more jumps it can make in the air before landing (1 = a double jump).")]
        public int airJumps = 1;

        [Tooltip("Gravity multiplier while rising with the button already released (higher = shorter hops).")]
        public float releasedJumpGravity = 3f;

        [Tooltip("Gravity multiplier while falling (above 1 falls faster than it rose).")]
        public float fallGravity = 1.4f;

        [Tooltip("The fastest it falls, in units per second.")]
        public float maxFallSpeed = 23f;

        [Tooltip("Seconds after walking off a ledge in which a jump still counts as from the ground.")]
        public float coyoteTime = 0.1f;

        [Tooltip("Seconds a jump pressed just before landing is kept and made on landing.")]
        public float jumpBuffer = 0.1f;

        [Header("Other")]
        [Tooltip("Walks with the arrow keys as well as A/D. Off: A/D only, e.g. while the arrows drive something else.")]
        public bool arrowKeys = true;

        [Tooltip("How close below the collider the ground counts as touched, in world units.")]
        public float groundCheckDistance = 0.05f;

        [Tooltip("Walking speed (world units/s) at which the walk animation plays at its normal rate; faster "
               + "walking plays it proportionally faster.")]
        public float walkAnimationSpeed = 3f;

        [Tooltip("Keep it inside this tilemap's painted area, e.g. the border frame around the level (it follows " +
                 "the frame's parallax). Empty: no limit.")]
        public Tilemap confineTo;

        [Tooltip("How far inside the area's edge it stops, in world units.")]
        public float confineMargin = 1f;

        /// <summary>True while standing on (touching the top of) something solid.</summary>
        public bool IsGrounded { get; private set; }

        /// <summary>True while something carries it (e.g. a crane's grabber): it neither walks nor falls meanwhile.</summary>
        public bool IsHeld => _holders > 0;
        int _holders;                                         // two cranes may hold it at once

        // called by whatever picks it up and puts it down (e.g. Industrial Assets' Crane)
        // held: off the ground (its animation hangs), and no jump from before carries over to the release (a buffered
        // press, coyote time); put down, it falls like walking off a ledge (its air jumps left)
        void IGrabbable.OnGrabbed(Transform by) { _holders++; IsGrounded = false; _velocity = Vector2.zero; ClearJump(); }
        void IGrabbable.OnReleased(Transform by)
        {
            _holders = Mathf.Max(0, _holders - 1);
            if (IsHeld) return;
            _velocity = Vector2.zero; ClearJump(); _airJumpsLeft = airJumps;
        }
        void ClearJump() { _jumping = false; _jumpPressedAt = -1f; _groundedAt = -1f; }

        /// <summary>The current movement, in world units per second.</summary>
        public Vector2 Velocity => _velocity;

        /// <summary>The gravity a full jump uses, in units per second squared (from the jump height and time).</summary>
        public float Gravity => 2f * jumpHeight / (timeToApex * timeToApex);

        /// <summary>The take-off speed of a jump, in units per second.</summary>
        public float JumpSpeed => 2f * jumpHeight / timeToApex;

        static readonly int SpeedParam = Animator.StringToHash("Speed");
        static readonly int VerticalSpeedParam = Animator.StringToHash("VerticalSpeed");
        static readonly int GroundedParam = Animator.StringToHash("Grounded");
        static readonly int WalkAnimSpeedParam = Animator.StringToHash("WalkAnimSpeed");

        Rigidbody2D _body;
        Collider2D _collider;
        BoundsInt _confineCells;
        Animator _animator;
        SpriteRenderer _sprite;
        float _inputX;
        bool _sprint, _jumpHeld;
        Vector2 _velocity;
        float _jumpPressedAt = -1f, _groundedAt = -1f;
        int _airJumpsLeft;
        bool _jumping;                                        // rising from a jump (not from a bump or a slope)
        Rigidbody2D.SlideMovement _slide;
        ContactFilter2D _groundFilter;
        readonly RaycastHit2D[] _hits = new RaycastHit2D[4];

#if ENABLE_INPUT_SYSTEM
        InputAction _move, _moveArrows, _sprintAction, _jumpAction;
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
                gravity = Vector2.zero,                       // gravity is this script's: Slide's would add its own
                surfaceAnchor = Vector2.zero,                 // no snapping down onto the ground every step
                surfaceSlideAngle = 90f,
                maxIterations = 3,
            };

#if ENABLE_INPUT_SYSTEM
            _move = new InputAction("Move");
            _move.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/a").With("Positive", "<Keyboard>/d");
            _move.AddBinding("<Gamepad>/leftStick/x");
            _moveArrows = new InputAction("Move (arrows)");
            _moveArrows.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/leftArrow").With("Positive", "<Keyboard>/rightArrow");
            _sprintAction = new InputAction("Sprint", InputActionType.Button);
            _sprintAction.AddBinding("<Keyboard>/shift");
            _sprintAction.AddBinding("<Gamepad>/leftStickPress");
            _jumpAction = new InputAction("Jump", InputActionType.Button);
            _jumpAction.AddBinding("<Keyboard>/space");
            _jumpAction.AddBinding("<Gamepad>/buttonSouth");
#endif
        }

        void OnEnable()
        {
            // one controller moves a character: while this one is on, a flyer on the same object is off
            if (TryGetComponent(out FlyController fly) && fly.enabled)
            {
                fly.enabled = false;
                Debug.Log($"{name}: the PlatformerController is on, so its FlyController is switched off.", this);
            }
#if ENABLE_INPUT_SYSTEM
            _move?.Enable(); _moveArrows?.Enable(); _sprintAction?.Enable(); _jumpAction?.Enable();
#endif
        }

#if ENABLE_INPUT_SYSTEM
        void OnDisable() { _move?.Disable(); _moveArrows?.Disable(); _sprintAction?.Disable(); _jumpAction?.Disable(); }
        void OnDestroy() { _move?.Dispose(); _moveArrows?.Dispose(); _sprintAction?.Dispose(); _jumpAction?.Dispose(); }
#endif

        void Start()
        {
            if (confineTo != null) { confineTo.CompressBounds(); _confineCells = confineTo.cellBounds; }
        }

        void Update()
        {
#if ENABLE_INPUT_SYSTEM
            _inputX = _move.ReadValue<float>() + (arrowKeys ? _moveArrows.ReadValue<float>() : 0f);
            _sprint = _sprintAction.IsPressed();
            _jumpHeld = _jumpAction.IsPressed();
            if (_jumpAction.WasPressedThisFrame()) _jumpPressedAt = Time.time;
#else
            _inputX = Input.GetAxisRaw("Horizontal");
            // the Horizontal axis has the arrows too: without them, A/D only (the legacy Input Manager can't tell a
            // gamepad's stick from the arrows in that axis, so the stick pauses while an arrow is held)
            if (!arrowKeys && (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.RightArrow)))
                _inputX = (Input.GetKey(KeyCode.D) ? 1f : 0f) - (Input.GetKey(KeyCode.A) ? 1f : 0f);
            _sprint = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            _jumpHeld = Input.GetKey(KeyCode.Space) || Input.GetKey(KeyCode.JoystickButton0);
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.JoystickButton0)) _jumpPressedAt = Time.time;
#endif
            _inputX = Mathf.Clamp(_inputX, -1f, 1f);

            if (_animator != null)
            {
                _animator.SetFloat(SpeedParam, Mathf.Abs(_velocity.x));
                _animator.SetFloat(VerticalSpeedParam, _velocity.y);
                _animator.SetBool(GroundedParam, IsGrounded);
                if (walkAnimationSpeed > 0f)
                    _animator.SetFloat(WalkAnimSpeedParam, Mathf.Max(0.5f, Mathf.Abs(_velocity.x) / walkAnimationSpeed));
            }
            if (_sprite != null && Mathf.Abs(_velocity.x) > 0.1f) _sprite.flipX = _velocity.x < 0f;
        }

        /// <summary>Starts a jump, as the jump button does (also for scripts: springs, enemies' heads).</summary>
        public void Jump() => _jumpPressedAt = Time.time;

        void FixedUpdate()
        {
            if (IsHeld) return;                               // the carrier moves it
            float dt = Time.fixedDeltaTime;
            IsGrounded = Touching(Vector2.down) && _velocity.y <= 0.01f;
            if (IsGrounded) { _groundedAt = Time.time; _airJumpsLeft = airJumps; _jumping = false; if (_velocity.y < 0f) _velocity.y = 0f; }

            // walking: as the flyer on the ground, a little looser in the air
            float target = _inputX * speed * (_sprint ? sprintMultiplier : 1f);
            float accel = IsGrounded ? acceleration : _velocity.y < 0f ? fallingAcceleration : airAcceleration;
            _velocity.x = accel > 0f ? Mathf.MoveTowards(_velocity.x, target, accel * dt) : target;

            // jumping: from the ground (or just after leaving it), else one of the air jumps
            if (_jumpPressedAt >= 0f && Time.time - _jumpPressedAt <= jumpBuffer)
            {
                bool fromGround = IsGrounded || (Time.time - _groundedAt <= coyoteTime && !_jumping);
                if (fromGround || _airJumpsLeft > 0)
                {
                    if (!fromGround) _airJumpsLeft--;
                    _velocity.y = JumpSpeed;
                    _jumping = true; IsGrounded = false; _jumpPressedAt = -1f; _groundedAt = -1f;
                }
            }

            // gravity: a full jump's while rising with the button held; stronger once it's let go, or falling
            float vy0 = _velocity.y;
            if (!IsGrounded)
            {
                float g = Gravity;
                if (_velocity.y < 0f) g *= fallGravity;
                else if (!_jumpHeld) g *= releasedJumpGravity;
                _velocity.y = Mathf.Max(_velocity.y - g * dt, -maxFallSpeed);
                if (_velocity.y <= 0f) _jumping = false;
            }

            // the step's average speed, so a held jump peaks exactly at the jump height
            var move = Confine(new Vector2(_velocity.x, (vy0 + _velocity.y) * 0.5f), dt);
            if (move == Vector2.zero) return;
            var step = _slide;
            Vector2 at = _body.position;
            if (move.x != 0f)
            {
                step.useNoMove = true; step.useStartPosition = false;
                var r = _body.Slide(new Vector2(move.x, 0f), dt, step);
                at = r.position;
                if (Mathf.Abs(at.x - _body.position.x) < Mathf.Abs(move.x * dt) * 0.5f && Touching(new Vector2(Mathf.Sign(move.x), 0f))) _velocity.x = 0f;
            }
            step.useNoMove = false; step.useSimulationMove = true;
            step.useStartPosition = true; step.startPosition = at;
            var vr = _body.Slide(new Vector2(0f, move.y), dt, step);
            // a ceiling stops the rise
            if (move.y > 0f && vr.position.y - at.y < move.y * dt * 0.5f) { _velocity.y = 0f; _jumping = false; }
        }

        bool Touching(Vector2 direction)
        {
            int n = _body.Cast(direction, _groundFilter, _hits, groundCheckDistance);
            for (int i = 0; i < n; i++) if (Vector2.Dot(_hits[i].normal, -direction) > 0.7f) return true;
            return false;
        }

        // The velocity limited so the collider ends this step inside the confining area (as the flyer's).
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

        /// <summary>Teleports the character, e.g. to a spawn point.</summary>
        public void Warp(Vector2 position)
        {
            _velocity = Vector2.zero;
            _body.position = position;
            transform.position = position;
        }
    }
}
