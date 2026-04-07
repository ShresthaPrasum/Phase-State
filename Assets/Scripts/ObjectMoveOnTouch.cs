using UnityEngine;

public class ObjectMoveOnTouch : MonoBehaviour
{
	public enum TouchAction
	{
		MoveObject,
		ToggleActiveState
	}

	public enum ActiveTouchMode
	{
		SetState,
		FlipState
	}

	[Header("Touch Filter")]
	[SerializeField] private string playerTag = "Player";

	[Header("Action")]
	[SerializeField] private TouchAction action = TouchAction.MoveObject;

	[Header("Move Settings")]
	[SerializeField] private Vector3 moveOffset = new Vector3(0f, 3f, 0f);
	[SerializeField] private float moveSpeed = 3f;
	[SerializeField] private bool moveInLocalSpace = false;
	[SerializeField] private bool moveOnce = true;
	[SerializeField] private bool loopMovement = false;

	[Header("Toggle Settings")]
	[SerializeField] private GameObject targetObject;
	[SerializeField] private ActiveTouchMode activeTouchMode = ActiveTouchMode.SetState;
	[SerializeField] private bool setActiveOnTouch = true;

	[Header("Reset")]
	[SerializeField] private bool resetOnPlayerExit = false;

	private Vector3 startPosition;
	private Vector3 worldMoveOffset;
	private Vector3 positiveLoopPosition;
	private Vector3 negativeLoopPosition;
	private Vector3 targetPosition;
	private bool hasMoved;
	private bool isMoving;
	private bool movingToPositive;

	private void Awake()
	{
		startPosition = transform.position;
		worldMoveOffset = moveInLocalSpace ? transform.TransformVector(moveOffset) : moveOffset;
		positiveLoopPosition = startPosition + worldMoveOffset;
		negativeLoopPosition = startPosition - worldMoveOffset;
		targetPosition = positiveLoopPosition;
		movingToPositive = true;

		if (targetObject == null)
		{
			targetObject = gameObject;
		}
	}

	private void Update()
	{
		if (!isMoving)
		{
			return;
		}

		transform.position = Vector3.MoveTowards(transform.position, targetPosition, moveSpeed * Time.deltaTime);

		if ((transform.position - targetPosition).sqrMagnitude <= 0.0001f)
		{
			transform.position = targetPosition;

			if (action == TouchAction.MoveObject && loopMovement)
			{
				if (movingToPositive)
				{
					targetPosition = negativeLoopPosition;
					movingToPositive = false;
				}
				else
				{
					targetPosition = positiveLoopPosition;
					movingToPositive = true;
				}

				isMoving = true;
				return;
			}

			isMoving = false;

			if (moveOnce)
			{
				hasMoved = true;
			}
		}
	}

	private void OnTriggerEnter(Collider other)
	{
		HandleTouch(other.gameObject);
	}

	private void OnTriggerEnter2D(Collider2D other)
	{
		HandleTouch(other.gameObject);
	}

	private void OnCollisionEnter(Collision collision)
	{
		HandleTouch(collision.gameObject);
	}

	private void OnCollisionEnter2D(Collision2D collision)
	{
		HandleTouch(collision.gameObject);
	}

	private void OnTriggerExit(Collider other)
	{
		if (!resetOnPlayerExit)
		{
			return;
		}

		if (IsPlayer(other.gameObject))
		{
			ResetAction();
		}
	}

	private void OnTriggerExit2D(Collider2D other)
	{
		if (!resetOnPlayerExit)
		{
			return;
		}

		if (IsPlayer(other.gameObject))
		{
			ResetAction();
		}
	}

	private void HandleTouch(GameObject touchedObject)
	{
		if (!IsPlayer(touchedObject))
		{
			return;
		}

		if (action == TouchAction.MoveObject)
		{
			worldMoveOffset = moveInLocalSpace ? transform.TransformVector(moveOffset) : moveOffset;
			positiveLoopPosition = startPosition + worldMoveOffset;
			negativeLoopPosition = startPosition - worldMoveOffset;

			if (loopMovement)
			{
				targetPosition = positiveLoopPosition;
				movingToPositive = true;
				isMoving = true;
				return;
			}

			if (moveOnce && hasMoved)
			{
				return;
			}

			targetPosition = positiveLoopPosition;

			isMoving = true;
			return;
		}

		if (targetObject != null)
		{
			if (activeTouchMode == ActiveTouchMode.FlipState)
			{
				targetObject.SetActive(!targetObject.activeSelf);
			}
			else
			{
				targetObject.SetActive(setActiveOnTouch);
			}
		}
	}

	private bool IsPlayer(GameObject candidate)
	{
		return candidate != null && candidate.CompareTag(playerTag);
	}

	private void ResetAction()
	{
		if (action == TouchAction.MoveObject)
		{
			transform.position = startPosition;
			targetPosition = positiveLoopPosition;
			movingToPositive = true;
			hasMoved = false;
			isMoving = false;
		}
	}
}
