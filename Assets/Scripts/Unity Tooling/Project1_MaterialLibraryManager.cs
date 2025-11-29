using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.IO;

/// <summary>
/// Material Library Manager - Foundation Project 1
/// A complete material management system with search, filtering, thumbnails, and batch operations
/// Usage: Window > Material Library Manager
/// </summary>
public class MaterialLibraryManager : EditorWindow
{
    #region Data Classes
    
    [System.Serializable]
    public class MaterialEntry
    {
        public Material material;
        public string path;
        public string shaderName;
        public Texture2D thumbnail;
        public bool isFavorite;
        public List<string> tags = new List<string>();
        
        public MaterialEntry(Material mat, string assetPath)
        {
            material = mat;
            path = assetPath;
            shaderName = mat.shader != null ? mat.shader.name : "None";
        }
    }
    
    [System.Serializable]
    public class LibraryData
    {
        public List<MaterialEntry> materials = new List<MaterialEntry>();
        public List<string> allTags = new List<string>();
    }
    
    #endregion
    
    #region Fields
    
    private LibraryData libraryData = new LibraryData();
    private Vector2 scrollPosition;
    private string searchQuery = "";
    private string selectedShaderFilter = "All";
    private List<string> availableShaders = new List<string>();
    private bool showFavoritesOnly = false;
    private bool showThumbnails = true;
    private float thumbnailSize = 64f;
    
    // UI State
    private MaterialEntry selectedMaterial;
    private bool needsRefresh = false;
    private GUIStyle thumbnailStyle;
    private GUIStyle selectedStyle;
    
    // Batch Operations
    private Material batchReplacementMaterial;
    private string batchPropertyName = "";
    private float batchFloatValue = 0f;
    private Color batchColorValue = Color.white;
    
    #endregion
    
    #region Window Setup
    
    [MenuItem("Window/Material Library Manager")]
    public static void ShowWindow()
    {
        MaterialLibraryManager window = GetWindow<MaterialLibraryManager>("Material Library");
        window.minSize = new Vector2(400, 300);
        window.Show();
    }
    
    private void OnEnable()
    {
        RefreshLibrary();
        LoadPreferences();
    }
    
    private void OnDisable()
    {
        SavePreferences();
    }
    
    #endregion
    
    #region Main GUI
    
    private void OnGUI()
    {
        InitializeStyles();
        
        DrawToolbar();
        DrawFilterBar();
        
        EditorGUILayout.BeginHorizontal();
        {
            DrawMaterialList();
            DrawInspectorPanel();
        }
        EditorGUILayout.EndHorizontal();
        
        DrawBatchOperationsPanel();
        
        if (needsRefresh)
        {
            RefreshLibrary();
            needsRefresh = false;
        }
    }
    
    private void InitializeStyles()
    {
        if (thumbnailStyle == null)
        {
            thumbnailStyle = new GUIStyle(GUI.skin.box);
            thumbnailStyle.padding = new RectOffset(2, 2, 2, 2);
        }
        
        if (selectedStyle == null)
        {
            selectedStyle = new GUIStyle(GUI.skin.box);
            selectedStyle.normal.background = MakeTex(2, 2, new Color(0.3f, 0.5f, 0.8f, 0.5f));
        }
    }
    
    #endregion
    
    #region Toolbar
    
    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        {
            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                RefreshLibrary();
            }
            
            if (GUILayout.Button("Generate Thumbnails", EditorStyles.toolbarButton, GUILayout.Width(140)))
            {
                GenerateAllThumbnails();
            }
            
            GUILayout.FlexibleSpace();
            
            showThumbnails = GUILayout.Toggle(showThumbnails, "Thumbnails", EditorStyles.toolbarButton, GUILayout.Width(80));
            
            if (GUILayout.Button("Settings", EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                ShowSettings();
            }
        }
        EditorGUILayout.EndHorizontal();
    }
    
    #endregion
    
    #region Filter Bar
    
    private void DrawFilterBar()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        {
            // Search
            EditorGUILayout.BeginHorizontal();
            {
                GUILayout.Label("Search:", GUILayout.Width(60));
                string newSearch = EditorGUILayout.TextField(searchQuery);
                if (newSearch != searchQuery)
                {
                    searchQuery = newSearch;
                }
                
                if (GUILayout.Button("Clear", GUILayout.Width(50)))
                {
                    searchQuery = "";
                }
            }
            EditorGUILayout.EndHorizontal();
            
            // Filters
            EditorGUILayout.BeginHorizontal();
            {
                GUILayout.Label("Shader:", GUILayout.Width(60));
                
                List<string> shaderOptions = new List<string> { "All" };
                shaderOptions.AddRange(availableShaders);
                
                int currentIndex = shaderOptions.IndexOf(selectedShaderFilter);
                if (currentIndex == -1) currentIndex = 0;
                
                int newIndex = EditorGUILayout.Popup(currentIndex, shaderOptions.ToArray());
                selectedShaderFilter = shaderOptions[newIndex];
                
                GUILayout.Space(20);
                showFavoritesOnly = GUILayout.Toggle(showFavoritesOnly, "★ Favorites Only", GUILayout.Width(120));
            }
            EditorGUILayout.EndHorizontal();
            
            // Stats
            var filtered = GetFilteredMaterials();
            EditorGUILayout.LabelField($"Showing {filtered.Count} of {libraryData.materials.Count} materials", EditorStyles.miniLabel);
        }
        EditorGUILayout.EndVertical();
    }
    
    #endregion
    
    #region Material List
    
    private void DrawMaterialList()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(position.width * 0.6f));
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            {
                var filteredMaterials = GetFilteredMaterials();
                
                foreach (var entry in filteredMaterials)
                {
                    DrawMaterialEntry(entry);
                }
            }
            EditorGUILayout.EndScrollView();
        }
        EditorGUILayout.EndVertical();
    }
    
    private void DrawMaterialEntry(MaterialEntry entry)
    {
        bool isSelected = selectedMaterial == entry;
        GUIStyle style = isSelected ? selectedStyle : EditorStyles.helpBox;
        
        EditorGUILayout.BeginHorizontal(style);
        {
            // Thumbnail
            if (showThumbnails && entry.thumbnail != null)
            {
                if (GUILayout.Button(entry.thumbnail, thumbnailStyle, 
                    GUILayout.Width(thumbnailSize), GUILayout.Height(thumbnailSize)))
                {
                    selectedMaterial = entry;
                    Selection.activeObject = entry.material;
                }
            }
            
            // Info
            EditorGUILayout.BeginVertical();
            {
                EditorGUILayout.BeginHorizontal();
                {
                    if (GUILayout.Button(entry.material.name, EditorStyles.boldLabel))
                    {
                        selectedMaterial = entry;
                        Selection.activeObject = entry.material;
                    }
                    
                    // Favorite toggle
                    string star = entry.isFavorite ? "★" : "☆";
                    if (GUILayout.Button(star, GUILayout.Width(25)))
                    {
                        entry.isFavorite = !entry.isFavorite;
                    }
                }
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.LabelField("Shader: " + entry.shaderName, EditorStyles.miniLabel);
                EditorGUILayout.LabelField("Path: " + entry.path, EditorStyles.miniLabel);
            }
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndHorizontal();
        
        GUILayout.Space(2);
    }
    
    #endregion
    
    #region Inspector Panel
    
    private void DrawInspectorPanel()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(position.width * 0.4f - 10));
        {
            EditorGUILayout.LabelField("Material Inspector", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            
            if (selectedMaterial != null && selectedMaterial.material != null)
            {
                // Material preview
                if (selectedMaterial.thumbnail != null)
                {
                    GUILayout.Box(selectedMaterial.thumbnail, GUILayout.Height(128), GUILayout.ExpandWidth(true));
                }
                
                EditorGUILayout.Space();
                
                // Material info
                EditorGUILayout.LabelField("Name:", selectedMaterial.material.name);
                EditorGUILayout.LabelField("Shader:", selectedMaterial.shaderName);
                
                EditorGUILayout.Space();
                
                // Actions
                if (GUILayout.Button("Select in Project"))
                {
                    EditorGUIUtility.PingObject(selectedMaterial.material);
                    Selection.activeObject = selectedMaterial.material;
                }
                
                if (GUILayout.Button("Regenerate Thumbnail"))
                {
                    GenerateThumbnail(selectedMaterial);
                }
                
                if (GUILayout.Button("Find Objects Using This Material"))
                {
                    FindObjectsUsingMaterial(selectedMaterial.material);
                }
                
                EditorGUILayout.Space();
                
                // Properties preview
                EditorGUILayout.LabelField("Material Properties:", EditorStyles.boldLabel);
                
                Material mat = selectedMaterial.material;
                Shader shader = mat.shader;
                
                if (shader != null)
                {
                    EditorGUI.BeginDisabledGroup(true);
                    int propertyCount = ShaderUtil.GetPropertyCount(shader);
                    for (int i = 0; i < Mathf.Min(propertyCount, 10); i++)
                    {
                        string propName = ShaderUtil.GetPropertyName(shader, i);
                        ShaderUtil.ShaderPropertyType propType = ShaderUtil.GetPropertyType(shader, i);
                        
                        switch (propType)
                        {
                            case ShaderUtil.ShaderPropertyType.Color:
                                EditorGUILayout.ColorField(propName, mat.GetColor(propName));
                                break;
                            case ShaderUtil.ShaderPropertyType.Float:
                            case ShaderUtil.ShaderPropertyType.Range:
                                EditorGUILayout.FloatField(propName, mat.GetFloat(propName));
                                break;
                            case ShaderUtil.ShaderPropertyType.TexEnv:
                                EditorGUILayout.ObjectField(propName, mat.GetTexture(propName), typeof(Texture), false);
                                break;
                        }
                    }
                    if (propertyCount > 10)
                    {
                        EditorGUILayout.LabelField($"... and {propertyCount - 10} more properties", EditorStyles.miniLabel);
                    }
                    EditorGUI.EndDisabledGroup();
                }
            }
            else
            {
                EditorGUILayout.LabelField("Select a material to view details", EditorStyles.centeredGreyMiniLabel);
            }
        }
        EditorGUILayout.EndVertical();
    }
    
    #endregion
    
    #region Batch Operations
    
    private void DrawBatchOperationsPanel()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        {
            EditorGUILayout.LabelField("Batch Operations", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            {
                // Replace Material
                EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(position.width * 0.33f - 10));
                {
                    EditorGUILayout.LabelField("Replace Material", EditorStyles.miniLabel);
                    batchReplacementMaterial = (Material)EditorGUILayout.ObjectField(
                        "New Material", batchReplacementMaterial, typeof(Material), false);
                    
                    EditorGUI.BeginDisabledGroup(batchReplacementMaterial == null);
                    if (GUILayout.Button("Replace Selected"))
                    {
                        BatchReplaceMaterial();
                    }
                    EditorGUI.EndDisabledGroup();
                }
                EditorGUILayout.EndVertical();
                
                // Set Property Value
                EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(position.width * 0.33f - 10));
                {
                    EditorGUILayout.LabelField("Set Property Value", EditorStyles.miniLabel);
                    batchPropertyName = EditorGUILayout.TextField("Property Name", batchPropertyName);
                    batchFloatValue = EditorGUILayout.FloatField("Float Value", batchFloatValue);
                    
                    EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(batchPropertyName));
                    if (GUILayout.Button("Apply to Filtered"))
                    {
                        BatchSetFloatProperty();
                    }
                    EditorGUI.EndDisabledGroup();
                }
                EditorGUILayout.EndVertical();
                
                // Set Color
                EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(position.width * 0.33f - 10));
                {
                    EditorGUILayout.LabelField("Set Color Property", EditorStyles.miniLabel);
                    batchColorValue = EditorGUILayout.ColorField("Color", batchColorValue);
                    
                    EditorGUI.BeginDisabledGroup(string.IsNullOrEmpty(batchPropertyName));
                    if (GUILayout.Button("Apply to Filtered"))
                    {
                        BatchSetColorProperty();
                    }
                    EditorGUI.EndDisabledGroup();
                }
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndVertical();
    }
    
    #endregion
    
    #region Core Functions
    
    private void RefreshLibrary()
    {
        libraryData.materials.Clear();
        availableShaders.Clear();
        
        string[] materialGUIDs = AssetDatabase.FindAssets("t:Material");
        
        EditorUtility.DisplayProgressBar("Refreshing Library", "Scanning materials...", 0);
        
        for (int i = 0; i < materialGUIDs.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(materialGUIDs[i]);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            
            if (mat != null)
            {
                MaterialEntry entry = new MaterialEntry(mat, path);
                libraryData.materials.Add(entry);
                
                if (!availableShaders.Contains(entry.shaderName))
                {
                    availableShaders.Add(entry.shaderName);
                }
            }
            
            float progress = (float)i / materialGUIDs.Length;
            EditorUtility.DisplayProgressBar("Refreshing Library", $"Processing {i + 1}/{materialGUIDs.Length}", progress);
        }
        
        availableShaders.Sort();
        EditorUtility.ClearProgressBar();
        
        Debug.Log($"Material Library refreshed: {libraryData.materials.Count} materials found");
    }
    
    private List<MaterialEntry> GetFilteredMaterials()
    {
        return libraryData.materials.Where(m =>
        {
            // Search filter
            if (!string.IsNullOrEmpty(searchQuery))
            {
                bool matchesName = m.material.name.ToLower().Contains(searchQuery.ToLower());
                bool matchesShader = m.shaderName.ToLower().Contains(searchQuery.ToLower());
                if (!matchesName && !matchesShader) return false;
            }
            
            // Shader filter
            if (selectedShaderFilter != "All" && m.shaderName != selectedShaderFilter)
                return false;
            
            // Favorites filter
            if (showFavoritesOnly && !m.isFavorite)
                return false;
            
            return true;
        }).ToList();
    }
    
    #endregion
    
    #region Thumbnail Generation
    
    private void GenerateAllThumbnails()
    {
        for (int i = 0; i < libraryData.materials.Count; i++)
        {
            GenerateThumbnail(libraryData.materials[i]);
            
            float progress = (float)i / libraryData.materials.Count;
            EditorUtility.DisplayProgressBar("Generating Thumbnails", 
                $"Processing {i + 1}/{libraryData.materials.Count}", progress);
        }
        
        EditorUtility.ClearProgressBar();
        Repaint();
    }
    
    private void GenerateThumbnail(MaterialEntry entry)
    {
        if (entry.material == null) return;
        
        // Create preview texture
        entry.thumbnail = AssetPreview.GetAssetPreview(entry.material);
        
        if (entry.thumbnail == null)
        {
            // Force preview generation
            AssetPreview.GetAssetPreview(entry.material);
            entry.thumbnail = AssetPreview.GetMiniThumbnail(entry.material);
        }
    }
    
    #endregion
    
    #region Batch Operations Implementation
    
    private void BatchReplaceMaterial()
    {
        if (selectedMaterial == null || batchReplacementMaterial == null) return;
        
        // Find all renderers using the selected material
        Renderer[] allRenderers = FindObjectsOfType<Renderer>();
        int replacedCount = 0;
        
        foreach (Renderer renderer in allRenderers)
        {
            Material[] materials = renderer.sharedMaterials;
            bool modified = false;
            
            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] == selectedMaterial.material)
                {
                    materials[i] = batchReplacementMaterial;
                    modified = true;
                    replacedCount++;
                }
            }
            
            if (modified)
            {
                Undo.RecordObject(renderer, "Replace Material");
                renderer.sharedMaterials = materials;
            }
        }
        
        Debug.Log($"Replaced material on {replacedCount} renderer(s)");
        EditorUtility.DisplayDialog("Batch Replace", $"Replaced material on {replacedCount} renderer(s)", "OK");
    }
    
    private void BatchSetFloatProperty()
    {
        var filtered = GetFilteredMaterials();
        int modifiedCount = 0;
        
        foreach (var entry in filtered)
        {
            if (entry.material.HasProperty(batchPropertyName))
            {
                Undo.RecordObject(entry.material, "Batch Set Float Property");
                entry.material.SetFloat(batchPropertyName, batchFloatValue);
                EditorUtility.SetDirty(entry.material);
                modifiedCount++;
            }
        }
        
        AssetDatabase.SaveAssets();
        Debug.Log($"Modified {modifiedCount} materials");
        EditorUtility.DisplayDialog("Batch Operation", $"Modified {modifiedCount} materials", "OK");
    }
    
    private void BatchSetColorProperty()
    {
        var filtered = GetFilteredMaterials();
        int modifiedCount = 0;
        
        foreach (var entry in filtered)
        {
            if (entry.material.HasProperty(batchPropertyName))
            {
                Undo.RecordObject(entry.material, "Batch Set Color Property");
                entry.material.SetColor(batchPropertyName, batchColorValue);
                EditorUtility.SetDirty(entry.material);
                modifiedCount++;
            }
        }
        
        AssetDatabase.SaveAssets();
        Debug.Log($"Modified {modifiedCount} materials");
        EditorUtility.DisplayDialog("Batch Operation", $"Modified {modifiedCount} materials", "OK");
    }
    
    #endregion
    
    #region Utility Functions
    
    private void FindObjectsUsingMaterial(Material material)
    {
        List<GameObject> objects = new List<GameObject>();
        Renderer[] allRenderers = FindObjectsOfType<Renderer>();
        
        foreach (Renderer renderer in allRenderers)
        {
            if (renderer.sharedMaterials.Contains(material))
            {
                objects.Add(renderer.gameObject);
            }
        }
        
        if (objects.Count > 0)
        {
            Selection.objects = objects.ToArray();
            Debug.Log($"Found {objects.Count} object(s) using material '{material.name}'");
        }
        else
        {
            Debug.Log($"No objects found using material '{material.name}'");
        }
    }
    
    private void ShowSettings()
    {
        GenericMenu menu = new GenericMenu();
        menu.AddItem(new GUIContent("Thumbnail Size/Small (48px)"), thumbnailSize == 48f, () => thumbnailSize = 48f);
        menu.AddItem(new GUIContent("Thumbnail Size/Medium (64px)"), thumbnailSize == 64f, () => thumbnailSize = 64f);
        menu.AddItem(new GUIContent("Thumbnail Size/Large (96px)"), thumbnailSize == 96f, () => thumbnailSize = 96f);
        menu.AddSeparator("");
        menu.AddItem(new GUIContent("Reset All Favorites"), false, ResetAllFavorites);
        menu.ShowAsContext();
    }
    
    private void ResetAllFavorites()
    {
        foreach (var entry in libraryData.materials)
        {
            entry.isFavorite = false;
        }
    }
    
    private Texture2D MakeTex(int width, int height, Color col)
    {
        Color[] pix = new Color[width * height];
        for (int i = 0; i < pix.Length; i++)
            pix[i] = col;
        
        Texture2D result = new Texture2D(width, height);
        result.SetPixels(pix);
        result.Apply();
        return result;
    }
    
    #endregion
    
    #region Preferences
    
    private void SavePreferences()
    {
        EditorPrefs.SetBool("MaterialLibrary_ShowThumbnails", showThumbnails);
        EditorPrefs.SetFloat("MaterialLibrary_ThumbnailSize", thumbnailSize);
        EditorPrefs.SetString("MaterialLibrary_ShaderFilter", selectedShaderFilter);
    }
    
    private void LoadPreferences()
    {
        showThumbnails = EditorPrefs.GetBool("MaterialLibrary_ShowThumbnails", true);
        thumbnailSize = EditorPrefs.GetFloat("MaterialLibrary_ThumbnailSize", 64f);
        selectedShaderFilter = EditorPrefs.GetString("MaterialLibrary_ShaderFilter", "All");
    }
    
    #endregion
}
