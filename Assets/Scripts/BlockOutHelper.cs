using UnityEngine;
using UnityEditor;

public class BlockOutHelper : MonoBehaviour
{
    [Header("Replacement Model")]
    [Tooltip("Drag your replacement model prefab here")]
    public GameObject replacementPrefab;
    
    [Header("Selected Object")]
    [Tooltip("This will show the currently selected object")]
    public GameObject selectedObject;
    
    private void OnValidate()
    {
        // Update selected object reference when selection changes in editor
        #if UNITY_EDITOR
        if (Selection.activeGameObject != null)
        {
            selectedObject = Selection.activeGameObject;
        }
        #endif
    }
    
    public void SwapSelectedObject()
    {
        #if UNITY_EDITOR
        // Get the currently selected object
        selectedObject = Selection.activeGameObject;
        #endif
        
        if (selectedObject == null)
        {
            Debug.LogWarning("No object selected! Please select a cube in the hierarchy.");
            return;
        }
        
        if (replacementPrefab == null)
        {
            Debug.LogError("No replacement prefab assigned! Please assign a model in the inspector.");
            return;
        }
        
        // Store the original object's transform data
        Vector3 originalPosition = selectedObject.transform.position;
        Quaternion originalRotation = selectedObject.transform.rotation;
        Vector3 originalScale = selectedObject.transform.localScale;
        Transform originalParent = selectedObject.transform.parent;
        
        // Get the bounds of the original object
        Bounds originalBounds = GetObjectBounds(selectedObject);
        
        // Instantiate the replacement object
        GameObject newObject = Instantiate(replacementPrefab, originalPosition, originalRotation, originalParent);
        newObject.name = replacementPrefab.name;
        
        // Calculate and apply the constrained scale
        Vector3 newScale = CalculateConstrainedScale(newObject, originalBounds, originalScale);
        newObject.transform.localScale = newScale;
        
        // Destroy the original object
        DestroyImmediate(selectedObject);
        
        // Select the new object
        #if UNITY_EDITOR
        Selection.activeGameObject = newObject;
        #endif
        
        Debug.Log($"Successfully swapped object! New scale: {newScale}");
    }
    
    private Bounds GetObjectBounds(GameObject obj)
    {
        Renderer renderer = obj.GetComponent<Renderer>();
        if (renderer != null)
        {
            return renderer.bounds;
        }
        
        // If no renderer, use collider bounds
        Collider collider = obj.GetComponent<Collider>();
        if (collider != null)
        {
            return collider.bounds;
        }
        
        // Fallback: create bounds from transform scale
        return new Bounds(obj.transform.position, obj.transform.localScale);
    }
    
    private Vector3 CalculateConstrainedScale(GameObject newObject, Bounds targetBounds, Vector3 originalScale)
    {
        // Get the bounds of the new object at scale (1,1,1)
        newObject.transform.localScale = Vector3.one;
        Bounds newObjectBounds = GetObjectBounds(newObject);
        
        if (newObjectBounds.size == Vector3.zero)
        {
            Debug.LogWarning("New object has no bounds, using original scale");
            return originalScale;
        }
        
        // Calculate the scale factor needed for each axis
        float scaleX = targetBounds.size.x / newObjectBounds.size.x;
        float scaleY = targetBounds.size.y / newObjectBounds.size.y;
        float scaleZ = targetBounds.size.z / newObjectBounds.size.z;
        
        // Use the smallest scale factor to ensure the object fits within bounds
        float uniformScale = Mathf.Min(scaleX, scaleY, scaleZ);
        
        return new Vector3(uniformScale, uniformScale, uniformScale);
    }
}