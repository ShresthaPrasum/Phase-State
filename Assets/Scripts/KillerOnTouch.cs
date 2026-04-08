using UnityEngine;

public class KillerOnTouch : MonoBehaviour
{
    [Header("Detection")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private bool allowParentTagCheck = true;
    [SerializeField] private bool useTrigger = true;
    [SerializeField, Min(0f)] private float touchCooldown = 0.15f;

    private float cooldownTimer;

    private void Update()
    {
        if (cooldownTimer > 0f)
        {
            cooldownTimer -= Time.deltaTime;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!useTrigger)
        {
            return;
        }

        TryKill(other.gameObject);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (useTrigger)
        {
            return;
        }

        TryKill(collision.gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!useTrigger)
        {
            return;
        }

        TryKill(other.gameObject);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (useTrigger)
        {
            return;
        }

        TryKill(collision.gameObject);
    }

    private void TryKill(GameObject candidate)
    {
        if (cooldownTimer > 0f || !IsPlayer(candidate))
        {
            return;
        }

        PlayerInstabilityController controller = candidate.GetComponent<PlayerInstabilityController>();
        if (controller == null)
        {
            controller = candidate.GetComponentInParent<PlayerInstabilityController>();
        }

        if (controller == null)
        {
            return;
        }

        if (controller.GetCurrentState() != PlayerInstabilityController.PhaseState.Solid)
        {
            return;
        }

        cooldownTimer = touchCooldown;
        controller.TeleportToCheckpoint();
    }

    private bool IsPlayer(GameObject candidate)
    {
        if (candidate == null)
        {
            return false;
        }

        if (candidate.CompareTag(playerTag))
        {
            return true;
        }

        if (!allowParentTagCheck)
        {
            return false;
        }

        Transform current = candidate.transform.parent;
        while (current != null)
        {
            if (current.CompareTag(playerTag))
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }
}
