using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class SoftBodyVisualizer : MonoBehaviour
{
    [SerializeField] private SoftBodyGenerator softBodyGenerator;
    [SerializeField] private LineRenderer outlineRenderer;
    
    private Vector3[] bonePositions;
    private Vector3 centerPos;

    private void Awake()
    {
        EnsureLineRenderer();
        SetupLineRenderer();
    }

    private void Reset()
    {
        EnsureLineRenderer();
        SetupLineRenderer();
    }

    private void OnValidate()
    {
        EnsureLineRenderer();
        SetupLineRenderer();
    }

    private void EnsureLineRenderer()
    {
        if (outlineRenderer == null)
        {
            outlineRenderer = GetComponent<LineRenderer>();
        }

        if (outlineRenderer == null)
        {
            outlineRenderer = gameObject.AddComponent<LineRenderer>();
        }
    }

    private void SetupLineRenderer()
    {
        if (outlineRenderer == null)
        {
            return;
        }

        outlineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        outlineRenderer.startColor = Color.white;
        outlineRenderer.endColor = Color.white;
        outlineRenderer.startWidth = 0.15f;
        outlineRenderer.endWidth = 0.15f;
        outlineRenderer.positionCount = 0;
        outlineRenderer.loop = true;
        outlineRenderer.sortingOrder = 1;
        outlineRenderer.useWorldSpace = true;
    }

    private void LateUpdate()
    {
        if (softBodyGenerator == null) return;

        bonePositions = softBodyGenerator.GetBonePositions();
        centerPos = softBodyGenerator.transform.position;

        int requiredPoints = bonePositions.Length + 1;
        if (outlineRenderer.positionCount != requiredPoints)
        {
            outlineRenderer.positionCount = requiredPoints;
        }

        for (int i = 0; i < bonePositions.Length; i++)
        {
            outlineRenderer.SetPosition(i, bonePositions[i]);
        }

        outlineRenderer.SetPosition(bonePositions.Length, bonePositions[0]);
    }
}
