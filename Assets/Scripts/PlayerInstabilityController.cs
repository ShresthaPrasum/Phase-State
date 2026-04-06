using UnityEngine;
using UnityEngine.InputSystem;
using System;

public class PlayerInstabilityController : MonoBehaviour
{
    public enum PhaseState { Solid, Liquid, Gas }

    public delegate void StateChangedDelegate(PhaseState newState);
    public event StateChangedDelegate OnStateChanged;


    [Header("Physics References")]
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private Collider2D mainCollider;

    [Header("Input Settings")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private KeyCode solidKey = KeyCode.Alpha1;
    [SerializeField] private KeyCode liquidKey = KeyCode.Alpha2;
    [SerializeField] private KeyCode gasKey = KeyCode.Alpha3;

    [Header("Solid State Config")]
    [SerializeField] private float solidMass = 2f;
    [SerializeField] private float solidFriction = 0.8f;
    [SerializeField] private float solidGravityScale = 1f;
    [SerializeField] private float solidJumpForce = 8f;
    [SerializeField] private LayerMask solidCollisionMask = ~0;

    [Header("Liquid State Config")]
    [SerializeField] private float liquidMass = 1f;
    [SerializeField] private float liquidFriction = 0.2f;
    [SerializeField] private float liquidGravityScale = 0.7f;
    [SerializeField] private float liquidJumpForce = 4f;
    [SerializeField] private LayerMask liquidCollisionMask = ~0;

    [Header("Gas State Config")]
    [SerializeField] private float gasMass = 0.5f;
    [SerializeField] private float gasFriction = 0f;
    [SerializeField] private float gasGravityScale = -0.5f;
    [SerializeField] private float gasJumpForce = 2f;
    [SerializeField] private LayerMask gasCollisionMask;

    [Header("Instability System")]
    [SerializeField] private float maxInstability = 100f;
    [SerializeField] private float liquidInstabilityGainPerSecond = 15f;
    [SerializeField] private float gasInstabilityGainPerSecond = 25f;
    [SerializeField] private float solidInstabilityCooldownPerSecond = 20f;
    [SerializeField] private float instabilityResetStunDuration = 1f;

    [Header("Movement")]
    [SerializeField] private float groundDrag = 1f;
    [SerializeField] private float airDrag = 0.5f;
    [SerializeField] private float coyoteTime = 0.1f;
    [SerializeField] private LayerMask groundLayers = -1;
    [SerializeField] private float groundCheckExtraDistance = 0.08f;

    private PhaseState currentState = PhaseState.Solid;
    private float instabilityValue = 0f;
    private float coyoteCounter = 0f;
    private float stunCounter = 0f;
    private Vector2 moveInput = Vector2.zero;
    private bool isGrounded = false;
    private bool jumpPressed = false;

    [Header("Visual References")]
    [SerializeField] private SpriteRenderer playerBodySpriteRenderer;
    [SerializeField] private SpriteRenderer[] extraFlipRenderers;
    [SerializeField] private float trailOffsetX = 0.15f;
    [SerializeField] private Animator playerAnimator;
    [SerializeField] private bool flipPlayerBodySprite = false;

    private SpriteRenderer spriteRenderer;
    private Animator animator;
    private Vector3[] extraFlipRendererBasePositions;

    [Header("Animation")]
    [SerializeField] private string speedParameterName = "speed";

    private const float GROUND_CHECK_DISTANCE = 0.1f;
    private const string PLAYER_LAYER_NAME = "Player";

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        mainCollider = GetComponent<Collider2D>();
        spriteRenderer = playerBodySpriteRenderer != null ? playerBodySpriteRenderer : GetComponent<SpriteRenderer>();
        animator = playerAnimator != null ? playerAnimator : GetComponent<Animator>();
        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>(true);
        }

        if (extraFlipRenderers != null && extraFlipRenderers.Length > 0)
        {
            extraFlipRendererBasePositions = new Vector3[extraFlipRenderers.Length];
            for (int i = 0; i < extraFlipRenderers.Length; i++)
            {
                extraFlipRendererBasePositions[i] = extraFlipRenderers[i] != null
                    ? extraFlipRenderers[i].transform.localPosition
                    : Vector3.zero;
            }
        }

        if (rb == null || mainCollider == null)
        {
            Debug.LogError("PlayerInstabilityController requires Rigidbody2D and a Collider2D (BoxCollider2D, PolygonCollider2D, CircleCollider2D, etc.)!");
            enabled = false;
            return;
        }

        ApplyPhaseState(PhaseState.Solid);
    }

    private void Update()
    {
        UpdateGroundedState();
        UpdateCoyoteTime();
        HandleInput();
        UpdateInstability();
        UpdateStunCounter();
    }

    private void FixedUpdate()
    {
        if (stunCounter > 0) return;

        ApplyMovement();
    }

    private void HandleInput()
    {
        if (Keyboard.current == null)
        {
            moveInput = Vector2.zero;
            UpdateVisualsAndAnimator();
            return;
        }

        float horizontalInput = 0f;
        if (Keyboard.current[Key.D].isPressed || Keyboard.current[Key.RightArrow].isPressed)
            horizontalInput = 1f;
        else if (Keyboard.current[Key.A].isPressed || Keyboard.current[Key.LeftArrow].isPressed)
            horizontalInput = -1f;

        if (currentState == PhaseState.Gas)
        {
            horizontalInput = 0f;
        }

        moveInput = new Vector2(horizontalInput, 0);
        UpdateVisualsAndAnimator();

        if (currentState == PhaseState.Solid && (Keyboard.current[Key.Space].wasPressedThisFrame || Keyboard.current[Key.W].wasPressedThisFrame))
        {
            jumpPressed = true;
        }

        if (Keyboard.current[Key.Digit1].wasPressedThisFrame)
            TransitionToState(PhaseState.Solid);
        if (Keyboard.current[Key.Digit2].wasPressedThisFrame)
            TransitionToState(PhaseState.Liquid);
        if (Keyboard.current[Key.Digit3].wasPressedThisFrame)
            TransitionToState(PhaseState.Gas);
    }

    private void UpdateVisualsAndAnimator()
    {
        if (animator != null && !string.IsNullOrEmpty(speedParameterName))
        {
            float animationSpeed = moveInput.x;
            animator.SetFloat(speedParameterName, animationSpeed);
            animator.SetBool("Isrunning", Mathf.Abs(moveInput.x) > 0f);
        }

        bool? shouldFlip = null;

        if (moveInput.x < 0f)
        {
            shouldFlip = true;
        }
        else if (moveInput.x > 0f)
        {
            shouldFlip = false;
        }

        if (shouldFlip.HasValue)
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.flipX = shouldFlip.Value;
            }

            if (extraFlipRenderers != null)
            {
                for (int i = 0; i < extraFlipRenderers.Length; i++)
                {
                    if (extraFlipRenderers[i] != null)
                    {
                        Vector3 basePosition = extraFlipRendererBasePositions != null && i < extraFlipRendererBasePositions.Length
                            ? extraFlipRendererBasePositions[i]
                            : extraFlipRenderers[i].transform.localPosition;

                        if (shouldFlip.Value)
                        {
                            extraFlipRenderers[i].transform.localPosition = new Vector3(
                                basePosition.x - trailOffsetX,
                                basePosition.y,
                                basePosition.z
                            );
                        }
                        else
                        {
                            extraFlipRenderers[i].transform.localPosition = basePosition;
                        }
                    }
                }
            }
        }
    }

    private void TransitionToState(PhaseState newState)
    {
        if (currentState == newState) return;

        currentState = newState;
        ApplyPhaseState(newState);
        OnStateChanged?.Invoke(newState);

        Debug.Log($"[Phase State] Transitioned to: {newState} | Instability: {instabilityValue:F1}%");
    }

    private void ApplyPhaseState(PhaseState state)
    {
        switch (state)
        {
            case PhaseState.Solid:
                ApplySolidState();
                break;
            case PhaseState.Liquid:
                ApplyLiquidState();
                break;
            case PhaseState.Gas:
                ApplyGasState();
                break;
        }
    }

    private void ApplySolidState()
    {
        rb.mass = solidMass;
        rb.linearDamping = solidFriction;
        rb.gravityScale = solidGravityScale;
        mainCollider.isTrigger = false;

        SafeIgnoreLayerCollision("Grates", false);
    }

    private void ApplyLiquidState()
    {
        rb.mass = liquidMass;
        rb.linearDamping = liquidFriction;
        rb.gravityScale = liquidGravityScale;
        mainCollider.isTrigger = false;

        SafeIgnoreLayerCollision("Grates", false);
    }

    private void ApplyGasState()
    {
        rb.mass = gasMass;
        rb.linearDamping = gasFriction;
        rb.gravityScale = gasGravityScale;
        mainCollider.isTrigger = false;
        rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);

        SafeIgnoreLayerCollision("Grates", true);
    }

    private void SafeIgnoreLayerCollision(string otherLayerName, bool ignore)
    {
        int playerLayer = LayerMask.NameToLayer(PLAYER_LAYER_NAME);
        int otherLayer = LayerMask.NameToLayer(otherLayerName);

        if (playerLayer == -1)
        {
            Debug.LogWarning($"[PlayerInstabilityController] Layer '{PLAYER_LAYER_NAME}' not found. Please create it in Tags & Layers.");
            return;
        }

        if (otherLayer == -1)
        {
            Debug.LogWarning($"[PlayerInstabilityController] Layer '{otherLayerName}' not found. Please create it in Tags & Layers for Gas state to work properly.");
            return;
        }

        Physics2D.IgnoreLayerCollision(playerLayer, otherLayer, ignore);
    }

    private void ApplyMovement()
    {
        float targetVelocityX = moveInput.x * moveSpeed;
        if (currentState == PhaseState.Gas)
        {
            targetVelocityX = 0f;
        }

        rb.linearVelocity = new Vector2(targetVelocityX, rb.linearVelocity.y);

        if (jumpPressed && isGrounded)
        {
            float jumpForce = currentState switch
            {
                PhaseState.Solid => solidJumpForce,
                PhaseState.Liquid => liquidJumpForce,
                PhaseState.Gas => gasJumpForce,
                _ => solidJumpForce
            };

            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
            jumpPressed = false;
        }
        else
        {
            jumpPressed = false;
        }

        rb.linearDamping = isGrounded ? groundDrag : airDrag;
    }

    private void UpdateGroundedState()
    {
        Vector2 rayOrigin = mainCollider != null
            ? new Vector2(mainCollider.bounds.center.x, mainCollider.bounds.min.y + 0.01f)
            : (Vector2)transform.position;

        float rayDistance = mainCollider != null
            ? groundCheckExtraDistance
            : GROUND_CHECK_DISTANCE;

        RaycastHit2D hit = Physics2D.Raycast(
            rayOrigin,
            Vector2.down,
            rayDistance,
            groundLayers
        );

        isGrounded = hit.collider != null;
    }

    private void UpdateCoyoteTime()
    {
        if (isGrounded)
        {
            coyoteCounter = coyoteTime;
        }
        else
        {
            coyoteCounter -= Time.deltaTime;
        }
    }

    private void UpdateInstability()
    {
        if (stunCounter > 0) return;

        if (currentState == PhaseState.Solid && isGrounded && moveInput.magnitude < 0.01f)
        {
            instabilityValue = Mathf.Max(0, instabilityValue - solidInstabilityCooldownPerSecond * Time.deltaTime);
        }
        else if (currentState == PhaseState.Liquid)
        {
            instabilityValue += liquidInstabilityGainPerSecond * Time.deltaTime;
        }
        else if (currentState == PhaseState.Gas)
        {
            instabilityValue += gasInstabilityGainPerSecond * Time.deltaTime;
        }

        if (instabilityValue >= maxInstability)
        {
            TriggerInstabilityReset();
        }

        instabilityValue = Mathf.Clamp(instabilityValue, 0, maxInstability);
    }

    private void TriggerInstabilityReset()
    {
        instabilityValue = 0;
        stunCounter = instabilityResetStunDuration;

        TransitionToState(PhaseState.Solid);

        Debug.Log("[Instability] RESET TRIGGERED! Player is stunned and forced to Solid.");
    }

    private void UpdateStunCounter()
    {
        if (stunCounter > 0)
        {
            stunCounter -= Time.deltaTime;
        }
    }

    public PhaseState GetCurrentState() => currentState;
    public float GetInstabilityPercent() => (instabilityValue / maxInstability) * 100f;
    public float GetInstabilityValue() => instabilityValue;
    public bool IsStunned() => stunCounter > 0;
    public bool IsGrounded() => isGrounded;

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Vector3 rayStart = transform.position;
        Vector3 rayEnd = rayStart + Vector3.down * GROUND_CHECK_DISTANCE;
        Gizmos.DrawLine(rayStart, rayEnd);

        if (Application.isPlaying && isGrounded)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(transform.position, Vector3.one * 0.2f);
        }
    }

    public void ResetPlayer()
    {
        rb.linearVelocity = Vector2.zero;
        instabilityValue = 0;
        stunCounter = 0;
        TransitionToState(PhaseState.Solid);
    }
}
