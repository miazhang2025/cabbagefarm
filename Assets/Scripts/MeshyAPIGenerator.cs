using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using TMPro;

[Serializable]
public class MeshyTaskResponse
{
    public string result;
}

[Serializable]
public class MeshyTaskStatus
{
    public string id;
    public string status;
    public int progress;
    public ModelUrls model_urls;
    public List<TextureUrls> texture_urls;
}

[Serializable]
public class ModelUrls
{
    public string glb;
    public string fbx;
    public string obj;
    public string mtl;
}

[Serializable]
public class TextureUrls
{
    public string base_color;
    public string metallic;
    public string normal;
    public string roughness;
}

public class MeshyAPIGenerator : MonoBehaviour
{
    [Header("API Settings")]
    [Tooltip("Your Meshy API Key (format: msy_...)")]
    public string apiKey = "msy_your_api_key_here";
    
    [Header("UI References")]
    public TMP_InputField promptInputField;
    public Button generateButton;
    public TMP_Text statusText;
    public Slider progressSlider;
    
    [Header("Generation Settings")]
    public string artStyle = "realistic"; // realistic, sculpture, cartoon, low-poly, etc.
    public string negativePrompt = "low quality, low resolution, low poly, ugly";
    public bool shouldRemesh = true;
    public bool enablePBR = true;
    
    [Header("Output Settings")]
    public string downloadFolderPath = "Assets/Generated3DModels";
    public Vector3 spawnPosition = Vector3.zero;
    public Vector3 spawnRotation = Vector3.zero;
    public float spawnScale = 1f;
    
    private const string API_BASE_URL = "https://api.meshy.ai/openapi/v2";
    private bool isGenerating = false;

    void Start()
    {
        // Ensure download folder exists
        if (!Directory.Exists(downloadFolderPath))
        {
            Directory.CreateDirectory(downloadFolderPath);
            Debug.Log($"Created download folder: {downloadFolderPath}");
        }
        
        // Setup UI
        if (generateButton != null)
        {
            generateButton.onClick.AddListener(OnGenerateButtonClicked);
        }
        
        UpdateStatus("Ready to generate", 0);
    }

    public void OnGenerateButtonClicked()
    {
        if (isGenerating)
        {
            Debug.LogWarning("Generation already in progress!");
            return;
        }
        
        if (string.IsNullOrEmpty(apiKey) || apiKey == "msy_your_api_key_here")
        {
            UpdateStatus("Error: Please set your API key!", 0);
            Debug.LogError("API Key not set! Please add your Meshy API key in the inspector.");
            return;
        }
        
        if (promptInputField == null || string.IsNullOrEmpty(promptInputField.text))
        {
            UpdateStatus("Error: Please enter a prompt!", 0);
            Debug.LogError("Prompt is empty!");
            return;
        }
        
        StartCoroutine(GenerateModelWorkflow(promptInputField.text));
    }

    IEnumerator GenerateModelWorkflow(string prompt)
    {
        isGenerating = true;
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string modelName = SanitizeFileName(prompt.Substring(0, Math.Min(30, prompt.Length))) + "_" + timestamp;
        
        // Step 1: Create Preview Task
        UpdateStatus("Creating preview task...", 5);
        string previewTaskId = null;
        yield return StartCoroutine(CreatePreviewTask(prompt, (taskId) => previewTaskId = taskId));
        
        if (string.IsNullOrEmpty(previewTaskId))
        {
            UpdateStatus("Error: Failed to create preview task", 0);
            isGenerating = false;
            yield break;
        }
        
        Debug.Log($"Preview task created: {previewTaskId}");
        
        // Step 2: Poll Preview Task
        UpdateStatus("Generating preview model...", 10);
        MeshyTaskStatus previewTask = null;
        yield return StartCoroutine(PollTaskStatus(previewTaskId, (task) => previewTask = task, 10, 45));
        
        if (previewTask == null || previewTask.status != "SUCCEEDED")
        {
            UpdateStatus("Error: Preview generation failed", 0);
            isGenerating = false;
            yield break;
        }
        
        Debug.Log("Preview model completed!");
        
        // Step 3: Create Refine Task
        UpdateStatus("Creating refine task...", 50);
        string refineTaskId = null;
        yield return StartCoroutine(CreateRefineTask(previewTaskId, (taskId) => refineTaskId = taskId));
        
        if (string.IsNullOrEmpty(refineTaskId))
        {
            UpdateStatus("Error: Failed to create refine task", 0);
            isGenerating = false;
            yield break;
        }
        
        Debug.Log($"Refine task created: {refineTaskId}");
        
        // Step 4: Poll Refine Task
        UpdateStatus("Refining and texturing model...", 55);
        MeshyTaskStatus refineTask = null;
        yield return StartCoroutine(PollTaskStatus(refineTaskId, (task) => refineTask = task, 55, 85));
        
        if (refineTask == null || refineTask.status != "SUCCEEDED")
        {
            UpdateStatus("Error: Refine generation failed", 0);
            isGenerating = false;
            yield break;
        }
        
        Debug.Log("Refined model completed!");
        
        // Step 5: Download Model
        UpdateStatus("Downloading model...", 85);
        string modelPath = Path.Combine(downloadFolderPath, modelName + ".glb");
        yield return StartCoroutine(DownloadFile(refineTask.model_urls.glb, modelPath));
        
        Debug.Log($"Model downloaded to: {modelPath}");
        
        // Step 6: Download Textures
        UpdateStatus("Downloading textures...", 90);
        List<string> texturePaths = new List<string>();
        if (refineTask.texture_urls != null && refineTask.texture_urls.Count > 0)
        {
            var textures = refineTask.texture_urls[0];
            
            if (!string.IsNullOrEmpty(textures.base_color))
            {
                string path = Path.Combine(downloadFolderPath, modelName + "_BaseColor.png");
                yield return StartCoroutine(DownloadFile(textures.base_color, path));
                texturePaths.Add(path);
            }
            
            if (!string.IsNullOrEmpty(textures.normal))
            {
                string path = Path.Combine(downloadFolderPath, modelName + "_Normal.png");
                yield return StartCoroutine(DownloadFile(textures.normal, path));
                texturePaths.Add(path);
            }
            
            if (!string.IsNullOrEmpty(textures.metallic))
            {
                string path = Path.Combine(downloadFolderPath, modelName + "_Metallic.png");
                yield return StartCoroutine(DownloadFile(textures.metallic, path));
                texturePaths.Add(path);
            }
            
            if (!string.IsNullOrEmpty(textures.roughness))
            {
                string path = Path.Combine(downloadFolderPath, modelName + "_Roughness.png");
                yield return StartCoroutine(DownloadFile(textures.roughness, path));
                texturePaths.Add(path);
            }
        }
        
        Debug.Log($"Downloaded {texturePaths.Count} textures");
        
        // Step 7: Import and Setup in Scene
        UpdateStatus("Importing model to scene...", 95);
        yield return StartCoroutine(ImportModelToScene(modelPath, modelName, texturePaths));
        
        UpdateStatus($"Complete! Model '{modelName}' imported to scene.", 100);
        isGenerating = false;
    }

    IEnumerator CreatePreviewTask(string prompt, Action<string> callback)
    {
        string url = $"{API_BASE_URL}/text-to-3d";
        
        string jsonData = JsonUtility.ToJson(new
        {
            mode = "preview",
            prompt = prompt,
            art_style = artStyle,
            negative_prompt = negativePrompt,
            should_remesh = shouldRemesh
        });
        
        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", $"Bearer {apiKey}");
            
            yield return request.SendWebRequest();
            
            if (request.result == UnityWebRequest.Result.Success)
            {
                MeshyTaskResponse response = JsonUtility.FromJson<MeshyTaskResponse>(request.downloadHandler.text);
                callback?.Invoke(response.result);
            }
            else
            {
                Debug.LogError($"Preview task creation failed: {request.error}\n{request.downloadHandler.text}");
                callback?.Invoke(null);
            }
        }
    }

    IEnumerator CreateRefineTask(string previewTaskId, Action<string> callback)
    {
        string url = $"{API_BASE_URL}/text-to-3d";
        
        string jsonData = JsonUtility.ToJson(new
        {
            mode = "refine",
            preview_task_id = previewTaskId,
            enable_pbr = enablePBR
        });
        
        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", $"Bearer {apiKey}");
            
            yield return request.SendWebRequest();
            
            if (request.result == UnityWebRequest.Result.Success)
            {
                MeshyTaskResponse response = JsonUtility.FromJson<MeshyTaskResponse>(request.downloadHandler.text);
                callback?.Invoke(response.result);
            }
            else
            {
                Debug.LogError($"Refine task creation failed: {request.error}\n{request.downloadHandler.text}");
                callback?.Invoke(null);
            }
        }
    }

    IEnumerator PollTaskStatus(string taskId, Action<MeshyTaskStatus> callback, int startProgress, int endProgress)
    {
        string url = $"{API_BASE_URL}/text-to-3d/{taskId}";
        MeshyTaskStatus taskStatus = null;
        
        while (true)
        {
            using (UnityWebRequest request = UnityWebRequest.Get(url))
            {
                request.SetRequestHeader("Authorization", $"Bearer {apiKey}");
                yield return request.SendWebRequest();
                
                if (request.result == UnityWebRequest.Result.Success)
                {
                    taskStatus = JsonUtility.FromJson<MeshyTaskStatus>(request.downloadHandler.text);
                    
                    // Update progress bar proportionally
                    float taskProgress = taskStatus.progress / 100f;
                    int currentProgress = Mathf.RoundToInt(startProgress + (endProgress - startProgress) * taskProgress);
                    UpdateStatus($"Status: {taskStatus.status} ({taskStatus.progress}%)", currentProgress);
                    
                    if (taskStatus.status == "SUCCEEDED")
                    {
                        callback?.Invoke(taskStatus);
                        yield break;
                    }
                    else if (taskStatus.status == "FAILED" || taskStatus.status == "EXPIRED")
                    {
                        Debug.LogError($"Task failed with status: {taskStatus.status}");
                        callback?.Invoke(null);
                        yield break;
                    }
                }
                else
                {
                    Debug.LogError($"Failed to poll task status: {request.error}");
                }
            }
            
            yield return new WaitForSeconds(5f); // Poll every 5 seconds
        }
    }

    IEnumerator DownloadFile(string url, string savePath)
    {
        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            yield return request.SendWebRequest();
            
            if (request.result == UnityWebRequest.Result.Success)
            {
                File.WriteAllBytes(savePath, request.downloadHandler.data);
                Debug.Log($"File downloaded: {savePath}");
            }
            else
            {
                Debug.LogError($"Failed to download file: {request.error}");
            }
        }
    }

    IEnumerator ImportModelToScene(string modelPath, string modelName, List<string> texturePaths)
    {
        // Note: GLB import in Unity requires GLTFUtility or similar package
        // This is a placeholder - you'll need to adapt based on your GLB importer
        
        #if UNITY_EDITOR
        // Refresh asset database to recognize new files
        UnityEditor.AssetDatabase.Refresh();
        
        // Wait a frame for import to complete
        yield return new WaitForSeconds(0.5f);
        
        // Try to load the model as an asset
        string assetPath = modelPath.Replace(Application.dataPath, "Assets");
        GameObject modelPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        
        if (modelPrefab != null)
        {
            // Instantiate the model
            GameObject instance = Instantiate(modelPrefab);
            instance.name = modelName;
            instance.transform.position = spawnPosition;
            instance.transform.eulerAngles = spawnRotation;
            instance.transform.localScale = Vector3.one * spawnScale;
            
            // Create and apply material with textures
            Material mat = CreateMaterialWithTextures(modelName, texturePaths);
            if (mat != null)
            {
                Renderer[] renderers = instance.GetComponentsInChildren<Renderer>();
                foreach (Renderer renderer in renderers)
                {
                    renderer.material = mat;
                }
            }
            
            Debug.Log($"Model instantiated in scene: {modelName}");
        }
        else
        {
            Debug.LogWarning($"Could not load model as asset. Manual import may be required.\nModel saved at: {modelPath}");
        }
        #else
        Debug.LogWarning("Model import to scene only works in Unity Editor. Model saved at: " + modelPath);
        yield return null;
        #endif
    }

    Material CreateMaterialWithTextures(string materialName, List<string> texturePaths)
    {
        Material mat = new Material(Shader.Find("Standard"));
        mat.name = materialName + "_Material";
        
        foreach (string texturePath in texturePaths)
        {
            #if UNITY_EDITOR
            string assetPath = texturePath.Replace(Application.dataPath, "Assets");
            Texture2D texture = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            
            if (texture != null)
            {
                if (texturePath.Contains("BaseColor"))
                {
                    mat.mainTexture = texture;
                }
                else if (texturePath.Contains("Normal"))
                {
                    mat.SetTexture("_BumpMap", texture);
                }
                else if (texturePath.Contains("Metallic"))
                {
                    mat.SetTexture("_MetallicGlossMap", texture);
                }
            }
            #endif
        }
        
        #if UNITY_EDITOR
        // Save material as asset
        string materialPath = Path.Combine(downloadFolderPath, materialName + "_Material.mat");
        string materialAssetPath = materialPath.Replace(Application.dataPath, "Assets");
        UnityEditor.AssetDatabase.CreateAsset(mat, materialAssetPath);
        UnityEditor.AssetDatabase.SaveAssets();
        #endif
        
        return mat;
    }

    void UpdateStatus(string message, int progress)
    {
        if (statusText != null)
            statusText.text = message;
        
        if (progressSlider != null)
            progressSlider.value = progress;
        
        Debug.Log($"[{progress}%] {message}");
    }

    string SanitizeFileName(string fileName)
    {
        string invalid = new string(Path.GetInvalidFileNameChars());
        foreach (char c in invalid)
        {
            fileName = fileName.Replace(c.ToString(), "_");
        }
        return fileName.Replace(" ", "_");
    }
}