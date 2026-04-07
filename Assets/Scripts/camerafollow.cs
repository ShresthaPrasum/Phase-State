using UnityEngine;

public class CameraFollow2D : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;
    [SerializeField] private bool autoFindPlayerOnStart = true;
    [SerializeField] private bool preferRigidbodyPosition = true;

    [Header("Follow")]
    [SerializeField] private Vector3 offset = new Vector3(0f, 1.2f, 0f);
    [SerializeField, Min(0f)] private float smoothTime = 0.15f;
    [SerializeField] private float maxSpeed = 50f;

    [Header("Dead Zone")]
    [SerializeField] private bool useDeadZone = true;
    [SerializeField] private Vector2 deadZoneSize = new Vector2(1.5f, 0.8f);

    [Header("Axis Locks")]
    [SerializeField] private bool lockX;
    [SerializeField] private bool lockY;

    [Header("Stability")]
    [SerializeField] private float positionDeadband = 0.01f;

    private Vector3 velocity;
    private float cameraZ;
    private Rigidbody2D targetRigidbody;

    private void Awake()
    {
        cameraZ = transform.position.z;
    }

    private void Start()
    {
        if (target == null && autoFindPlayerOnStart)
        {
            PlayerInstabilityController player = FindFirstObjectByType<PlayerInstabilityController>();
            if (player != null)
            {
                target = player.transform;
            }
        }

        CacheTargetRigidbody();
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        Vector3 desired = GetTargetPosition() + offset;
        desired.z = cameraZ;

        if (useDeadZone)
        {
            desired = ApplyDeadZone(desired);
        }

        if (lockX)
        {
            desired.x = transform.position.x;
        }

        if (lockY)
        {
            desired.y = transform.position.y;
        }

        if ((desired - transform.position).sqrMagnitude <= positionDeadband * positionDeadband)
        {
            velocity = Vector3.zero;
            return;
        }

        if (smoothTime <= 0f)
        {
            transform.position = desired;
            velocity = Vector3.zero;
            return;
        }

        transform.position = Vector3.SmoothDamp(
            transform.position,
            desired,
            ref velocity,
            smoothTime,
            maxSpeed,
            Time.deltaTime);
    }

    private Vector3 ApplyDeadZone(Vector3 desired)
    {
        Vector3 current = transform.position;

        float halfWidth = Mathf.Max(0f, deadZoneSize.x * 0.5f);
        float halfHeight = Mathf.Max(0f, deadZoneSize.y * 0.5f);

        float minX = current.x - halfWidth;
        float maxX = current.x + halfWidth;
        float minY = current.y - halfHeight;
        float maxY = current.y + halfHeight;

        if (desired.x > maxX)
        {
            current.x += desired.x - maxX;
        }
        else if (desired.x < minX)
        {
            current.x += desired.x - minX;
        }

        if (desired.y > maxY)
        {
            current.y += desired.y - maxY;
        }
        else if (desired.y < minY)
        {
            current.y += desired.y - minY;
        }

        current.z = cameraZ;
        return current;
    }

    private Vector3 GetTargetPosition()
    {
        if (target == null)
        {
            return transform.position;
        }

        if (preferRigidbodyPosition)
        {
            if (targetRigidbody == null || targetRigidbody.transform != target)
            {
                CacheTargetRigidbody();
            }

            if (targetRigidbody != null)
            {
                return targetRigidbody.position;
            }
        }

        return target.position;
    }

    private void CacheTargetRigidbody()
    {
        targetRigidbody = target != null ? target.GetComponent<Rigidbody2D>() : null;
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        CacheTargetRigidbody();
    }

    private void OnDrawGizmosSelected()
    {
        if (!useDeadZone)
        {
            return;
        }

        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.85f);
        Vector3 center = transform.position;
        center.z = 0f;

        Vector3 size = new Vector3(
            Mathf.Max(0f, deadZoneSize.x),
            Mathf.Max(0f, deadZoneSize.y),
            0f);

        Gizmos.DrawWireCube(center, size);
    }
}