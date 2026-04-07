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
    [SerializeField] private float liquidExtraFallForce = 8f;
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

    [Header("Instability HUD")]
    [SerializeField] private bool showInstabilityHud = true;
    [SerializeField] private Vector2 hudAnchor = new Vector2(20f, 20f);
    [SerializeField] private Vector2 hudSize = new Vector2(260f, 20f);
    [SerializeField] private float hudScale = 1f;
    [SerializeField] private int hudFontSize = 14;

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
    private GUIStyle hudLabelStyle;

    [Header("Animation")]
    [SerializeField] private string speedParameterName = "speed";

    [Header("Mode Presentation")]
    [SerializeField] private GameObject solidVisualRoot;
    [SerializeField] private Transform solidPresentationTransform;
    [SerializeField] private Collider2D[] solidOnlyColliders;
    [SerializeField] private GameObject fluidPlayerRoot;
    [SerializeField] private Transform fluidPresentationTransform;
    [SerializeField] private Rigidbody2D fluidPresentationRigidbody;
    [SerializeField] private bool useFluidPresentationForLiquid = true;
    [SerializeField] private bool useFluidPresentationForGas = false;
    private SoftBodyGenerator cachedFluidSoftBody;
    private bool isLiquidFrozenFromGasTransition = false;

    private const float GROUND_CHECK_DISTANCE = 0.1f;
    private const string PLAYER_LAYER_NAME = "Player";

    private void Awake()
    {
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
            Debug.LogError("PlayerInstabilityController requires manual Inspector assignment for Rigidbody2D and Collider2D references.");
            enabled = false;
            return;
        }

        if (fluidPresentationRigidbody == rb)
        {
            Debug.LogWarning("[PlayerInstabilityController] fluidPresentationRigidbody points to the solid/core rb. Clear it and assign the fluid body's Rigidbody2D instead.");
            fluidPresentationRigidbody = null;
        }

        ApplyPhaseState(PhaseState.Solid);
        ApplyModePresentation(currentState);
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
        ApplyLiquidFallAcceleration();
        EnsureFluidIsSelfDriven();
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

        PhaseState oldState = currentState;
        Vector2 handoffPosition = ReadActivePresentationPosition(oldState);

        currentState = newState;
        ApplyPhaseState(newState);
        ApplyModePresentation(newState);
        ApplyTransitionPosition(newState, handoffPosition);

        // Freeze liquid movement if transitioning from Gas to Liquid
        if (oldState == PhaseState.Gas && newState == PhaseState.Liquid)
        {
            isLiquidFrozenFromGasTransition = true;
            SoftBodyGenerator softBody = GetFluidSoftBody();
            if (softBody != null)
            {
                softBody.FreezeHorizontalMovement();
            }
        }

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

    private void ApplyModePresentation(PhaseState state)
    {
        bool useFluid = (state == PhaseState.Liquid && useFluidPresentationForLiquid) ||
                        (state == PhaseState.Gas && useFluidPresentationForGas);

        if (solidVisualRoot != null)
        {
            solidVisualRoot.SetActive(!useFluid);
        }

        if (solidOnlyColliders != null)
        {
            for (int i = 0; i < solidOnlyColliders.Length; i++)
            {
                if (solidOnlyColliders[i] != null)
                {
                    solidOnlyColliders[i].enabled = !useFluid;
                }
            }
        }

        if (fluidPlayerRoot != null)
        {
            fluidPlayerRoot.SetActive(useFluid);
            SoftBodyGenerator softBody = GetFluidSoftBody();
            if (!useFluid && softBody != null)
            {
                softBody.ClearExternalFollowTarget();
            }
        }
    }

    private void EnsureFluidIsSelfDriven()
    {
        if (fluidPlayerRoot != null && fluidPlayerRoot.activeSelf)
        {
            SoftBodyGenerator softBody = GetFluidSoftBody();
            if (softBody != null)
            {
                softBody.ClearExternalFollowTarget();
            }
        }
    }

    private bool IsFluidPresentationState(PhaseState state)
    {
        return (state == PhaseState.Liquid && useFluidPresentationForLiquid) ||
               (state == PhaseState.Gas && useFluidPresentationForGas);
    }

    private Vector2 ReadActivePresentationPosition(PhaseState state)
    {
        if (IsFluidPresentationState(state))
        {
            SoftBodyGenerator softBody = GetFluidSoftBody();
            return GetCurrentFluidPosition(softBody);
        }

        return GetCurrentSolidPosition();
    }

    private void ApplyTransitionPosition(PhaseState newState, Vector2 handoffPosition)
    {
        SetSolidPosition(handoffPosition, true);

        if (IsFluidPresentationState(newState))
        {
            SetFluidPosition(handoffPosition, true);
        }
    }

    private Vector2 GetCurrentSolidPosition()
    {
        return rb != null ? rb.position : (Vector2)transform.position;
    }

    private void SetSolidPosition(Vector2 position, bool resetVelocity)
    {
        if (rb != null)
        {
            rb.position = position;
            if (resetVelocity)
            {
                rb.linearVelocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }
        }
    }

    private Vector2 GetCurrentFluidPosition(SoftBodyGenerator softBody)
    {
        if (fluidPresentationTransform != null)
        {
            return fluidPresentationTransform.position;
        }

        if (fluidPresentationRigidbody != null)
        {
            return fluidPresentationRigidbody.position;
        }

        if (softBody != null)
        {
            return softBody.GetCenterPosition();
        }

        if (fluidPlayerRoot != null)
        {
            Rigidbody2D rootRb = fluidPlayerRoot.GetComponent<Rigidbody2D>();
            if (rootRb != null && rootRb != rb)
            {
                return rootRb.position;
            }

            Rigidbody2D[] childRigidbodies = fluidPlayerRoot.GetComponentsInChildren<Rigidbody2D>(true);
            for (int i = 0; i < childRigidbodies.Length; i++)
            {
                if (childRigidbodies[i] != null && childRigidbodies[i] != rb)
                {
                    return childRigidbodies[i].position;
                }
            }

            return fluidPlayerRoot.transform.position;
        }

        Debug.LogError("[PlayerInstabilityController] Fluid position source is not assigned. Set fluidPresentationRigidbody or fluidPlayerRoot correctly in the Inspector.");
        return rb != null ? rb.position : (Vector2)transform.position;
    }

    private void SetFluidPosition(Vector2 position, bool resetVelocity)
    {
        SoftBodyGenerator softBody = GetFluidSoftBody();
        if (softBody != null)
        {
            softBody.SnapToPosition(position, resetVelocity);
            softBody.ClearExternalFollowTarget();
            return;
        }

        if (fluidPresentationRigidbody != null)
        {
            fluidPresentationRigidbody.position = position;
            if (resetVelocity)
            {
                fluidPresentationRigidbody.linearVelocity = Vector2.zero;
                fluidPresentationRigidbody.angularVelocity = 0f;
            }
            return;
        }

        if (fluidPresentationTransform != null)
        {
            fluidPresentationTransform.position = position;
            return;
        }

        if (fluidPlayerRoot != null)
        {
            fluidPlayerRoot.transform.position = position;
        }
    }

    private SoftBodyGenerator GetFluidSoftBody()
    {
        if (fluidPlayerRoot == null)
        {
            cachedFluidSoftBody = null;
            return null;
        }

        if (cachedFluidSoftBody == null)
        {
            cachedFluidSoftBody = fluidPlayerRoot.GetComponent<SoftBodyGenerator>();
            if (cachedFluidSoftBody == null)
            {
                cachedFluidSoftBody = fluidPlayerRoot.GetComponentInChildren<SoftBodyGenerator>(true);
            }
        }

        return cachedFluidSoftBody;
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

    private void ApplyLiquidFallAcceleration()
    {
        if (currentState != PhaseState.Liquid || rb == null)
        {
            return;
        }

        rb.AddForce(Vector2.down * liquidExtraFallForce, ForceMode2D.Force);
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

        // Disable horizontal movement if liquid is frozen from Gas transition
        if (currentState == PhaseState.Liquid && isLiquidFrozenFromGasTransition)
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
        // Use SoftBodyGenerator's ground check when in Liquid state
        if (currentState == PhaseState.Liquid)
        {
            SoftBodyGenerator softBody = GetFluidSoftBody();
            if (softBody != null)
            {
                isGrounded = softBody.IsGrounded();
                
                // Debug: Log liquid ground state if frozen
                if (isLiquidFrozenFromGasTransition)
                {
                    Debug.Log($"[Liquid Ground Check] isGrounded={isGrounded}, isFrozen={isLiquidFrozenFromGasTransition}");
                }
            }
        }
        else
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

        // Unfreeze liquid movement when it touches the ground after Gas transition
        if (isGrounded && currentState == PhaseState.Liquid && isLiquidFrozenFromGasTransition)
        {
            isLiquidFrozenFromGasTransition = false;
            SoftBodyGenerator softBody = GetFluidSoftBody();
            if (softBody != null)
            {
                softBody.UnfreezeHorizontalMovement();
                Debug.Log("[Liquid Unlocked] Called UnfreezeHorizontalMovement()");
            }
        }
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

    public Transform GetCameraFollowTarget()
    {
        if (IsFluidPresentationState(currentState) && fluidPlayerRoot != null)
        {
            if (fluidPresentationTransform != null)
            {
                return fluidPresentationTransform;
            }

            if (fluidPresentationRigidbody != null)
            {
                return fluidPresentationRigidbody.transform;
            }

            SoftBodyGenerator softBody = GetFluidSoftBody();
            if (softBody != null)
            {
                return softBody.transform;
            }

            return fluidPlayerRoot.transform;
        }

        if (solidPresentationTransform != null)
        {
            return solidPresentationTransform;
        }

        if (solidVisualRoot != null)
        {
            return solidVisualRoot.transform;
        }

        return transform;
    }

    private void OnGUI()
    {
        if (!showInstabilityHud)
        {
            return;
        }

        if (hudLabelStyle == null)
        {
            hudLabelStyle = new GUIStyle(GUI.skin.label)
            {
                richText = false,
                alignment = TextAnchor.MiddleLeft
            };
        }

        float scale = Mathf.Max(0.1f, hudScale);
        hudLabelStyle.fontSize = Mathf.RoundToInt(hudFontSize * scale);
        hudLabelStyle.normal.textColor = Color.black;

        float x = hudAnchor.x;
        float y = hudAnchor.y;
        float width = Mathf.Max(1f, hudSize.x * scale);
        float height = Mathf.Max(1f, hudSize.y * scale);
        float percent = Mathf.Clamp01(GetInstabilityPercent() / 100f);
        string labelText = "Instability: " + Mathf.RoundToInt(instabilityValue) + "% [" + currentState + "]";

        float labelPadding = 6f * scale;
        float labelHeight = Mathf.Max(
            hudLabelStyle.CalcHeight(new GUIContent(labelText), width),
            hudLabelStyle.fontSize + (4f * scale));
        float labelY = y - labelPadding - labelHeight;

        Rect labelRect = new Rect(x, labelY, width, labelHeight);
        Rect bgRect = new Rect(x, y, width, height);
        Rect fillRect = new Rect(x, y, width * percent, height);
        Rect borderRect = new Rect(x - scale, y - scale, width + (2f * scale), height + (2f * scale));

        GUI.color = new Color(0f, 0f, 0f, 0.75f);
        GUI.DrawTexture(borderRect, Texture2D.whiteTexture);

        GUI.color = new Color(0.12f, 0.12f, 0.12f, 0.95f);
        GUI.DrawTexture(bgRect, Texture2D.whiteTexture);

        Color low = new Color(0.20f, 0.85f, 0.35f, 1f);
        Color high = new Color(0.95f, 0.15f, 0.15f, 1f);
        GUI.color = Color.Lerp(low, high, percent);
        GUI.DrawTexture(fillRect, Texture2D.whiteTexture);

        GUI.color = Color.black;
        GUI.Label(labelRect, labelText, hudLabelStyle);
    }

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
