#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(SplineDrawingTool))]
public class SplineDrawingToolEditor : Editor
{
    private SplineDrawingTool tool;
    private bool isDrawMode = false;

    void OnEnable()
    {
        tool = (SplineDrawingTool)target;
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Drawing Controls", EditorStyles.boldLabel);

        if (GUILayout.Button(isDrawMode ? "Exit Draw Mode" : "Enter Draw Mode"))
        {
            isDrawMode = !isDrawMode;
            Debug.Log("Draw Mode: " + (isDrawMode ? "ENABLED" : "DISABLED"));
            SceneView.RepaintAll();
        }

        if (GUILayout.Button("Clear Spline"))
        {
            tool.ClearSpline();
        }

        if (isDrawMode)
        {
            EditorGUILayout.HelpBox("Draw Mode Active! Click and drag in Scene view to draw.", MessageType.Info);
        }
    }

    void OnSceneGUI()
    {
        if (!isDrawMode)
        {
            return;
        }

        // Take control of the scene view
        int controlID = GUIUtility.GetControlID(FocusType.Passive);
        HandleUtility.AddDefaultControl(controlID);

        Event e = Event.current;
        
        // Visual feedback that we're in draw mode
        Handles.BeginGUI();
        GUIStyle style = new GUIStyle();
        style.normal.textColor = Color.green;
        style.fontSize = 20;
        style.fontStyle = FontStyle.Bold;
        GUI.Label(new Rect(10, 10, 300, 30), "DRAW MODE ACTIVE", style);
        Handles.EndGUI();

        if (e.type == EventType.MouseDown && e.button == 0)
        {
            Debug.Log("Mouse Down detected!");
            
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            Debug.Log($"Raycast from: {ray.origin}, Direction: {ray.direction}");
            Debug.Log($"Ground Layer Mask: {tool.groundLayer.value}");
            
            if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, tool.groundLayer))
            {
                Debug.Log($"Hit ground at: {hit.point}, Object: {hit.collider.gameObject.name}");
                Vector3 point = hit.point;
                point.y += tool.drawHeight;
                tool.StartDrawing(point);
                e.Use();
            }
            else
            {
                Debug.LogWarning("Raycast did not hit anything on Ground layer!");
                
                // Try without layer mask to see what we're hitting
                if (Physics.Raycast(ray, out RaycastHit anyHit, Mathf.Infinity))
                {
                    Debug.Log($"But raycast hit: {anyHit.collider.gameObject.name} on layer: {LayerMask.LayerToName(anyHit.collider.gameObject.layer)}");
                }
            }
        }
        else if (e.type == EventType.MouseDrag && e.button == 0)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            
            if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, tool.groundLayer))
            {
                Vector3 point = hit.point;
                point.y += tool.drawHeight;
                tool.ContinueDrawing(point);
                e.Use();
            }
        }
        else if (e.type == EventType.MouseUp && e.button == 0)
        {
            Debug.Log("Mouse Up - Ending drawing");
            tool.EndDrawing();
            e.Use();
        }

        SceneView.RepaintAll();
    }
}
#endif