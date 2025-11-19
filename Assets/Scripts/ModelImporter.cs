using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using TMPro;
using UnityEngine.UI;

public class ModelImporter : MonoBehaviour
{
    public TextMeshProUGUI statusText;
    public string folderPath = "Assets/MeshyModels/Speech";
    public Vector3 positionRange = new Vector3(5, 0, 5);
    public string promptText = "";
    private string format = "fbx";
    private string[] formats = { "glb", "fbx", "obj", "mtl", "usdz"};
    private string artstyle = "realistic";
    private string[] artstyles = {"realistic","sculpture"};
    private string statusMessage = "";
    //private bool SpeechRecStatus = false;

    private string apiKey = "msy_XEaoe9uHKNotQkWmjfifk7C3tUBf1tyRczsQ"; // Replace with your actual key
    public string downloadFolder = "Assets/MeshyModels/Speech";
    private const string API_URL = "https://api.meshy.ai/openapi/v2/text-to-3d";

    private System.Collections.Generic.List<GameObject> importedModels = new System.Collections.Generic.List<GameObject>();


    public void OnGenerate(){
        if (string.IsNullOrEmpty(promptText))
            {
                statusMessage = "Please enter a prompt.";
                Debug.Log(statusMessage);

            }
            else
            {
                statusMessage = "Sending prompt to Meshy...";
                statusText.text = statusMessage;
                Debug.Log(statusMessage);
                GenerateModelFromPrompt(promptText);
            }
    }


    public void OnImport(){
            importedModels.Clear();
            ImportModelsFromFolder(folderPath);
    }

    public void OnRandom(){
        RandomizePositions();
    }

    public void OnDelete(){
        DeleteImportedModels();
    }


    // private void OnGUI()
    // {
    //     GUILayout.Label("Meshy Settings", EditorStyles.boldLabel);

    //     folderPath = EditorGUILayout.TextField("Folder Path", folderPath);
    //     apiKey = EditorGUILayout.TextField("Meshy API Key", apiKey);
        

    //     GUILayout.Space(20);
    //     GUILayout.Label("Model Generation", EditorStyles.boldLabel);

    //     promptText = EditorGUILayout.TextField("Prompt", promptText);
    //     format = formats[EditorGUILayout.Popup("Format", Array.IndexOf(formats, format), formats)];
    //     artstyle = artstyles[EditorGUILayout.Popup("Art Style", Array.IndexOf(artstyles,artstyle),artstyles)];

    //     if (GUILayout.Button("Generate Model"))
    //     {
    //         if (string.IsNullOrEmpty(promptText.Trim()))
    //         {
    //             statusMessage = "Please enter a prompt.";
    //         }
    //         else
    //         {
    //             statusMessage = "Sending prompt to Meshy...";
    //             GenerateModelFromPrompt(promptText);
    //         }
    //     }

    //     GUILayout.Space(10);
    //     EditorGUILayout.HelpBox("Status: " + statusMessage, MessageType.Info);

    //     GUILayout.Space(20);
    //     GUILayout.Label("Model Settings", EditorStyles.boldLabel);

    //     positionRange = EditorGUILayout.Vector3Field("Position Range", positionRange);

    //     if (GUILayout.Button("Import"))
    //     {
    //         importedModels.Clear();
    //         ImportModelsFromFolder(folderPath);
    //     }

    //     if (GUILayout.Button("Randomize Positions"))
    //     {
    //         RandomizePositions();
    //     }

    //     if (GUILayout.Button("Delete Imported"))
    //     {
    //         DeleteImportedModels();
    //     }

        
    // }

    private void ImportModelsFromFolder(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
        {
            Debug.LogError("Invalid folder path: " + path);
            return;
        }

        string[] guids = AssetDatabase.FindAssets("t:Model", new[] { path });

        foreach (string guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            GameObject modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (modelPrefab != null)
            {
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(modelPrefab);
                Undo.RegisterCreatedObjectUndo(instance, "Import Model");
                importedModels.Add(instance);
                Debug.Log("Imported: " + modelPrefab.name);
            }
        }
    }

    private void RandomizePositions()
    {
        foreach (GameObject obj in importedModels)
        {
            if (obj != null)
            {
                Vector3 randomPosition = new Vector3(
                    UnityEngine.Random.Range(-positionRange.x, positionRange.x),
                    UnityEngine.Random.Range(-positionRange.y, positionRange.y),
                    UnityEngine.Random.Range(-positionRange.z, positionRange.z)
                );
                obj.transform.position = randomPosition;
                Undo.RecordObject(obj.transform, "Randomize Position");
            }
        }

        Debug.Log("Randomized positions of imported models.");
    }

    private void DeleteImportedModels()
    {
        foreach (GameObject obj in importedModels)
        {
            if (obj != null)
            {
                Undo.DestroyObjectImmediate(obj);
            }
        }

        importedModels.Clear();
        Debug.Log("Deleted all imported models.");
    }

    private async void GenerateModelFromPrompt(string prompt)
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

        var payload = new
        {
            mode = "preview",
            prompt = prompt,
            art_style = artstyle,
            should_remesh = true
        };

        string json = JsonConvert.SerializeObject(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            HttpResponseMessage response = await client.PostAsync(API_URL, content);
            if (!response.IsSuccessStatusCode)
            {
                statusMessage = "POST Error: " + response.ReasonPhrase;
                Debug.Log(statusMessage);
                return;
            }

            string responseBody = await response.Content.ReadAsStringAsync();
            var resultData = JsonConvert.DeserializeObject<TaskIDResponse>(responseBody);

            statusMessage = "Model is being generated...";
            Debug.Log(statusMessage);

            await PollForResult(resultData.result, format);
        }
        catch (HttpRequestException ex)
        {
            statusMessage = "Request failed: " + ex.Message;
            Debug.Log(statusMessage);
        }
    }

    // private async Task TextureGeneration(string taskId)
    // {
    //     var client = new HttpClient();
    //     client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

    //      var payload = new
    //     {
    //         mode = "refine",
    //         preview_task_id: taskId,
    //         enable_pbr: true,
    //     };

    //     string json = JsonConvert.SerializeObject(payload);
    //     var content = new StringContent(json, Encoding.UTF8, "application/json");

    //     try
    //     {
    //         HttpResponseMessage response = await client.PostAsync(API_URL, content);
    //         if (!response.IsSuccessStatusCode)
    //         {
    //             statusMessage = "POST Error: " + response.ReasonPhrase;
    //             return;
    //         }

    //         string responseBody = await response.Content.ReadAsStringAsync();
    //         var resultData = JsonConvert.DeserializeObject<TaskIDResponse>(responseBody);

    //         statusMessage = "Model is being generated...";
    //         await PollForResult(resultData.result, format);
    //     }
    //     catch (HttpRequestException ex)
    //     {
    //         statusMessage = "Request failed: " + ex.Message;
    //     }

    // }

    private async Task PollForResult(string previewTaskId, string format)
{
    var client = new HttpClient();
    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

    string getUrl = $"{API_URL}/{previewTaskId}";

    // Poll preview task
    while (true)
    {
        try
        {
            HttpResponseMessage response = await client.GetAsync(getUrl);
            if (!response.IsSuccessStatusCode)
            {
                statusMessage = "GET Error (Preview): " + response.ReasonPhrase;
                Debug.Log(statusMessage);
                return;
            }

            string content = await response.Content.ReadAsStringAsync();
            var meshyResponse = JsonConvert.DeserializeObject<MeshyResponse>(content);

            if (meshyResponse.status == "SUCCEEDED")
            {
                statusMessage = "Preview succeeded. Requesting refinement...";
                Debug.Log(statusMessage);
                string refineTaskId = await StartRefineTask(previewTaskId);
                if (!string.IsNullOrEmpty(refineTaskId))
                {
                    await PollForRefinedResult(refineTaskId, format);
                }
                else
                {
                    statusMessage = "Failed to start refine task.";
                    Debug.Log(statusMessage);
                }

                return;
            }
            else
            {
                statusMessage = $"Generating Preview... ({meshyResponse.progress}%)";
                Debug.Log(statusMessage);
                statusText.text = statusMessage;
                await Task.Delay(5000);
            }
        }
        catch (HttpRequestException ex)
        {
            statusMessage = "Polling preview failed: " + ex.Message;
            statusText.text = statusMessage;
            Debug.Log(statusMessage);
            return;
        }
    }
}

private async Task<string> StartRefineTask(string previewTaskId)
{
    var client = new HttpClient();
    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

    var payload = new
    {
        mode = "refine",
        preview_task_id = previewTaskId,
        enable_pbr = true
    };

    string json = JsonConvert.SerializeObject(payload);
    var content = new StringContent(json, Encoding.UTF8, "application/json");

    try
    {
        HttpResponseMessage response = await client.PostAsync(API_URL, content);
        if (!response.IsSuccessStatusCode)
        {
            statusMessage = "POST Error (Refine): " + response.ReasonPhrase;
            Debug.Log(statusMessage);
            return null;
        }

        string responseBody = await response.Content.ReadAsStringAsync();
        var taskResponse = JsonConvert.DeserializeObject<TaskIDResponse>(responseBody);
        return taskResponse.result;
    }
    catch (HttpRequestException ex)
    {
        statusMessage = "Refine request failed: " + ex.Message;
        Debug.Log(statusMessage);
        return null;
    }
}

private async Task PollForRefinedResult(string refineTaskId, string format)
{
    var client = new HttpClient();
    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

    string getUrl = $"{API_URL}/{refineTaskId}";

    while (true)
    {
        try
        {
            HttpResponseMessage response = await client.GetAsync(getUrl);
            if (!response.IsSuccessStatusCode)
            {
                statusMessage = "GET Error (Refine): " + response.ReasonPhrase;
                Debug.Log(statusMessage);
                return;
            }

            string content = await response.Content.ReadAsStringAsync();
            var meshyResponse = JsonConvert.DeserializeObject<MeshyResponse>(content);

            if (meshyResponse.status == "SUCCEEDED")
            {
                string refinedUrl = meshyResponse.model_urls.GetUrlByFormat(format.ToLower());
                string modelFileName = $"refined_{meshyResponse.id}.{format}";
                string modelPath = Path.Combine(downloadFolder, modelFileName);

                Directory.CreateDirectory(downloadFolder);
                await DownloadFile(refinedUrl, modelPath);

                // Download associated textures
                string texturePath = null;

if (meshyResponse.texture_urls != null)
{
    for (int i = 0; i < meshyResponse.texture_urls.Length; i++)
    {
        var textureUrl = meshyResponse.texture_urls[i].base_color;
        if (!string.IsNullOrEmpty(textureUrl))
        {
            string textureFileName = $"refined_{meshyResponse.id}_texture_{i}.png";
            texturePath = Path.Combine(downloadFolder, textureFileName);
            await DownloadFile(textureUrl, texturePath);
            break; // only use the first one
        }
    }
}


             AssetDatabase.Refresh();

if (File.Exists(modelPath) && File.Exists(texturePath))
{
    ApplyTextureToModel(
        modelPath.Replace(Application.dataPath, "Assets"),
        texturePath.Replace(Application.dataPath, "Assets")
    );
}

statusMessage = "Refined model and texture imported and applied.";
Debug.Log(statusMessage);
return;

            }
            else
            {
                statusMessage = $"Refining... ({meshyResponse.progress}%)";
                statusText.text = statusMessage;
                Debug.Log(statusMessage);
                await Task.Delay(5000);
            }
        }
        catch (HttpRequestException ex)
        {
            statusMessage = "Polling refine failed: " + ex.Message;
            statusText.text = statusMessage;
            Debug.Log(statusMessage);
            return;
        }
    }
}

    private async Task DownloadFile(string url, string path)
    {
        using (HttpClient client = new HttpClient())
        {
            try
            {
                byte[] data = await client.GetByteArrayAsync(url);
                File.WriteAllBytes(path, data);
            }
            catch (HttpRequestException ex)
            {
                statusMessage = "Download failed: " + ex.Message;
                statusText.text = statusMessage;
                Debug.Log(statusMessage);
            }
        }
    }

private void ApplyTextureToModel(string modelPath, string texturePath)
{
    // Load model
    AssetDatabase.Refresh();
    GameObject modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
    if (modelPrefab == null)
    {
        Debug.LogError("Model not found at path: " + modelPath);
        return;
    }

    // Instantiate into scene
    GameObject modelInstance = (GameObject)PrefabUtility.InstantiatePrefab(modelPrefab);
    Undo.RegisterCreatedObjectUndo(modelInstance, "Instantiate Model");

    // Load texture
    Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
    if (texture == null)
    {
        Debug.LogWarning("Texture not found: " + texturePath);
        return;
    }

    // Create material
    Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
    mat.mainTexture = texture;

    // Save material (optional)
    string matPath = Path.Combine(Path.GetDirectoryName(modelPath), Path.GetFileNameWithoutExtension(modelPath) + "_Material.mat");
    matPath = matPath.Replace("\\", "/");
    AssetDatabase.CreateAsset(mat, matPath);

    // Assign to renderer(s)
    var renderers = modelInstance.GetComponentsInChildren<Renderer>();
    foreach (var r in renderers)
    {
        r.sharedMaterial = mat;
    }

    Debug.Log("Assigned texture to model.");
    statusText.text = "Say somthing";
}

    [Serializable]
    public class TaskIDResponse
    {
        public string result;
    }

[Serializable]
public class MeshyResponse
{
    public string id;
    public ModelURLs model_urls;
    public string status;
    public int progress;
    public string thumbnail_url;
    public TextureEntry[] texture_urls;
}

[Serializable]
public class TextureEntry
{
    public string base_color;
}
    [Serializable]
    public class ModelURLs
    {
        public string glb;
        public string fbx;
        public string obj;
        public string mtl;
        public string usdz;

        public string GetUrlByFormat(string format)
        {
            switch (format)
            {
                case "fbx": return fbx;
                case "glb": return glb;
                case "obj": return obj;
                case "mtl": return mtl;
                case "usdz": return usdz;
                default: return glb;
            }
        }
    }
}