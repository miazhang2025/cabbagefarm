using UnityEngine;
using UnityEditor;
using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine.Networking; 

public class ModelAPI : EditorWindow
{
    // API Configuration
    private string apiKey = ""; 
    private string apiBaseUrl = "https://api.meshy.ai/openapi/v1"; 
    private string imageTo3dUrl = "https://api.meshy.ai/openapi/v1/image-to-3d";
    private string multiImageTo3dUrl = "https://api.meshy.ai/openapi/v1/multi-image-to-3d";
    private string folderPath = "Assets/MeshyModels";
    
    // Generation Settings
    private Texture2D inputImage = null;
    private string inputImagePath = "";
    private string imageFolderPath = "";
    private string artStyle = "realistic";
    private string[] artStyles = { "realistic", "sculpture" };
    private bool shouldRemesh = true;
    private string format = "glb";
    private string[] formats = { "glb", "fbx", "obj", "mtl", "usdz" };
    private bool enablePbr = true;
    private bool useMultiImage = false;
    
    // Status and Task Tracking
    private string statusMessage = "";
    private string currentPreviewTaskId = "";
    private string currentRefineTaskId = "";
    private MeshyResponse currentTaskResponse = null;
    
    // Preview and Confirmation
    private Vector2 taskPreviewScrollPos;
    private Texture2D taskThumbnail = null;
    private bool showDownloadConfirmation = false;
    private string pendingModelUrl = "";
    private string pendingTaskId = "";
    private TextureEntry[] pendingTextureUrls = null;
    
    // Response Classes
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
        public string prompt;
        public string art_style;
        public long started_at;
        public long created_at;
        public long finished_at;
        public int preceding_tasks;
        public TaskError task_error;

    }

    [Serializable]
    public class TaskError
    {
        public string message;
    }

    [Serializable]
    public class TextureEntry
    {
        public string base_color;
        public string metallic;
        public string normal;
        public string roughness;
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
            switch (format.ToLower())
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

    [MenuItem("MeshyAI/3D Model Generator")]
    public static void ShowWindow()
    {
        GetWindow<ModelAPI>("MeshyAI Model Generator");
    }

    private void OnGUI()
    {
        GUILayout.Label("MeshyAI 3D Model Generator", EditorStyles.boldLabel);
        EditorGUILayout.Space(10);

        // API Configuration
        EditorGUILayout.LabelField("API Configuration", EditorStyles.boldLabel);
        apiKey = EditorGUILayout.TextField("API Key", apiKey);
        folderPath = EditorGUILayout.TextField("Download Folder", folderPath);
        
        EditorGUILayout.Space(10);

        // Generation Settings
        EditorGUILayout.LabelField("Generation Settings", EditorStyles.boldLabel);
        
        useMultiImage = EditorGUILayout.Toggle("Use Multi-Image (Folder)", useMultiImage);
        
        if (useMultiImage)
        {
            // Multi-Image Mode: Folder Input
            EditorGUILayout.LabelField("Image Folder", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            imageFolderPath = EditorGUILayout.TextField("Folder Path", imageFolderPath);
            if (GUILayout.Button("Browse", GUILayout.Width(80)))
            {
                string path = EditorUtility.OpenFolderPanel("Select Image Folder", "", "");
                if (!string.IsNullOrEmpty(path))
                {
                    imageFolderPath = path;
                }
            }
            EditorGUILayout.EndHorizontal();
            
            if (!string.IsNullOrEmpty(imageFolderPath))
            {
                string[] imageFiles = GetImageFilesFromFolder(imageFolderPath);
                EditorGUILayout.LabelField($"Found {imageFiles.Length} image(s)", EditorStyles.miniLabel);
                if (imageFiles.Length > 4)
                {
                    EditorGUILayout.HelpBox($"Warning: Multi-image-to-3D supports 1-4 images. Only the first 4 will be used.", MessageType.Warning);
                }
            }
        }
        else
        {
            // Single Image Mode
            EditorGUILayout.LabelField("Input Image", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Select Image", GUILayout.Width(100)))
            {
                string path = EditorUtility.OpenFilePanel("Select Image", "", "png,jpg,jpeg");
                if (!string.IsNullOrEmpty(path))
                {
                    LoadImageFromPath(path);
                }
            }
            if (GUILayout.Button("Clear", GUILayout.Width(60)))
            {
                inputImage = null;
                inputImagePath = "";
            }
            EditorGUILayout.EndHorizontal();
            
            if (!string.IsNullOrEmpty(inputImagePath))
            {
                EditorGUILayout.LabelField("Image Path:", inputImagePath);
            }
            
            // Image Preview
            if (inputImage != null)
            {
                GUILayout.Label(inputImage, GUILayout.Width(200), GUILayout.Height(200));
            }
            else
            {
                EditorGUILayout.HelpBox("No image selected. Please select an image file (PNG, JPG, JPEG).", MessageType.Info);
            }
        }
        
        EditorGUILayout.Space(10);
        
        format = formats[EditorGUILayout.Popup("Format", Array.IndexOf(formats, format), formats)];
        shouldRemesh = EditorGUILayout.Toggle("Should Remesh", shouldRemesh);
        enablePbr = EditorGUILayout.Toggle("Enable PBR", enablePbr);

        EditorGUILayout.Space(10);

        // Generate Button
        bool canGenerate = !string.IsNullOrEmpty(apiKey) && !showDownloadConfirmation;
        if (useMultiImage)
        {
            canGenerate = canGenerate && !string.IsNullOrEmpty(imageFolderPath) && GetImageFilesFromFolder(imageFolderPath).Length > 0;
        }
        else
        {
            canGenerate = canGenerate && inputImage != null;
        }
        
        EditorGUI.BeginDisabledGroup(!canGenerate);
        if (GUILayout.Button(useMultiImage ? "Generate 3D Model from Folder" : "Generate 3D Model from Image", GUILayout.Height(30)))
        {
            if (useMultiImage)
            {
                GenerateMultiImageModel();
            }
            else
            {
                GenerateModel();
            }
        }
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.Space(10);

        // Status Message
        EditorGUILayout.HelpBox(statusMessage, MessageType.Info);

        // Download Confirmation Dialog
        if (showDownloadConfirmation && currentTaskResponse != null)
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Model Ready for Download", EditorStyles.boldLabel);
            EditorGUILayout.Space(5);
            
            // Task Preview
            taskPreviewScrollPos = EditorGUILayout.BeginScrollView(taskPreviewScrollPos, GUILayout.Height(300));
            
            EditorGUILayout.LabelField("Task ID:", currentTaskResponse.id);
            EditorGUILayout.LabelField("Status:", currentTaskResponse.status);
            EditorGUILayout.LabelField("Progress:", $"{currentTaskResponse.progress}%");
            
            if (!string.IsNullOrEmpty(currentTaskResponse.prompt))
            {
                EditorGUILayout.LabelField("Source:", currentTaskResponse.prompt);
            }
            
            if (!string.IsNullOrEmpty(currentTaskResponse.art_style))
            {
                EditorGUILayout.LabelField("Art Style:", currentTaskResponse.art_style);
            }
            
            // Thumbnail
            if (!string.IsNullOrEmpty(currentTaskResponse.thumbnail_url))
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Preview:", EditorStyles.boldLabel);
                
                if (taskThumbnail == null)
                {
                    if (GUILayout.Button("Load Preview Image"))
                    {
                        LoadThumbnail(currentTaskResponse.thumbnail_url);
                    }
                }
                else
                {
                    GUILayout.Label(taskThumbnail, GUILayout.Width(200), GUILayout.Height(200));
                }
            }
            
            // Model URLs
            if (currentTaskResponse.model_urls != null)
            {
                EditorGUILayout.Space(5);
                EditorGUILayout.LabelField("Available Formats:", EditorStyles.boldLabel);
                
                if (!string.IsNullOrEmpty(currentTaskResponse.model_urls.glb))
                    EditorGUILayout.LabelField("✓ GLB", EditorStyles.miniLabel);
                if (!string.IsNullOrEmpty(currentTaskResponse.model_urls.fbx))
                    EditorGUILayout.LabelField("✓ FBX", EditorStyles.miniLabel);
                if (!string.IsNullOrEmpty(currentTaskResponse.model_urls.obj))
                    EditorGUILayout.LabelField("✓ OBJ", EditorStyles.miniLabel);
                if (!string.IsNullOrEmpty(currentTaskResponse.model_urls.usdz))
                    EditorGUILayout.LabelField("✓ USDZ", EditorStyles.miniLabel);
            }
            
            EditorGUILayout.EndScrollView();
            
            EditorGUILayout.Space(10);
            
            // Download Buttons
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Download & Create Material", GUILayout.Height(30)))
            {
                DownloadAndCreateMaterial();
            }
            if (GUILayout.Button("Cancel", GUILayout.Height(30)))
            {
                showDownloadConfirmation = false;
                currentTaskResponse = null;
                taskThumbnail = null;
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.EndVertical();
        }

        // Task Progress (if generating)
        if (!string.IsNullOrEmpty(currentPreviewTaskId) && !showDownloadConfirmation)
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Generation Progress", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Preview Task ID:", currentPreviewTaskId);
            if (!string.IsNullOrEmpty(currentRefineTaskId))
            {
                EditorGUILayout.LabelField("Refine Task ID:", currentRefineTaskId);
            }
            EditorGUILayout.EndVertical();
        }
    }

    /// <summary>
    /// Get all image files from a folder
    /// </summary>
    private string[] GetImageFilesFromFolder(string folderPath)
    {
        if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
        {
            return new string[0];
        }

        List<string> imageFiles = new List<string>();
        string[] extensions = { "*.png", "*.jpg", "*.jpeg" };
        
        foreach (string extension in extensions)
        {
            imageFiles.AddRange(Directory.GetFiles(folderPath, extension, SearchOption.TopDirectoryOnly));
        }

        return imageFiles.ToArray();
    }

    /// <summary>
    /// Load image from file path
    /// </summary>
    private void LoadImageFromPath(string path)
    {
        try
        {
            byte[] imageData = File.ReadAllBytes(path);
            inputImage = new Texture2D(2, 2);
            inputImage.LoadImage(imageData);
            inputImagePath = path;
            statusMessage = $"Image loaded: {Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            statusMessage = $"Failed to load image: {ex.Message}";
            Debug.LogError($"Error loading image: {ex.Message}");
            inputImage = null;
            inputImagePath = "";
        }
    }

    /// <summary>
    /// Main generation workflow: Create Task -> Poll -> Download
    /// Note: Image-to-3D API generates the final model directly (no separate preview/refine steps)
    /// </summary>
    private async void GenerateModel()
    {
        if (string.IsNullOrEmpty(apiKey.Trim()))
        {
            statusMessage = "Please enter an API Key.";
            return;
        }

        if (inputImage == null)
        {
            statusMessage = "Please select an input image.";
            return;
        }

        try
        {
            statusMessage = "Uploading image and creating task...";
            currentPreviewTaskId = "";
            currentRefineTaskId = "";
            showDownloadConfirmation = false;

            // Step 1: Create task with image (generates final model directly)
            string taskId = await CreatePreviewTaskFromImage(inputImage, inputImagePath, artStyle, shouldRemesh);
            
            if (string.IsNullOrEmpty(taskId))
            {
                statusMessage = "Failed to create task.";
                return;
            }

            currentPreviewTaskId = taskId;
            currentRefineTaskId = taskId;
            statusMessage = "Task created. Generating 3D model...";

            // Step 2: Poll for task completion (Image-to-3D generates final model directly)
            await PollForRefinedResult(taskId);
        }
        catch (Exception ex)
        {
            statusMessage = $"Error: {ex.Message}";
            Debug.LogError($"Error generating model: {ex.Message}");
        }
    }

    /// <summary>
    /// Multi-image generation workflow: Load images from folder -> Create Task -> Poll -> Download
    /// </summary>
    private async void GenerateMultiImageModel()
    {
        if (string.IsNullOrEmpty(apiKey.Trim()))
        {
            statusMessage = "Please enter an API Key.";
            return;
        }

        if (string.IsNullOrEmpty(imageFolderPath) || !Directory.Exists(imageFolderPath))
        {
            statusMessage = "Please select a valid image folder.";
            return;
        }

        string[] imageFiles = GetImageFilesFromFolder(imageFolderPath);
        if (imageFiles.Length == 0)
        {
            statusMessage = "No image files found in the selected folder.";
            return;
        }

        // Limit to 4 images as per API documentation
        int imageCount = Mathf.Min(imageFiles.Length, 4);
        statusMessage = $"Found {imageFiles.Length} image(s), using first {imageCount}...";

        try
        {
            statusMessage = "Loading images and creating multi-image task...";
            currentPreviewTaskId = "";
            currentRefineTaskId = "";
            showDownloadConfirmation = false;

            // Step 1: Create task with multiple images
            string taskId = await CreateMultiImageTask(imageFiles, imageCount);
            
            if (string.IsNullOrEmpty(taskId))
            {
                statusMessage = "Failed to create multi-image task.";
                return;
            }

            currentPreviewTaskId = taskId;
            currentRefineTaskId = taskId;
            statusMessage = "Multi-image task created. Generating 3D model...";

            // Step 2: Poll for task completion
            await PollForRefinedResult(taskId);
        }
        catch (Exception ex)
        {
            statusMessage = $"Error: {ex.Message}";
            Debug.LogError($"Error generating multi-image model: {ex.Message}");
        }
    }

    /// <summary>
    /// Create a preview task from image
    /// </summary>
    private async Task<string> CreatePreviewTaskFromImage(Texture2D image, string imagePath, string artStyle, bool shouldRemesh)
    {
        // Get image bytes first
        byte[] imageBytes = null;
        string fileName = "";
        string contentType = "image/png";
        
        if (!string.IsNullOrEmpty(imagePath) && File.Exists(imagePath))
        {
            try
            {
                imageBytes = File.ReadAllBytes(imagePath);
                fileName = Path.GetFileName(imagePath);
                
                // Determine content type from file extension
                string extension = Path.GetExtension(imagePath).ToLower();
                switch (extension)
                {
                    case ".jpg":
                    case ".jpeg":
                        contentType = "image/jpeg";
                        break;
                    case ".png":
                        contentType = "image/png";
                        break;
                    default:
                        contentType = "image/png";
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to read image file: {ex.Message}");
                return null;
            }
        }
        else if (image != null)
        {
            try
            {
                // Convert texture to PNG bytes
                imageBytes = image.EncodeToPNG();
                fileName = "input_image.png";
                contentType = "image/png";
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to encode image: {ex.Message}");
                return null;
            }
        }
        
        if (imageBytes == null || imageBytes.Length == 0)
        {
            Debug.LogError("Failed to get image bytes for upload");
            return null;
        }

        Debug.Log($"Preparing to upload image: {fileName}, Size: {imageBytes.Length} bytes, Type: {contentType}");

        // Convert image to base64 data URI as required by Meshy API
        string base64Image = Convert.ToBase64String(imageBytes);
        string dataUri = $"data:{contentType};base64,{base64Image}";

        // Use UnityWebRequest to send JSON request
        UnityWebRequest request = null;
        try
        {
            // Create JSON payload according to Meshy API documentation
            // Note: art_style is not a parameter for image-to-3d API
            var payload = new
            {
                image_url = dataUri,
                should_remesh = shouldRemesh,
                should_texture = true, // Always generate textures (enable_pbr requires should_texture to be true)
                enable_pbr = enablePbr, // Generate PBR maps if enabled
                ai_model = "latest" // Using latest (Meshy 6 Preview)
            };

            string jsonPayload = JsonConvert.SerializeObject(payload);
            byte[] jsonBytes = Encoding.UTF8.GetBytes(jsonPayload);

            // Create UnityWebRequest with JSON data
            request = new UnityWebRequest(imageTo3dUrl, "POST");
            request.uploadHandler = new UploadHandlerRaw(jsonBytes);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", $"Bearer {apiKey}");
            request.timeout = 300; // 5 minutes

            Debug.Log("Sending JSON request to Meshy API...");

            // Send request
            var operation = request.SendWebRequest();
            
            // Wait for completion
            while (!operation.isDone)
            {
                await Task.Yield();
            }

            Debug.Log($"Response received: {request.responseCode}");

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Preview task creation failed: {request.responseCode} - {request.error} - {request.downloadHandler?.text}");
                return null;
            }

            string responseBody = request.downloadHandler.text;
            var resultData = JsonConvert.DeserializeObject<TaskIDResponse>(responseBody);
            
            Debug.Log($"Preview task created successfully: {resultData?.result}");
            return resultData?.result;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error creating preview task: {ex.Message}\n{ex.StackTrace}");
            return null;
        }
        finally
        {
            request?.Dispose();
        }
    }

    /// <summary>
    /// Create a multi-image-to-3d task from multiple image files
    /// </summary>
    private async Task<string> CreateMultiImageTask(string[] imageFiles, int imageCount)
    {
        // Convert images to base64 data URIs
        List<string> imageUrls = new List<string>();
        
        for (int i = 0; i < imageCount; i++)
        {
            try
            {
                byte[] imageBytes = File.ReadAllBytes(imageFiles[i]);
                string extension = Path.GetExtension(imageFiles[i]).ToLower();
                string contentType = "image/png";
                
                switch (extension)
                {
                    case ".jpg":
                    case ".jpeg":
                        contentType = "image/jpeg";
                        break;
                    case ".png":
                        contentType = "image/png";
                        break;
                }
                
                string base64Image = Convert.ToBase64String(imageBytes);
                string dataUri = $"data:{contentType};base64,{base64Image}";
                imageUrls.Add(dataUri);
                
                Debug.Log($"Loaded image {i + 1}/{imageCount}: {Path.GetFileName(imageFiles[i])}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to load image {imageFiles[i]}: {ex.Message}");
                return null;
            }
        }

        Debug.Log($"Preparing to upload {imageUrls.Count} images for multi-image-to-3d...");

        // Use UnityWebRequest to send JSON request
        UnityWebRequest request = null;
        try
        {
            // Create JSON payload according to Meshy API documentation
            var payload = new
            {
                image_urls = imageUrls.ToArray(),
                should_remesh = shouldRemesh,
                should_texture = true, // Always generate textures
                enable_pbr = enablePbr, // Generate PBR maps if enabled
                ai_model = "latest" // Using latest (Meshy 6 Preview)
            };

            string jsonPayload = JsonConvert.SerializeObject(payload);
            byte[] jsonBytes = Encoding.UTF8.GetBytes(jsonPayload);

            // Create UnityWebRequest with JSON data
            request = new UnityWebRequest(multiImageTo3dUrl, "POST");
            request.uploadHandler = new UploadHandlerRaw(jsonBytes);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", $"Bearer {apiKey}");
            request.timeout = 300; // 5 minutes

            Debug.Log("Sending JSON request to Meshy Multi-Image-to-3D API...");

            // Send request
            var operation = request.SendWebRequest();
            
            // Wait for completion
            while (!operation.isDone)
            {
                await Task.Yield();
            }

            Debug.Log($"Response received: {request.responseCode}");

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Multi-image task creation failed: {request.responseCode} - {request.error} - {request.downloadHandler?.text}");
                return null;
            }

            string responseBody = request.downloadHandler.text;
            var resultData = JsonConvert.DeserializeObject<TaskIDResponse>(responseBody);
            
            Debug.Log($"Multi-image task created successfully: {resultData?.result}");
            return resultData?.result;
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error creating multi-image task: {ex.Message}\n{ex.StackTrace}");
            return null;
        }
        finally
        {
            request?.Dispose();
        }
    }

    /// <summary>
    /// Poll for preview task completion
    /// </summary>
    private async Task<bool> PollForPreview(string previewTaskId)
    {
        using (HttpClient client = new HttpClient())
        {
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
            string getUrl = $"{imageTo3dUrl}/{previewTaskId}";

            while (true)
            {
                HttpResponseMessage response = await client.GetAsync(getUrl);
                
                if (!response.IsSuccessStatusCode)
                {
                    Debug.LogError($"Preview polling failed: {response.ReasonPhrase}");
                    return false;
                }

                string content = await response.Content.ReadAsStringAsync();
                var meshyResponse = JsonConvert.DeserializeObject<MeshyResponse>(content);

                statusMessage = $"Generating Preview... ({meshyResponse.progress}%)";

                if (meshyResponse.status == "SUCCEEDED")
                {
                    statusMessage = "Preview generation succeeded!";
                    return true;
                }
                else if (meshyResponse.status == "FAILED")
                {
                    statusMessage = "Preview generation failed!";
                    if (meshyResponse.task_error != null && !string.IsNullOrEmpty(meshyResponse.task_error.message))
                    {
                        statusMessage += $" Error: {meshyResponse.task_error.message}";
                    }
                    return false;
                }

                await Task.Delay(5000); // Wait 5 seconds before next poll
            }
        }
    }

    /// <summary>
    /// Create a refine task
    /// Note: Image-to-3D API doesn't have a separate refine step - it generates the final model directly
    /// This method is kept for compatibility but may not be needed for image-to-3d workflow
    /// </summary>
    private async Task<string> CreateRefineTask(string previewTaskId, bool enablePbr)
    {
        // Image-to-3D API generates the final model directly, no refine step needed
        // Return the same task ID to continue polling
        Debug.Log("Image-to-3D API generates final model directly, skipping refine step.");
        await Task.CompletedTask; // Make it async
        return previewTaskId;
    }

    /// <summary>
    /// Poll for refined result and show confirmation
    /// </summary>
    private async Task PollForRefinedResult(string refineTaskId)
    {
        using (HttpClient client = new HttpClient())
        {
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
            
            // Determine which endpoint to use based on whether we used multi-image
            string baseUrl = useMultiImage ? multiImageTo3dUrl : imageTo3dUrl;
            string getUrl = $"{baseUrl}/{refineTaskId}";

            while (true)
            {
                HttpResponseMessage response = await client.GetAsync(getUrl);
                
                // If 404, try the other endpoint (tasks might be accessible from both)
                if (!response.IsSuccessStatusCode && response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    string altUrl = useMultiImage ? $"{imageTo3dUrl}/{refineTaskId}" : $"{multiImageTo3dUrl}/{refineTaskId}";
                    response = await client.GetAsync(altUrl);
                    if (response.IsSuccessStatusCode)
                    {
                        getUrl = altUrl; // Use the working endpoint for future polls
                    }
                }
                
                if (!response.IsSuccessStatusCode)
                {
                    Debug.LogError($"Polling failed: {response.ReasonPhrase}");
                    statusMessage = "Failed to check task status.";
                    return;
                }

                string content = await response.Content.ReadAsStringAsync();
                var meshyResponse = JsonConvert.DeserializeObject<MeshyResponse>(content);

                statusMessage = $"Generating 3D Model... ({meshyResponse.progress}%)";

                if (meshyResponse.status == "SUCCEEDED")
                {
                    // Store task response for confirmation
                    currentTaskResponse = meshyResponse;
                    
                    // Get model URL for selected format
                    string modelUrl = meshyResponse.model_urls?.GetUrlByFormat(format);
                    
                    if (!string.IsNullOrEmpty(modelUrl))
                    {
                        pendingModelUrl = modelUrl;
                        pendingTaskId = meshyResponse.id;
                        pendingTextureUrls = meshyResponse.texture_urls;
                        
                        statusMessage = "Model generation completed! Review and confirm download.";
                        showDownloadConfirmation = true;
                    }
                    else
                    {
                        statusMessage = $"Model generation completed, but no URL found for format: {format}";
                    }
                    return;
                }
                else if (meshyResponse.status == "FAILED")
                {
                    statusMessage = "Refine generation failed!";
                    if (meshyResponse.task_error != null && !string.IsNullOrEmpty(meshyResponse.task_error.message))
                    {
                        statusMessage += $" Error: {meshyResponse.task_error.message}";
                    }
                    return;
                }

                await Task.Delay(5000); // Wait 5 seconds before next poll
            }
        }
    }

    /// <summary>
    /// Download model and textures, then create material
    /// </summary>
    private async void DownloadAndCreateMaterial()
    {
        if (string.IsNullOrEmpty(pendingModelUrl))
        {
            statusMessage = "No model URL available.";
            return;
        }

        statusMessage = "Downloading model and textures...";
        showDownloadConfirmation = false;

        try
        {
            // Ensure folder exists
            string fullFolderPath = Path.Combine(Application.dataPath, folderPath.Replace("Assets/", ""));
            Directory.CreateDirectory(fullFolderPath);

            // Download model
            string modelFileName = $"meshy_{pendingTaskId}.{format}";
            string modelPath = Path.Combine(fullFolderPath, modelFileName);

            using (HttpClient client = new HttpClient())
            {
                byte[] modelData = await client.GetByteArrayAsync(pendingModelUrl);
                File.WriteAllBytes(modelPath, modelData);
                Debug.Log($"Model downloaded to: {modelPath}");
            }

            // Download all textures including PBR maps
            Dictionary<string, string> texturePaths = new Dictionary<string, string>();
            if (pendingTextureUrls != null && pendingTextureUrls.Length > 0)
            {
                for (int i = 0; i < pendingTextureUrls.Length; i++)
                {
                    var textureEntry = pendingTextureUrls[i];
                    
                    // Download base color
                    if (!string.IsNullOrEmpty(textureEntry.base_color))
                    {
                        string textureFileName = $"meshy_{pendingTaskId}_texture_{i}_basecolor.png";
                        string texturePath = Path.Combine(fullFolderPath, textureFileName);

                        using (HttpClient client = new HttpClient())
                        {
                            byte[] textureData = await client.GetByteArrayAsync(textureEntry.base_color);
                            File.WriteAllBytes(texturePath, textureData);
                            texturePaths["baseColor"] = texturePath;
                            Debug.Log($"Base color texture downloaded to: {texturePath}");
                        }
                    }
                    
                    // Download PBR maps if available
                    if (enablePbr)
                    {
                        if (!string.IsNullOrEmpty(textureEntry.metallic))
                        {
                            string metallicFileName = $"meshy_{pendingTaskId}_texture_{i}_metallic.png";
                            string metallicPath = Path.Combine(fullFolderPath, metallicFileName);

                            using (HttpClient client = new HttpClient())
                            {
                                byte[] textureData = await client.GetByteArrayAsync(textureEntry.metallic);
                                File.WriteAllBytes(metallicPath, textureData);
                                texturePaths["metallic"] = metallicPath;
                                Debug.Log($"Metallic texture downloaded to: {metallicPath}");
                            }
                        }
                        
                        if (!string.IsNullOrEmpty(textureEntry.normal))
                        {
                            string normalFileName = $"meshy_{pendingTaskId}_texture_{i}_normal.png";
                            string normalPath = Path.Combine(fullFolderPath, normalFileName);

                            using (HttpClient client = new HttpClient())
                            {
                                byte[] textureData = await client.GetByteArrayAsync(textureEntry.normal);
                                File.WriteAllBytes(normalPath, textureData);
                                texturePaths["normal"] = normalPath;
                                Debug.Log($"Normal texture downloaded to: {normalPath}");
                            }
                        }
                        
                        if (!string.IsNullOrEmpty(textureEntry.roughness))
                        {
                            string roughnessFileName = $"meshy_{pendingTaskId}_texture_{i}_roughness.png";
                            string roughnessPath = Path.Combine(fullFolderPath, roughnessFileName);

                            using (HttpClient client = new HttpClient())
                            {
                                byte[] textureData = await client.GetByteArrayAsync(textureEntry.roughness);
                                File.WriteAllBytes(roughnessPath, textureData);
                                texturePaths["roughness"] = roughnessPath;
                                Debug.Log($"Roughness texture downloaded to: {roughnessPath}");
                            }
                        }
                    }
                    
                    break; // Use first texture set
                }
            }

            // Refresh asset database
            AssetDatabase.Refresh();
            
            // Wait a moment for Unity to import assets
            await Task.Delay(1000);

            // Create material with textures
            string modelAssetPath = Path.Combine("Assets", folderPath.Replace("Assets/", ""), modelFileName).Replace("\\", "/");
            
            if (texturePaths.Count > 0)
            {
                Dictionary<string, string> textureAssetPaths = new Dictionary<string, string>();
                foreach (var kvp in texturePaths)
                {
                    string textureAssetPath = Path.Combine("Assets", folderPath.Replace("Assets/", ""), Path.GetFileName(kvp.Value)).Replace("\\", "/");
                    textureAssetPaths[kvp.Key] = textureAssetPath;
                }
                
                CreateURPMaterialWithTextures(textureAssetPaths, modelAssetPath, pendingTaskId);
            }
            statusMessage = $"Model and material created successfully!\nModel: {modelAssetPath}";
            
            // Reset pending data
            pendingModelUrl = "";
            pendingTaskId = "";
            pendingTextureUrls = null;
            currentTaskResponse = null;
            taskThumbnail = null;
        }
        catch (Exception ex)
        {
            statusMessage = $"Download failed: {ex.Message}";
            Debug.LogError($"Error downloading model: {ex.Message}");
        }
    }

    /// <summary>
    /// Create a URP Lit material with all PBR textures and apply it to the model
    /// </summary>
    private void CreateURPMaterialWithTextures(Dictionary<string, string> textureAssetPaths, string modelPath, string taskId)
    {
        // Try to find URP Lit shader
        Shader urpLitShader = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLitShader == null)
        {
            // Fallback to Standard if URP Lit is not available
            urpLitShader = Shader.Find("Standard");
            Debug.LogWarning("URP Lit shader not found. Using Standard shader instead. Make sure URP is installed.");
        }

        // Create material
        Material material = new Material(urpLitShader);
        material.name = $"Meshy_{taskId}_Material";

        // Load and assign base color texture
        if (textureAssetPaths.ContainsKey("baseColor"))
        {
            Texture2D baseColor = AssetDatabase.LoadAssetAtPath<Texture2D>(textureAssetPaths["baseColor"]);
            if (baseColor != null)
            {
                if (urpLitShader.name.Contains("Universal"))
                {
                    material.SetTexture("_BaseMap", baseColor);
                    material.SetColor("_BaseColor", Color.white);
                }
                else
                {
                    material.mainTexture = baseColor;
                }
            }
        }

        // Load and assign PBR textures if available
        if (enablePbr)
        {
            // Metallic
            if (textureAssetPaths.ContainsKey("metallic"))
            {
                Texture2D metallic = AssetDatabase.LoadAssetAtPath<Texture2D>(textureAssetPaths["metallic"]);
                if (metallic != null)
                {
                    if (urpLitShader.name.Contains("Universal"))
                    {
                        material.SetTexture("_MetallicGlossMap", metallic);
                        material.EnableKeyword("_METALLICSPECGLOSSMAP");
                        material.SetFloat("_Metallic", 1.0f); // Enable metallic workflow
                    }
                    else
                    {
                        material.SetTexture("_MetallicGlossMap", metallic);
                        material.SetFloat("_Metallic", 1.0f);
                    }
                }
            }

            // Normal map
            if (textureAssetPaths.ContainsKey("normal"))
            {
                Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(textureAssetPaths["normal"]);
                if (normal != null)
                {
                    if (urpLitShader.name.Contains("Universal"))
                    {
                        material.SetTexture("_BumpMap", normal);
                        material.EnableKeyword("_NORMALMAP");
                        material.SetFloat("_BumpScale", 1.0f);
                    }
                    else
                    {
                        material.SetTexture("_BumpMap", normal);
                        material.SetFloat("_BumpScale", 1.0f);
                    }
                }
            }

            // Roughness/Smoothness
            // URP uses smoothness (inverse of roughness) stored in alpha channel of MetallicGlossMap
            // If we have a separate roughness map, we could combine it, but for now set smoothness
            if (textureAssetPaths.ContainsKey("roughness"))
            {
                Texture2D roughness = AssetDatabase.LoadAssetAtPath<Texture2D>(textureAssetPaths["roughness"]);
                if (roughness != null)
                {
                    if (urpLitShader.name.Contains("Universal"))
                    {
                        // URP smoothness is typically in the alpha of metallic map
                        // For now, set a default smoothness value
                        // In a full implementation, you'd need to combine roughness into metallic alpha
                        material.SetFloat("_Smoothness", 0.5f);
                    }
                    else
                    {
                        // Standard shader uses roughness in metallic map alpha
                        material.SetFloat("_Glossiness", 0.5f);
                    }
                }
            }
            else
            {
                // Set default smoothness if no roughness map
                if (urpLitShader.name.Contains("Universal"))
                {
                    material.SetFloat("_Smoothness", 0.5f);
                }
            }
        }

        // Save material
        string matPath = Path.Combine(Path.GetDirectoryName(modelPath), material.name + ".mat").Replace("\\", "/");
        AssetDatabase.CreateAsset(material, matPath);
        AssetDatabase.SaveAssets();

        // Load model and apply material
        GameObject modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        if (modelPrefab != null)
        {
            // Apply material to all renderers in the model
            Renderer[] renderers = modelPrefab.GetComponentsInChildren<Renderer>();
            foreach (Renderer renderer in renderers)
            {
                Material[] materials = new Material[renderer.sharedMaterials.Length];
                for (int i = 0; i < materials.Length; i++)
                {
                    materials[i] = material;
                }
                renderer.sharedMaterials = materials;
            }

            // Mark model as dirty and save
            EditorUtility.SetDirty(modelPrefab);
            AssetDatabase.SaveAssets();
            Debug.Log($"URP Lit material with PBR textures applied to model: {modelPath}");
        }
        else
        {
            Debug.LogWarning($"Model not found at: {modelPath}. Material created at: {matPath}");
        }
    }

    /// <summary>
    /// Load thumbnail image from URL
    /// </summary>
    private async void LoadThumbnail(string thumbnailUrl)
    {
        using (HttpClient client = new HttpClient())
        {
            try
            {
                byte[] imageData = await client.GetByteArrayAsync(thumbnailUrl);
                taskThumbnail = new Texture2D(2, 2);
                taskThumbnail.LoadImage(imageData);
                statusMessage = "Preview image loaded.";
            }
            catch (Exception ex)
            {
                statusMessage = $"Failed to load preview: {ex.Message}";
                Debug.LogError($"Error loading thumbnail: {ex.Message}");
            }
        }
    }
}

