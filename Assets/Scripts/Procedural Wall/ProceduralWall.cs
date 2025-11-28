using UnityEngine;
using UnityEngine.Splines;
using System.Collections.Generic;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class ProceduralWall : MonoBehaviour
{
    [Header("Spline Settings")]
    public SplineContainer splineContainer;
    
    [Header("Wall Settings")]
    public WallProfile wallProfile;
    [Range(0.1f, 2f)]
    public float segmentLength = 0.5f;
    
    [Header("Materials")]
    public Material wallMaterial;
    
    [Header("UV Settings")]
    public float uvScale = 1f;
    
    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;
    private Mesh mesh;

    void Awake()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();
        
        if (wallMaterial != null)
            meshRenderer.material = wallMaterial;
    }

    void Start()
    {
        GenerateWall();
    }

    public void GenerateWall()
    {
        if (splineContainer == null || wallProfile == null)
        {
            Debug.LogWarning("SplineContainer or WallProfile is missing!");
            return;
        }

        if (mesh == null)
        {
            mesh = new Mesh();
            mesh.name = "Procedural Wall";
            meshFilter.mesh = mesh;
        }

        mesh.Clear();

        Spline spline = splineContainer.Spline;
        
        if (spline.Count < 2)
        {
            Debug.LogWarning("Spline needs at least 2 points!");
            return;
        }

        GenerateMesh(spline);
    }

    void GenerateMesh(Spline spline)
    {
        List<Vector3> vertices = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> triangles = new List<int>();

        float splineLength = spline.GetLength();
        int segmentCount = Mathf.Max(2, Mathf.CeilToInt(splineLength / segmentLength));
        
        float accumulatedDistance = 0f;
        Vector3 previousPosition = Vector3.zero;

        // Generate cross-sections along the spline
        for (int i = 0; i <= segmentCount; i++)
        {
            float t = (float)i / segmentCount;
            
            // Get position and direction at this point on the spline
            Vector3 position = spline.EvaluatePosition(t);
            Vector3 tangent = spline.EvaluateTangent(t);
            Vector3 forward = tangent.normalized;
            
            // Calculate accumulated distance for UV mapping
            if (i > 0)
            {
                accumulatedDistance += Vector3.Distance(position, previousPosition);
            }
            previousPosition = position;

            // Calculate perpendicular vector for cross-section orientation
            Vector3 up = spline.EvaluateUpVector(t);
            Vector3 right = Vector3.Cross(forward, up).normalized;

            // Create vertices for this cross-section
            int vertexOffset = vertices.Count;
            
            foreach (var point in wallProfile.points)
            {
                Vector3 offset = right * point.position.x + up * point.position.y;
                Vector3 vertexPosition = position + offset;
                vertices.Add(vertexPosition);

                // Calculate UVs
                float u = point.uv.x;
                float v = (accumulatedDistance * uvScale);
                uvs.Add(new Vector2(u, v));
            }

            // Create triangles connecting to previous cross-section
            if (i > 0)
            {
                int prevVertexOffset = vertexOffset - wallProfile.points.Length;
                
                for (int j = 0; j < wallProfile.points.Length; j++)
                {
                    int nextJ = (j + 1) % wallProfile.points.Length;
                    
                    // Create two triangles for each quad
                    // Triangle 1
                    triangles.Add(prevVertexOffset + j);
                    triangles.Add(vertexOffset + j);
                    triangles.Add(vertexOffset + nextJ);
                    
                    // Triangle 2
                    triangles.Add(prevVertexOffset + j);
                    triangles.Add(vertexOffset + nextJ);
                    triangles.Add(prevVertexOffset + nextJ);
                }
            }
        }

        // Assign to mesh
        mesh.vertices = vertices.ToArray();
        mesh.uv = uvs.ToArray();
        mesh.triangles = triangles.ToArray();
        
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }

    // Call this when spline changes
    public void OnSplineChanged()
    {
        GenerateWall();
    }
}