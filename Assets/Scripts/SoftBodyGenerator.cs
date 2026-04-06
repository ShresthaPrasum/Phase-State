using UnityEngine;
using UnityEngine.InputSystem;

public class SoftBodyGenerator : MonoBehaviour
{
    [SerializeField] private int boneCount = 8;
    [SerializeField] private float circleRadius = 1.5f;
    [SerializeField] private GameObject bonePrefab;
    
    [Header("Spring Joint Settings")]
    [SerializeField] private float springFrequency = 5f;
    [SerializeField] private float dampingRatio = 0.5f;
    [SerializeField] private float perimeterSpringFrequency = 4f;
    [SerializeField] private float perimeterDampingRatio = 0.6f;

    [Header("Volume Preservation")]
    [SerializeField] private float areaPressureStrength = 5f;
    [SerializeField] private float maxPressureForce = 3f;
    
    [Header("Bone Physics")]
    [SerializeField] private float boneMass = 1f;
    [SerializeField] private float boneFriction = 0.1f;
    [SerializeField] private float boneCircleRadius = 0.25f;
    
    [Header("Collision")]
    [SerializeField] private LayerMask boneLayer;

    [Header("Controls")]
    [SerializeField] private bool enableKeyboardControl = true;
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float jumpImpulse = 4f;
    
    private GameObject[] bones;
    private Rigidbody2D[] boneRigidbodies;
    private Rigidbody2D centerRb;
    private Vector3[] boneOffsets;
    private Vector2 moveInput;
    private bool jumpRequested;
    private float targetArea;

    private void Start()
    {
        centerRb = GetComponent<Rigidbody2D>();
        if (centerRb == null)
        {
            centerRb = gameObject.AddComponent<Rigidbody2D>();
        }
        centerRb.bodyType = RigidbodyType2D.Kinematic;

        bones = new GameObject[boneCount];
        boneRigidbodies = new Rigidbody2D[boneCount];
        boneOffsets = new Vector3[boneCount];

        for (int i = 0; i < boneCount; i++)
        {
            float angle = (360f / boneCount) * i;
            float radians = angle * Mathf.Deg2Rad;
            
            Vector3 position = transform.position + new Vector3(
                Mathf.Cos(radians) * circleRadius,
                Mathf.Sin(radians) * circleRadius,
                0f
            );

            bones[i] = new GameObject($"Bone_{i}");
            bones[i].transform.position = position;

            boneOffsets[i] = new Vector3(
                Mathf.Cos(radians) * circleRadius,
                Mathf.Sin(radians) * circleRadius,
                0f
            );

            SetupBonePhysics(bones[i], i);
            SetupSpringJoint(bones[i]);
        }

        SetupPerimeterSprings();
        targetArea = ComputePolygonArea();

        SetupCollisionIgnore();
    }

    private void Update()
    {
        if (!enableKeyboardControl)
        {
            return;
        }

        float horizontal = 0f;
        if (Keyboard.current != null)
        {
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
            {
                horizontal -= 1f;
            }

            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
            {
                horizontal += 1f;
            }

            if (Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                jumpRequested = true;
            }
        }

        moveInput = new Vector2(horizontal, 0f);
    }

    private void FixedUpdate()
    {
        if (enableKeyboardControl)
        {
            Vector2 targetPosition = centerRb.position + (moveInput * moveSpeed * Time.fixedDeltaTime);
            centerRb.MovePosition(targetPosition);

            if (jumpRequested)
            {
                for (int i = 0; i < boneRigidbodies.Length; i++)
                {
                    if (boneRigidbodies[i] != null)
                    {
                        boneRigidbodies[i].AddForce(Vector2.up * jumpImpulse, ForceMode2D.Impulse);
                    }
                }

                jumpRequested = false;
            }
        }

        ApplyAreaPressure();
    }

    private void SetupPerimeterSprings()
    {
        for (int i = 0; i < boneRigidbodies.Length; i++)
        {
            int next = (i + 1) % boneRigidbodies.Length;
            Rigidbody2D current = boneRigidbodies[i];
            Rigidbody2D connected = boneRigidbodies[next];

            if (current == null || connected == null)
            {
                continue;
            }

            SpringJoint2D perimeterSpring = current.gameObject.AddComponent<SpringJoint2D>();
            perimeterSpring.connectedBody = connected;
            perimeterSpring.autoConfigureConnectedAnchor = true;
            perimeterSpring.autoConfigureDistance = false;
            perimeterSpring.distance = Vector2.Distance(current.position, connected.position);
            perimeterSpring.frequency = perimeterSpringFrequency;
            perimeterSpring.dampingRatio = perimeterDampingRatio;
            perimeterSpring.enableCollision = false;
        }
    }

    private void ApplyAreaPressure()
    {
        if (boneRigidbodies == null || boneRigidbodies.Length < 3)
        {
            return;
        }

        float currentArea = ComputePolygonArea();
        float areaError = targetArea - currentArea;
        float pressure = Mathf.Clamp(areaError * areaPressureStrength, -maxPressureForce, maxPressureForce);

        Vector2 centerPosition = centerRb.position;
        for (int i = 0; i < boneRigidbodies.Length; i++)
        {
            if (boneRigidbodies[i] == null)
            {
                continue;
            }

            Vector2 radialDirection = (boneRigidbodies[i].position - centerPosition).normalized;
            boneRigidbodies[i].AddForce(radialDirection * pressure, ForceMode2D.Force);
        }
    }

    private float ComputePolygonArea()
    {
        if (bones == null || bones.Length < 3)
        {
            return 0f;
        }

        float area = 0f;
        for (int i = 0; i < bones.Length; i++)
        {
            int next = (i + 1) % bones.Length;
            Vector2 a = bones[i].transform.position;
            Vector2 b = bones[next].transform.position;
            area += (a.x * b.y) - (b.x * a.y);
        }

        return Mathf.Abs(area) * 0.5f;
    }

    private void SetupBonePhysics(GameObject bone, int index)
    {
        Rigidbody2D boneRb = bone.AddComponent<Rigidbody2D>();
        boneRb.mass = boneMass;
        boneRb.gravityScale = 1f;
        boneRb.constraints = RigidbodyConstraints2D.FreezeRotation;
        boneRigidbodies[index] = boneRb;

        CircleCollider2D circleCollider = bone.AddComponent<CircleCollider2D>();
        circleCollider.radius = boneCircleRadius;

        PhysicsMaterial2D material = new PhysicsMaterial2D
        {
            friction = boneFriction,
            bounciness = 0.2f
        };
        circleCollider.sharedMaterial = material;
        circleCollider.usedByEffector = false;
    }

    private void SetupSpringJoint(GameObject bone)
    {
        SpringJoint2D springJoint = bone.AddComponent<SpringJoint2D>();
        springJoint.connectedBody = centerRb;
        springJoint.autoConfigureConnectedAnchor = true;
        springJoint.frequency = springFrequency;
        springJoint.dampingRatio = dampingRatio;
        springJoint.enableCollision = false;
    }

    private void SetupCollisionIgnore()
    {
        int layer = boneLayer.value == 0 ? LayerMask.NameToLayer("Bone") : Mathf.RoundToInt(Mathf.Log(boneLayer.value, 2));
        if (layer == -1)
        {
            Debug.LogWarning("Bone layer not found. Creating new layer would require editor. Please manually create a 'Bone' layer.");
            return;
        }

        for (int i = 0; i < bones.Length; i++)
        {
            bones[i].layer = layer;
            for (int j = i + 1; j < bones.Length; j++)
            {
                Physics2D.IgnoreLayerCollision(layer, layer, true);
            }
        }
    }

    public Vector3[] GetBonePositions()
    {
        Vector3[] positions = new Vector3[bones.Length];
        for (int i = 0; i < bones.Length; i++)
        {
            if (bones[i] != null)
            {
                positions[i] = bones[i].transform.position;
            }
        }
        return positions;
    }

    public GameObject GetBone(int index)
    {
        if (index >= 0 && index < bones.Length)
        {
            return bones[index];
        }
        return null;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.black;
        Gizmos.DrawWireSphere(transform.position, circleRadius);

        if (Application.isPlaying && bones != null)
        {
            Gizmos.color = Color.black;
            foreach (var bone in bones)
            {
                if (bone != null)
                {
                    Gizmos.DrawWireSphere(bone.transform.position, boneCircleRadius);
                }
            }
        }
    }
}
