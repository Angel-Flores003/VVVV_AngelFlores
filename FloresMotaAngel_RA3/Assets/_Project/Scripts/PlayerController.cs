using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour, InputSystem_Actions.IPlayerVActions
{
	private InputSystem_Actions inputAction;
	private InputAction move, jump;
	private Rigidbody2D rb;
	private SpriteRenderer spriteRenderer;
	[SerializeField] private float rayLength = 1f;
	[SerializeField] private float gravity = 1f;
	public LayerMask groundLayer;
	private Animator animator;
	private bool isGravityFlipped;

	void Start()
	{
		move = InputSystem.actions.FindAction("Move");
		jump = InputSystem.actions.FindAction("Jump");
		rb = GetComponent<Rigidbody2D>();
		spriteRenderer = GetComponent<SpriteRenderer>();
		animator = GetComponent<Animator>();

		rb.gravityScale = gravity;
	}

	void Update()
	{
		Vector2 vec = move.ReadValue<Vector2>();
		Vector3 dir = new Vector3(vec.x, 0, vec.y);
		//mov
		transform.Translate(dir * 10 * Time.deltaTime);
		//direction
		if (vec.x < 0)
		{
			spriteRenderer.flipX = false;
		}
		else if (vec.x > 0)
		{
			spriteRenderer.flipX = true;
		}
		//animation
		animator.SetFloat("speed", Mathf.Abs(vec.x));
	}

	private void OnEnable()
	{
		inputAction.Enable();
	}

	private void OnDisable()
	{
		inputAction.Disable();
	}

	private void Awake()
	{
		inputAction = new InputSystem_Actions();
		inputAction.PlayerV.SetCallbacks(this);
	}

	public void OnMove(InputAction.CallbackContext context)
	{
		if (!context.performed) return;
	}
	public void OnJump(InputAction.CallbackContext context)
	{
		if (!context.performed) return;

		if (IsGrounded() && jump.WasPressedThisFrame())
		{
			FlipGravity();
		}
	}

	bool IsGrounded()
	{
		Vector2 rayDirection = rb.gravityScale > 0 ? Vector2.down : Vector2.up;
		RaycastHit2D hit = Physics2D.Raycast(transform.position, rayDirection, rayLength, groundLayer);

		return hit;
	}

	void FlipGravity()
	{
		isGravityFlipped = !isGravityFlipped;
		rb.gravityScale *= -1;
		spriteRenderer.flipY = isGravityFlipped;
	}

	private void OnTriggerEnter2D(Collider2D collision)
	{
		if (collision.transform.CompareTag("Spikes"))
		{
			Debug.Log("Te has pinchado");
		}
	}
}