using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(BlockOutHelper))]
public class BlockOutEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        
        BlockOutHelper swapper = (BlockOutHelper)target;
        
        // Add some space
        EditorGUILayout.Space(10);
        
        // Create a larger button
        GUIStyle buttonStyle = new GUIStyle(GUI.skin.button);
        buttonStyle.fontSize = 14;
        buttonStyle.fixedHeight = 40;
        
        // Display the swap button
        GUI.backgroundColor = Color.green;
        if (GUILayout.Button("Swap Selected Object", buttonStyle))
        {
            swapper.SwapSelectedObject();
        }
        GUI.backgroundColor = Color.white;
        
        // Display helpful information
        EditorGUILayout.Space(5);
        EditorGUILayout.HelpBox(
            "1. Assign a replacement model prefab above\n" +
            "2. Select a cube in the Hierarchy\n" +
            "3. Click 'Swap Selected Object'", 
            MessageType.Info);
    }
}