using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class SoftBodyVisualizer : MonoBehaviour
{
    [SerializeField] private SoftBodyGenerator softBodyGenerator;
    [SerializeField] private LineRenderer outlineRenderer;
    [SerializeField] private bool renderFill = true;
    [SerializeField] private Color fillColor = Color.black;
    [SerializeField] private int smoothnessPerEdge = 5;
    [SerializeField] private float outlineWidth = 0.15f;
    
    private Vector3[] bonePositions;
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Mesh fillMesh;
    private Material fillMaterial;
    private Vector3[] smoothPoints = new Vector3[0];

    private void Awake()
    {
        EnsureLineRenderer();
        EnsureFillComponents();
        SetupLineRenderer();
        SetupFillRenderer();
    }

    private void Reset()
    {
        EnsureLineRenderer();
        EnsureFillComponents();
        SetupLineRenderer();
        SetupFillRenderer();
    }

    private void OnValidate()
    {
        EnsureLineRenderer();
        EnsureFillComponents();
        SetupLineRenderer();
        SetupFillRenderer();
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

    private void EnsureFillComponents()
    {
        meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null)
        {
            meshFilter = gameObject.AddComponent<MeshFilter>();
        }

        meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer == null)
        {
            meshRenderer = gameObject.AddComponent<MeshRenderer>();
        }

        if (fillMesh == null)
        {
            fillMesh = new Mesh { name = "SoftBodyFillMesh" };
        }

        meshFilter.sharedMesh = fillMesh;
    }

    private void SetupLineRenderer()
    {
        if (outlineRenderer == null)
        {
            return;
        }

        if (outlineRenderer.material == null)
        {
            outlineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        }

        Color start = outlineRenderer.startColor;
        start.a = 1f;
        outlineRenderer.startColor = start;

        Color end = outlineRenderer.endColor;
        end.a = 1f;
        outlineRenderer.endColor = end;

        outlineRenderer.positionCount = 0;
        outlineRenderer.loop = true;
        outlineRenderer.useWorldSpace = true;
        outlineRenderer.startWidth = outlineWidth;
        outlineRenderer.endWidth = outlineWidth;
        outlineRenderer.numCornerVertices = 6;
        outlineRenderer.numCapVertices = 6;
    }

    private void SetupFillRenderer()
    {
        if (meshRenderer == null)
        {
            return;
        }

        if (fillMaterial == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }
            fillMaterial = new Material(shader);
        }

        Color opaqueFill = fillColor;
        opaqueFill.a = 1f;
        fillMaterial.color = opaqueFill;
        meshRenderer.sharedMaterial = fillMaterial;
        meshRenderer.enabled = renderFill;

        if (outlineRenderer != null)
        {
            outlineRenderer.sortingOrder = meshRenderer.sortingOrder + 1;
        }
    }

    private void LateUpdate()
    {
        if (softBodyGenerator == null) return;

        bonePositions = softBodyGenerator.GetBonePositions();
        if (bonePositions == null || bonePositions.Length < 3)
        {
            return;
        }

        smoothPoints = BuildSmoothLoop(bonePositions, Mathf.Max(1, smoothnessPerEdge));
        UpdateOutline();
        UpdateFillMesh();
    }

    private void UpdateOutline()
    {
        if (outlineRenderer == null || smoothPoints == null || smoothPoints.Length < 3)
        {
            return;
        }

        outlineRenderer.positionCount = smoothPoints.Length;
        for (int i = 0; i < smoothPoints.Length; i++)
        {
            outlineRenderer.SetPosition(i, smoothPoints[i]);
        }
    }

    private void UpdateFillMesh()
    {
        if (!renderFill || fillMesh == null || smoothPoints == null || smoothPoints.Length < 3)
        {
            if (fillMesh != null)
            {
                fillMesh.Clear();
            }
            return;
        }

        Vector3 center = Vector3.zero;
        for (int i = 0; i < smoothPoints.Length; i++)
        {
            center += smoothPoints[i];
        }
        center /= smoothPoints.Length;

        int pointCount = smoothPoints.Length;
        Vector3[] vertices = new Vector3[pointCount + 1];
        int[] triangles = new int[pointCount * 3];

        vertices[0] = transform.InverseTransformPoint(center);
        for (int i = 0; i < pointCount; i++)
        {
            vertices[i + 1] = transform.InverseTransformPoint(smoothPoints[i]);
        }

        for (int i = 0; i < pointCount; i++)
        {
            int next = (i + 1) % pointCount;
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = next + 1;
            triangles[i * 3 + 2] = i + 1;
        }

        fillMesh.Clear();
        fillMesh.vertices = vertices;
        fillMesh.triangles = triangles;
        fillMesh.RecalculateBounds();
        fillMesh.RecalculateNormals();
    }

    private Vector3[] BuildSmoothLoop(Vector3[] controlPoints, int subdivisions)
    {
        int count = controlPoints.Length;
        Vector3[] result = new Vector3[count * subdivisions];
        int index = 0;

        for (int i = 0; i < count; i++)
        {
            Vector3 p0 = controlPoints[(i - 1 + count) % count];
            Vector3 p1 = controlPoints[i];
            Vector3 p2 = controlPoints[(i + 1) % count];
            Vector3 p3 = controlPoints[(i + 2) % count];

            for (int s = 0; s < subdivisions; s++)
            {
                float t = (float)s / subdivisions;
                result[index++] = CatmullRom(p0, p1, p2, p3, t);
            }
        }

        return result;
    }

    private Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;
        return 0.5f *
               ((2f * p1) +
                (-p0 + p2) * t +
                (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
                (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }

    private void OnDestroy()
    {
        if (fillMesh != null)
        {
            Destroy(fillMesh);
        }

        if (fillMaterial != null)
        {
            Destroy(fillMaterial);
        }
    }
}
