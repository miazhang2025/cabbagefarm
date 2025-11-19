using UnityEngine;
using System.Collections.Generic;
using System.IO;

public class Forest : MonoBehaviour
{
    [Header("Spawn Settings")]
    [Tooltip("Path to the folder containing FBX models (relative to Assets folder in editor, or Resources folder for runtime)")]
    public string folderPath = "Models";
    
    [Tooltip("Number of gameobjects to spawn")]
    public int spawnAmount = 10;
    
    [Header("Spawn Range")]
    [Tooltip("Minimum X position relative to this object")]
    public float minX = -10f;
    
    [Tooltip("Maximum X position relative to this object")]
    public float maxX = 10f;
    
    [Tooltip("Minimum Z position relative to this object")]
    public float minZ = -10f;
    
    [Tooltip("Maximum Z position relative to this object")]
    public float maxZ = 10f;
    
    [Header("Raycast Settings")]
    [Tooltip("Maximum distance to cast ray downward for surface detection")]
    public float maxRaycastDistance = 100f;
    
    [Tooltip("Layer mask for surface detection (leave empty to hit all layers)")]
    public LayerMask surfaceLayerMask = -1;
    
    [Tooltip("Offset above surface when placing objects")]
    public float surfaceOffset = 0f;
    
    [Header("Parent Settings")]
    [Tooltip("Optional parent transform for spawned objects")]
    public Transform parentTransform;
    
    private List<GameObject> spawnedObjects = new List<GameObject>();
    private Dictionary<GameObject, float> modelLowestYCache = new Dictionary<GameObject, float>();
    
    void Start()
    {
        SpawnObjects();
    }
    
    public void SpawnObjects()
    {
        // Clear previously spawned objects
        ClearSpawnedObjects();
        
        // Load models from folder
        GameObject[] models = LoadModelsFromFolder();
        
        if (models == null || models.Length == 0)
        {
            Debug.LogWarning($"No models found in folder: {folderPath}");
            return;
        }
        
        // Get the upper plane Y position of this object
        float upperPlaneY = GetUpperPlaneY();
        
        // Spawn objects
        for (int i = 0; i < spawnAmount; i++)
        {
            // Pick a random model
            GameObject modelPrefab = models[Random.Range(0, models.Length)];
            
            // Generate random position in X,Z range
            float randomX = Random.Range(minX, maxX);
            float randomZ = Random.Range(minZ, maxZ);
            Vector3 spawnPosition = transform.position + new Vector3(randomX, 0, randomZ);
            
            // Find surface above this object
            float surfaceY = FindSurfaceY(spawnPosition);
            
            // Get the lowest Y point of the model
            float modelLowestY = GetModelLowestY(modelPrefab);
            
            // Calculate position so the model's lowest vertex aligns with the surface
            // The spawn position Y should be: surfaceY - modelLowestY
            Vector3 finalPosition = new Vector3(spawnPosition.x, surfaceY - modelLowestY + surfaceOffset, spawnPosition.z);
            
            // Spawn the object
            GameObject spawnedObject = Instantiate(modelPrefab, finalPosition, Quaternion.identity);
            
            // Set parent if specified
            if (parentTransform != null)
            {
                spawnedObject.transform.SetParent(parentTransform);
            }
            else
            {
                spawnedObject.transform.SetParent(transform);
            }
            
            spawnedObjects.Add(spawnedObject);
        }
        
        Debug.Log($"Spawned {spawnedObjects.Count} objects from {models.Length} models");
    }
    
    private GameObject[] LoadModelsFromFolder()
    {
        List<GameObject> models = new List<GameObject>();
        
        #if UNITY_EDITOR
        // In editor, use AssetDatabase to load FBX files
        string fullPath = Path.Combine("Assets", folderPath);
        if (!Directory.Exists(fullPath))
        {
            // Try Resources path
            fullPath = Path.Combine("Assets", "Resources", folderPath);
        }
        
        if (Directory.Exists(fullPath))
        {
            // Search for Model assets (FBX files are imported as Model assets)
            string[] modelGuids = UnityEditor.AssetDatabase.FindAssets("t:Model", new[] { fullPath });
            foreach (string guid in modelGuids)
            {
                string assetPath = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                // Check if it's an FBX file
                string extension = Path.GetExtension(assetPath).ToLower();
                if (extension == ".fbx")
                {
                    // Load the model and create a GameObject from it
                    GameObject model = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                    if (model != null)
                    {
                        models.Add(model);
                    }
                }
            }
            
            // Also search for GameObject assets (in case FBX is imported as GameObject)
            if (models.Count == 0)
            {
                string[] gameObjectGuids = UnityEditor.AssetDatabase.FindAssets("t:GameObject", new[] { fullPath });
                foreach (string guid in gameObjectGuids)
                {
                    string assetPath = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                    string extension = Path.GetExtension(assetPath).ToLower();
                    if (extension == ".fbx")
                    {
                        GameObject model = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                        if (model != null)
                        {
                            models.Add(model);
                        }
                    }
                }
            }
        }
        #endif
        
        // Also try Resources.LoadAll for runtime (FBX files in Resources folder)
        if (models.Count == 0)
        {
            GameObject[] resourcesModels = Resources.LoadAll<GameObject>(folderPath);
            if (resourcesModels != null && resourcesModels.Length > 0)
            {
                models.AddRange(resourcesModels);
            }
        }
        
        return models.ToArray();
    }
    
    private float GetUpperPlaneY()
    {

        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            return col.bounds.max.y;
        }
        
        Renderer renderer = GetComponent<Renderer>();
        if (renderer != null)
        {
            return renderer.bounds.max.y;
        }
        
        Renderer[] childRenderers = GetComponentsInChildren<Renderer>();
        if (childRenderers.Length > 0)
        {
            Bounds combinedBounds = childRenderers[0].bounds;
            for (int i = 1; i < childRenderers.Length; i++)
            {
                combinedBounds.Encapsulate(childRenderers[i].bounds);
            }
            return combinedBounds.max.y;
        }
        
        return transform.position.y;
    }
    
    private float FindSurfaceY(Vector3 position)
    {
        // Cast ray downward from above the spawn position to find the upper plane of this object
        Vector3 rayStart = position + Vector3.up * maxRaycastDistance;
        RaycastHit hit;
        
        if (Physics.Raycast(rayStart, Vector3.down, out hit, maxRaycastDistance * 2f, surfaceLayerMask))
        {
            // Check if we hit this object or its children
            Transform hitTransform = hit.transform;
            while (hitTransform != null)
            {
                if (hitTransform == transform)
                {
                    // We hit this object, return the hit point Y
                    return hit.point.y;
                }
                hitTransform = hitTransform.parent;
            }
        }
        
        // If no surface found, use the upper plane Y of this object
        return GetUpperPlaneY();
    }
    
    private float GetModelLowestY(GameObject modelPrefab)
    {
        // Check cache first
        if (modelLowestYCache.ContainsKey(modelPrefab))
        {
            return modelLowestYCache[modelPrefab];
        }
        
        // Calculate the lowest Y point of the model relative to its pivot
        // We'll create a temporary instance at origin to get accurate bounds
        GameObject tempInstance = Instantiate(modelPrefab, Vector3.zero, Quaternion.identity);
        tempInstance.SetActive(false);
        
        // Get combined bounds of all renderers
        Renderer[] renderers = tempInstance.GetComponentsInChildren<Renderer>();
        Bounds combinedBounds = new Bounds();
        bool boundsInitialized = false;
        
        foreach (Renderer renderer in renderers)
        {
            if (!boundsInitialized)
            {
                combinedBounds = renderer.bounds;
                boundsInitialized = true;
            }
            else
            {
                combinedBounds.Encapsulate(renderer.bounds);
            }
        }
        
        // Calculate lowest Y relative to the model's pivot
        // Since tempInstance is at origin, bounds.min.y is the lowest Y relative to pivot
        float lowestY = 0f;
        if (boundsInitialized)
        {
            lowestY = combinedBounds.min.y;
        }
        
        // Clean up temporary instance
        #if UNITY_EDITOR
        DestroyImmediate(tempInstance);
        #else
        Destroy(tempInstance);
        #endif
        
        // Cache the result
        modelLowestYCache[modelPrefab] = lowestY;
        
        return lowestY;
    }
    
    public void ClearSpawnedObjects()
    {
        foreach (GameObject obj in spawnedObjects)
        {
            if (obj != null)
            {
                #if UNITY_EDITOR
                DestroyImmediate(obj);
                #else
                Destroy(obj);
                #endif
            }
        }
        spawnedObjects.Clear();
    }
    
    void OnDestroy()
    {
        ClearSpawnedObjects();
    }
    
    // Draw gizmos in editor to visualize spawn range
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        
        // Draw spawn area
        Vector3 center = transform.position;
        Vector3 size = new Vector3(maxX - minX, 0.1f, maxZ - minZ);
        Vector3 position = center + new Vector3((minX + maxX) / 2f, 0, (minZ + maxZ) / 2f);
        
        Gizmos.DrawWireCube(position, size);
        
        // Draw corners
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(center + new Vector3(minX, 0, minZ), 0.5f);
        Gizmos.DrawSphere(center + new Vector3(maxX, 0, minZ), 0.5f);
        Gizmos.DrawSphere(center + new Vector3(minX, 0, maxZ), 0.5f);
        Gizmos.DrawSphere(center + new Vector3(maxX, 0, maxZ), 0.5f);
    }
}
