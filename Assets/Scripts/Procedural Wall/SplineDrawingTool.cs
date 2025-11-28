using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class SplineDrawingTool : MonoBehaviour
{
    [Header("Drawing Settings")]
    public LayerMask groundLayer;
    public float pointSpacing = 1f;
    public float drawHeight = 0f;
    
    [Header("References")]
    public SplineContainer splineContainer;
    public ProceduralWall proceduralWall;
    
    [Header("Visualization")]
    public Color splineColor = Color.cyan;
    public float handleSize = 0.3f;
    
    private bool isDrawing = false;
    private Vector3 lastPoint;

    void Awake()
    {
        if (splineContainer == null)
        {
            splineContainer = GetComponent<SplineContainer>();
            if (splineContainer == null)
            {
                splineContainer = gameObject.AddComponent<SplineContainer>();
            }
        }

        // Initialize with empty spline
        if (splineContainer.Spline == null || splineContainer.Spline.Count == 0)
        {
            splineContainer.Spline = new Spline();
        }
    }

    public void StartDrawing(Vector3 worldPosition)
    {
        isDrawing = true;
        splineContainer.Spline.Clear();
        
        Vector3 localPos = splineContainer.transform.InverseTransformPoint(worldPosition);
        splineContainer.Spline.Add(new BezierKnot(localPos));
        lastPoint = worldPosition;
    }

    public void ContinueDrawing(Vector3 worldPosition)
    {
        if (!isDrawing) return;

        if (Vector3.Distance(worldPosition, lastPoint) >= pointSpacing)
        {
            Vector3 localPos = splineContainer.transform.InverseTransformPoint(worldPosition);
            splineContainer.Spline.Add(new BezierKnot(localPos));
            lastPoint = worldPosition;
            
            if (proceduralWall != null)
            {
                proceduralWall.OnSplineChanged();
            }
        }
    }

    public void EndDrawing()
    {
        isDrawing = false;
        
        if (proceduralWall != null)
        {
            proceduralWall.OnSplineChanged();
        }
    }

    public void ClearSpline()
    {
        splineContainer.Spline.Clear();
        
        if (proceduralWall != null)
        {
            proceduralWall.OnSplineChanged();
        }
    }

    void OnDrawGizmos()
    {
        if (splineContainer == null || splineContainer.Spline == null) return;

        Gizmos.color = splineColor;
        
        for (int i = 0; i < splineContainer.Spline.Count; i++)
        {
            Vector3 worldPos = splineContainer.transform.TransformPoint(splineContainer.Spline[i].Position);
            Gizmos.DrawSphere(worldPos, handleSize);
        }
    }
}