using UnityEngine;

public class SoftBodyMeshDeformer : MonoBehaviour
{
    [SerializeField] private SoftBodyGenerator softBodyGenerator;
    [SerializeField] private Color meshColor = Color.black;
    
    private Mesh deformingMesh;
    private Material meshMaterial;
    private Vector3[] vertices;
    private int[] triangles;

    private void Start()
    {
        if (softBodyGenerator == null)
        {
            Debug.LogError("SoftBodyMeshDeformer: SoftBodyGenerator not assigned!");
            return;
        }

        CreateBlobMesh();
        SetupMaterial();
    }

    private void SetupMaterial()
    {
        meshMaterial = new Material(Shader.Find("Sprites/Default"));
        meshMaterial.color = meshColor;
    }

    private void CreateBlobMesh()
    {
        Vector3[] bonePositions = softBodyGenerator.GetBonePositions();
        if (bonePositions == null || bonePositions.Length < 3)
        {
            Debug.LogError("SoftBodyMeshDeformer: Invalid bone positions!");
            return;
        }

        int boneCount = bonePositions.Length;

        vertices = new Vector3[boneCount + 1];
        vertices[0] = softBodyGenerator.transform.position;

        for (int i = 0; i < boneCount; i++)
        {
            vertices[i + 1] = bonePositions[i];
        }

        triangles = new int[boneCount * 3];
        for (int i = 0; i < boneCount; i++)
        {
            int nextI = (i + 1) % boneCount;
            
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = nextI + 1;
            triangles[i * 3 + 2] = i + 1;
        }

        deformingMesh = new Mesh();
        deformingMesh.name = "SoftBodyMesh";
        deformingMesh.vertices = vertices;
        deformingMesh.triangles = triangles;
        deformingMesh.RecalculateNormals();
        deformingMesh.RecalculateBounds();

        Debug.Log($"SoftBodyMeshDeformer: Mesh created with {boneCount} bones");
    }

    private void LateUpdate()
    {
        if (deformingMesh == null || softBodyGenerator == null || meshMaterial == null)
        {
            return;
        }

        meshMaterial.color = meshColor;

        Vector3[] bonePositions = softBodyGenerator.GetBonePositions();
        if (bonePositions == null || bonePositions.Length < 3)
        {
            return;
        }

        if (vertices == null || vertices.Length != bonePositions.Length + 1)
        {
            CreateBlobMesh();
            if (deformingMesh == null)
            {
                return;
            }
        }

        vertices[0] = softBodyGenerator.transform.position;

        for (int i = 0; i < bonePositions.Length; i++)
        {
            vertices[i + 1] = bonePositions[i];
        }

        deformingMesh.vertices = vertices;
        deformingMesh.RecalculateNormals();
        deformingMesh.RecalculateBounds();

        Graphics.DrawMesh(deformingMesh, Matrix4x4.identity, meshMaterial, 0);
    }

    private void OnDestroy()
    {
        if (deformingMesh != null)
            Destroy(deformingMesh);
        if (meshMaterial != null)
            Destroy(meshMaterial);
    }
}
