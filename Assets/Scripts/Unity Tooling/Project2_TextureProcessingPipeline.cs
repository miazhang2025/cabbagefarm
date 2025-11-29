using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Texture Processing Pipeline - Foundation Project 2
/// Automated texture import pipeline with naming conventions, compression, and variants
/// Automatically processes textures on import based on configurable rules
/// </summary>

#region Configuration Asset

[CreateAssetMenu(fileName = "TexturePipelineConfig", menuName = "Pipeline/Texture Pipeline Config")]
public class TexturePipelineConfig : ScriptableObject
{
    [System.Serializable]
    public class TextureRule
    {
        public string folderKeyword;
        public string nameSuffix;
        public TextureImporterType textureType;
        public int maxSize = 2048;
        public TextureImporterCompression compression = TextureImporterCompression.Compressed;
        public bool generateMipmaps = true;
        public TextureWrapMode wrapMode = TextureWrapMode.Repeat;
        public FilterMode filterMode = FilterMode.Bilinear;
        public bool sRGBTexture = true;
    }
    
    [Header("General Settings")]
    public bool enableAutomaticProcessing = true;
    public bool showImportLogs = true;
    public bool enforceNamingConventions = true;
    
    [Header("Naming Conventions")]
    public string texturePrefix = "TEX_";
    public string diffuseSuffix = "_D";
    public string normalSuffix = "_N";
    public string roughnessSuffix = "_R";
    public string metallicSuffix = "_M";
    public string aoSuffix = "_AO";
    public string heightSuffix = "_H";
    public string emissiveSuffix = "_E";
    
    [Header("Default Settings")]
    public int defaultMaxSize = 2048;
    public TextureImporterCompression defaultCompression = TextureImporterCompression.Compressed;
    
    [Header("Platform Overrides")]
    public bool enablePlatformOverrides = true;
    
    [System.Serializable]
    public class PlatformSettings
    {
        public string platform;
        public int maxTextureSize = 2048;
        public TextureImporterFormat format;
        public int compressionQuality = 50;
    }
    
    public List<PlatformSettings> androidSettings = new List<PlatformSettings>();
    public List<PlatformSettings> iosSettings = new List<PlatformSettings>();
    
    [Header("Processing Rules")]
    public List<TextureRule> textureRules = new List<TextureRule>();
    
    [Header("Validation")]
    public bool validatePowerOfTwo = true;
    public bool validateMaxSize = true;
    public int maxAllowedSize = 4096;
    
    [Header("Atlas Generation")]
    public bool autoGenerateAtlases = false;
    public List<string> atlasFolders = new List<string>();
    public int atlasMaxSize = 2048;
    
    public void InitializeDefaultRules()
    {
        textureRules.Clear();
        
        // Diffuse/Albedo textures
        textureRules.Add(new TextureRule
        {
            folderKeyword = "",
            nameSuffix = diffuseSuffix,
            textureType = TextureImporterType.Default,
            maxSize = 2048,
            compression = TextureImporterCompression.Compressed,
            sRGBTexture = true
        });
        
        // Normal maps
        textureRules.Add(new TextureRule
        {
            folderKeyword = "Normal",
            nameSuffix = normalSuffix,
            textureType = TextureImporterType.NormalMap,
            maxSize = 2048,
            compression = TextureImporterCompression.Compressed,
            sRGBTexture = false
        });
        
        // UI textures
        textureRules.Add(new TextureRule
        {
            folderKeyword = "UI",
            nameSuffix = "",
            textureType = TextureImporterType.Sprite,
            maxSize = 1024,
            compression = TextureImporterCompression.Compressed,
            generateMipmaps = false,
            filterMode = FilterMode.Bilinear
        });
        
        // Roughness/Metallic/AO
        textureRules.Add(new TextureRule
        {
            folderKeyword = "",
            nameSuffix = roughnessSuffix,
            textureType = TextureImporterType.Default,
            maxSize = 1024,
            compression = TextureImporterCompression.Compressed,
            sRGBTexture = false
        });
    }
}

#endregion

#region Asset Postprocessor

public class TexturePipelineProcessor : AssetPostprocessor
{
    private static TexturePipelineConfig config;
    
    private static TexturePipelineConfig GetConfig()
    {
        if (config == null)
        {
            string[] guids = AssetDatabase.FindAssets("t:TexturePipelineConfig");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                config = AssetDatabase.LoadAssetAtPath<TexturePipelineConfig>(path);
            }
        }
        return config;
    }
    
    void OnPreprocessTexture()
    {
        TexturePipelineConfig cfg = GetConfig();
        if (cfg == null || !cfg.enableAutomaticProcessing) return;
        
        TextureImporter importer = (TextureImporter)assetImporter;
        
        // Apply naming convention validation
        if (cfg.enforceNamingConventions)
        {
            ValidateNaming(cfg, assetPath);
        }
        
        // Find and apply matching rule
        TexturePipelineConfig.TextureRule matchedRule = FindMatchingRule(cfg, assetPath);
        
        if (matchedRule != null)
        {
            ApplyRule(importer, matchedRule);
            
            if (cfg.showImportLogs)
            {
                Debug.Log($"[Texture Pipeline] Applied rule to: {Path.GetFileName(assetPath)}");
            }
        }
        else
        {
            // Apply default settings
            ApplyDefaultSettings(importer, cfg);
        }
        
        // Apply platform-specific settings
        if (cfg.enablePlatformOverrides)
        {
            ApplyPlatformSettings(importer, cfg);
        }
    }
    
    void OnPostprocessTexture(Texture2D texture)
    {
        TexturePipelineConfig cfg = GetConfig();
        if (cfg == null) return;
        
        // Validation
        if (cfg.validatePowerOfTwo)
        {
            if (!IsPowerOfTwo(texture.width) || !IsPowerOfTwo(texture.height))
            {
                Debug.LogWarning($"[Texture Pipeline] Texture is not power of 2: {assetPath} ({texture.width}x{texture.height})");
            }
        }
        
        if (cfg.validateMaxSize)
        {
            if (texture.width > cfg.maxAllowedSize || texture.height > cfg.maxAllowedSize)
            {
                Debug.LogError($"[Texture Pipeline] Texture exceeds maximum size: {assetPath} ({texture.width}x{texture.height})");
            }
        }
    }
    
    private TexturePipelineConfig.TextureRule FindMatchingRule(TexturePipelineConfig cfg, string path)
    {
        string filename = Path.GetFileNameWithoutExtension(path);
        string directory = Path.GetDirectoryName(path);
        
        foreach (var rule in cfg.textureRules)
        {
            // Check folder keyword
            bool folderMatch = string.IsNullOrEmpty(rule.folderKeyword) || 
                             directory.Contains(rule.folderKeyword);
            
            // Check name suffix
            bool suffixMatch = string.IsNullOrEmpty(rule.nameSuffix) || 
                             filename.EndsWith(rule.nameSuffix);
            
            if (folderMatch && suffixMatch)
            {
                return rule;
            }
        }
        
        return null;
    }
    
    private void ApplyRule(TextureImporter importer, TexturePipelineConfig.TextureRule rule)
    {
        importer.textureType = rule.textureType;
        importer.maxTextureSize = rule.maxSize;
        importer.textureCompression = rule.compression;
        importer.mipmapEnabled = rule.generateMipmaps;
        importer.wrapMode = rule.wrapMode;
        importer.filterMode = rule.filterMode;
        importer.sRGBTexture = rule.sRGBTexture;
        
        // Disable Read/Write for optimization
        importer.isReadable = false;
    }
    
    private void ApplyDefaultSettings(TextureImporter importer, TexturePipelineConfig cfg)
    {
        importer.maxTextureSize = cfg.defaultMaxSize;
        importer.textureCompression = cfg.defaultCompression;
        importer.isReadable = false;
    }
    
    private void ApplyPlatformSettings(TextureImporter importer, TexturePipelineConfig cfg)
    {
        // Android settings
        foreach (var setting in cfg.androidSettings)
        {
            TextureImporterPlatformSettings platformSettings = new TextureImporterPlatformSettings();
            platformSettings.name = "Android";
            platformSettings.overridden = true;
            platformSettings.maxTextureSize = setting.maxTextureSize;
            platformSettings.format = setting.format;
            platformSettings.compressionQuality = setting.compressionQuality;
            
            importer.SetPlatformTextureSettings(platformSettings);
        }
        
        // iOS settings
        foreach (var setting in cfg.iosSettings)
        {
            TextureImporterPlatformSettings platformSettings = new TextureImporterPlatformSettings();
            platformSettings.name = "iPhone";
            platformSettings.overridden = true;
            platformSettings.maxTextureSize = setting.maxTextureSize;
            platformSettings.format = setting.format;
            platformSettings.compressionQuality = setting.compressionQuality;
            
            importer.SetPlatformTextureSettings(platformSettings);
        }
    }
    
    private void ValidateNaming(TexturePipelineConfig cfg, string path)
    {
        string filename = Path.GetFileNameWithoutExtension(path);
        
        if (!filename.StartsWith(cfg.texturePrefix))
        {
            Debug.LogWarning($"[Texture Pipeline] Texture doesn't follow naming convention: {filename} (Expected prefix: {cfg.texturePrefix})");
        }
    }
    
    private bool IsPowerOfTwo(int value)
    {
        return (value & (value - 1)) == 0 && value > 0;
    }
}

#endregion

#region Editor Window

public class TexturePipelineWindow : EditorWindow
{
    private TexturePipelineConfig config;
    private Vector2 scrollPosition;
    private bool showRules = true;
    private bool showValidation = true;
    private bool showBatchOps = true;
    
    // Batch operations
    private int batchMaxSize = 2048;
    private TextureImporterCompression batchCompression = TextureImporterCompression.Compressed;
    
    [MenuItem("Window/Texture Processing Pipeline")]
    public static void ShowWindow()
    {
        TexturePipelineWindow window = GetWindow<TexturePipelineWindow>("Texture Pipeline");
        window.minSize = new Vector2(500, 400);
        window.Show();
    }
    
    private void OnEnable()
    {
        LoadConfig();
    }
    
    private void LoadConfig()
    {
        string[] guids = AssetDatabase.FindAssets("t:TexturePipelineConfig");
        
        if (guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            config = AssetDatabase.LoadAssetAtPath<TexturePipelineConfig>(path);
        }
        else
        {
            // Create default config
            if (EditorUtility.DisplayDialog("Create Config", 
                "No Texture Pipeline Config found. Create one?", "Yes", "No"))
            {
                CreateDefaultConfig();
            }
        }
    }
    
    private void CreateDefaultConfig()
    {
        config = CreateInstance<TexturePipelineConfig>();
        config.InitializeDefaultRules();
        
        string path = "Assets/TexturePipelineConfig.asset";
        AssetDatabase.CreateAsset(config, path);
        AssetDatabase.SaveAssets();
        
        EditorUtility.FocusProjectWindow();
        Selection.activeObject = config;
        
        Debug.Log($"Created Texture Pipeline Config at: {path}");
    }
    
    private void OnGUI()
    {
        if (config == null)
        {
            EditorGUILayout.HelpBox("No Texture Pipeline Config found. Please create one.", MessageType.Warning);
            
            if (GUILayout.Button("Create Config"))
            {
                CreateDefaultConfig();
            }
            return;
        }
        
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        {
            DrawHeader();
            DrawMainSettings();
            DrawRulesSection();
            DrawValidationSection();
            DrawBatchOperations();
            DrawUtilities();
        }
        EditorGUILayout.EndScrollView();
    }
    
    private void DrawHeader()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        {
            EditorGUILayout.LabelField("Texture Processing Pipeline", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            
            EditorGUILayout.BeginHorizontal();
            {
                EditorGUILayout.LabelField("Config Asset:", GUILayout.Width(100));
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.ObjectField(config, typeof(TexturePipelineConfig), false);
                EditorGUI.EndDisabledGroup();
                
                if (GUILayout.Button("Select", GUILayout.Width(60)))
                {
                    Selection.activeObject = config;
                    EditorGUIUtility.PingObject(config);
                }
            }
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndVertical();
    }
    
    private void DrawMainSettings()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        {
            EditorGUILayout.LabelField("Main Settings", EditorStyles.boldLabel);
            
            config.enableAutomaticProcessing = EditorGUILayout.Toggle("Auto Processing", config.enableAutomaticProcessing);
            config.showImportLogs = EditorGUILayout.Toggle("Show Import Logs", config.showImportLogs);
            config.enforceNamingConventions = EditorGUILayout.Toggle("Enforce Naming", config.enforceNamingConventions);
            
            EditorGUILayout.Space();
            
            config.defaultMaxSize = EditorGUILayout.IntField("Default Max Size", config.defaultMaxSize);
            config.defaultCompression = (TextureImporterCompression)EditorGUILayout.EnumPopup(
                "Default Compression", config.defaultCompression);
        }
        EditorGUILayout.EndVertical();
    }
    
    private void DrawRulesSection()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        {
            showRules = EditorGUILayout.Foldout(showRules, $"Processing Rules ({config.textureRules.Count})", true);
            
            if (showRules)
            {
                EditorGUI.indentLevel++;
                
                for (int i = 0; i < config.textureRules.Count; i++)
                {
                    DrawRule(i);
                }
                
                EditorGUILayout.Space();
                
                if (GUILayout.Button("+ Add Rule"))
                {
                    config.textureRules.Add(new TexturePipelineConfig.TextureRule());
                }
                
                if (GUILayout.Button("Reset to Defaults"))
                {
                    if (EditorUtility.DisplayDialog("Reset Rules", 
                        "Reset all rules to defaults?", "Yes", "No"))
                    {
                        config.InitializeDefaultRules();
                    }
                }
                
                EditorGUI.indentLevel--;
            }
        }
        EditorGUILayout.EndVertical();
    }
    
    private void DrawRule(int index)
    {
        var rule = config.textureRules[index];
        
        EditorGUILayout.BeginVertical(GUI.skin.box);
        {
            EditorGUILayout.BeginHorizontal();
            {
                EditorGUILayout.LabelField($"Rule {index + 1}", EditorStyles.boldLabel);
                
                if (GUILayout.Button("×", GUILayout.Width(25)))
                {
                    config.textureRules.RemoveAt(index);
                    return;
                }
            }
            EditorGUILayout.EndHorizontal();
            
            rule.folderKeyword = EditorGUILayout.TextField("Folder Keyword", rule.folderKeyword);
            rule.nameSuffix = EditorGUILayout.TextField("Name Suffix", rule.nameSuffix);
            rule.textureType = (TextureImporterType)EditorGUILayout.EnumPopup("Type", rule.textureType);
            rule.maxSize = EditorGUILayout.IntField("Max Size", rule.maxSize);
            rule.compression = (TextureImporterCompression)EditorGUILayout.EnumPopup("Compression", rule.compression);
            rule.generateMipmaps = EditorGUILayout.Toggle("Generate Mipmaps", rule.generateMipmaps);
            rule.sRGBTexture = EditorGUILayout.Toggle("sRGB", rule.sRGBTexture);
        }
        EditorGUILayout.EndVertical();
        
        EditorGUILayout.Space();
    }
    
    private void DrawValidationSection()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        {
            showValidation = EditorGUILayout.Foldout(showValidation, "Validation Settings", true);
            
            if (showValidation)
            {
                EditorGUI.indentLevel++;
                
                config.validatePowerOfTwo = EditorGUILayout.Toggle("Validate Power of 2", config.validatePowerOfTwo);
                config.validateMaxSize = EditorGUILayout.Toggle("Validate Max Size", config.validateMaxSize);
                
                if (config.validateMaxSize)
                {
                    config.maxAllowedSize = EditorGUILayout.IntField("Max Allowed Size", config.maxAllowedSize);
                }
                
                EditorGUILayout.Space();
                
                if (GUILayout.Button("Validate All Textures"))
                {
                    ValidateAllTextures();
                }
                
                EditorGUI.indentLevel--;
            }
        }
        EditorGUILayout.EndVertical();
    }
    
    private void DrawBatchOperations()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        {
            showBatchOps = EditorGUILayout.Foldout(showBatchOps, "Batch Operations", true);
            
            if (showBatchOps)
            {
                EditorGUI.indentLevel++;
                
                EditorGUILayout.LabelField("Batch Settings", EditorStyles.miniLabel);
                batchMaxSize = EditorGUILayout.IntField("Max Size", batchMaxSize);
                batchCompression = (TextureImporterCompression)EditorGUILayout.EnumPopup("Compression", batchCompression);
                
                EditorGUILayout.Space();
                
                if (GUILayout.Button("Apply to Selected Textures"))
                {
                    BatchProcessSelected();
                }
                
                if (GUILayout.Button("Apply to All Textures"))
                {
                    if (EditorUtility.DisplayDialog("Batch Process", 
                        "Process all textures in project?", "Yes", "No"))
                    {
                        BatchProcessAll();
                    }
                }
                
                if (GUILayout.Button("Generate All Mipmaps"))
                {
                    BatchGenerateMipmaps();
                }
                
                EditorGUI.indentLevel--;
            }
        }
        EditorGUILayout.EndVertical();
    }
    
    private void DrawUtilities()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        {
            EditorGUILayout.LabelField("Utilities", EditorStyles.boldLabel);
            
            if (GUILayout.Button("Generate Texture Report"))
            {
                GenerateTextureReport();
            }
            
            if (GUILayout.Button("Find Oversized Textures"))
            {
                FindOversizedTextures();
            }
            
            if (GUILayout.Button("Fix Texture Names"))
            {
                FixTextureNames();
            }
        }
        EditorGUILayout.EndVertical();
    }
    
    #region Batch Operations Implementation
    
    private void BatchProcessSelected()
    {
        Object[] selected = Selection.GetFiltered<Texture2D>(SelectionMode.Assets);
        
        if (selected.Length == 0)
        {
            EditorUtility.DisplayDialog("No Selection", "Please select textures to process", "OK");
            return;
        }
        
        EditorUtility.DisplayProgressBar("Processing", "Textures", 0);
        
        for (int i = 0; i < selected.Length; i++)
        {
            string path = AssetDatabase.GetAssetPath(selected[i]);
            ProcessTexture(path);
            
            float progress = (float)(i + 1) / selected.Length;
            EditorUtility.DisplayProgressBar("Processing", path, progress);
        }
        
        EditorUtility.ClearProgressBar();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        
        Debug.Log($"Batch processed {selected.Length} textures");
    }
    
    private void BatchProcessAll()
    {
        string[] guids = AssetDatabase.FindAssets("t:Texture2D");
        
        EditorUtility.DisplayProgressBar("Processing", "All Textures", 0);
        
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            ProcessTexture(path);
            
            float progress = (float)(i + 1) / guids.Length;
            EditorUtility.DisplayProgressBar("Processing", path, progress);
        }
        
        EditorUtility.ClearProgressBar();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        
        Debug.Log($"Batch processed {guids.Length} textures");
    }
    
    private void ProcessTexture(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        
        if (importer != null)
        {
            importer.maxTextureSize = batchMaxSize;
            importer.textureCompression = batchCompression;
            importer.SaveAndReimport();
        }
    }
    
    private void BatchGenerateMipmaps()
    {
        string[] guids = AssetDatabase.FindAssets("t:Texture2D");
        int processedCount = 0;
        
        EditorUtility.DisplayProgressBar("Generating Mipmaps", "", 0);
        
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            
            if (importer != null && !importer.mipmapEnabled)
            {
                importer.mipmapEnabled = true;
                importer.SaveAndReimport();
                processedCount++;
            }
            
            float progress = (float)(i + 1) / guids.Length;
            EditorUtility.DisplayProgressBar("Generating Mipmaps", path, progress);
        }
        
        EditorUtility.ClearProgressBar();
        Debug.Log($"Generated mipmaps for {processedCount} textures");
    }
    
    #endregion
    
    #region Utility Functions
    
    private void ValidateAllTextures()
    {
        string[] guids = AssetDatabase.FindAssets("t:Texture2D");
        List<string> issues = new List<string>();
        
        EditorUtility.DisplayProgressBar("Validating", "Textures", 0);
        
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            
            if (tex != null)
            {
                // Check power of 2
                if (config.validatePowerOfTwo)
                {
                    if (!IsPowerOfTwo(tex.width) || !IsPowerOfTwo(tex.height))
                    {
                        issues.Add($"{path}: Not power of 2 ({tex.width}x{tex.height})");
                    }
                }
                
                // Check max size
                if (config.validateMaxSize)
                {
                    if (tex.width > config.maxAllowedSize || tex.height > config.maxAllowedSize)
                    {
                        issues.Add($"{path}: Exceeds max size ({tex.width}x{tex.height})");
                    }
                }
            }
            
            float progress = (float)(i + 1) / guids.Length;
            EditorUtility.DisplayProgressBar("Validating", path, progress);
        }
        
        EditorUtility.ClearProgressBar();
        
        if (issues.Count > 0)
        {
            string report = string.Join("\n", issues);
            Debug.LogWarning($"Validation found {issues.Count} issues:\n{report}");
            EditorUtility.DisplayDialog("Validation Complete", $"Found {issues.Count} issues. Check console for details.", "OK");
        }
        else
        {
            EditorUtility.DisplayDialog("Validation Complete", "All textures passed validation!", "OK");
        }
    }
    
    private void GenerateTextureReport()
    {
        string[] guids = AssetDatabase.FindAssets("t:Texture2D");
        long totalMemory = 0;
        Dictionary<string, int> formatCounts = new Dictionary<string, int>();
        
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            
            if (tex != null)
            {
                // Calculate memory
                long memory = CalculateTextureMemory(tex);
                totalMemory += memory;
                
                // Count formats
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer != null)
                {
                    string format = importer.textureCompression.ToString();
                    if (!formatCounts.ContainsKey(format))
                        formatCounts[format] = 0;
                    formatCounts[format]++;
                }
            }
        }
        
        // Generate report
        System.Text.StringBuilder report = new System.Text.StringBuilder();
        report.AppendLine("=== TEXTURE REPORT ===");
        report.AppendLine($"Total Textures: {guids.Length}");
        report.AppendLine($"Total Memory: {FormatBytes(totalMemory)}");
        report.AppendLine();
        report.AppendLine("Compression Formats:");
        
        foreach (var kvp in formatCounts.OrderByDescending(x => x.Value))
        {
            report.AppendLine($"  {kvp.Key}: {kvp.Value}");
        }
        
        Debug.Log(report.ToString());
        EditorUtility.DisplayDialog("Texture Report", "Report generated. Check console.", "OK");
    }
    
    private void FindOversizedTextures()
    {
        string[] guids = AssetDatabase.FindAssets("t:Texture2D");
        List<string> oversized = new List<string>();
        
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            
            if (tex != null && (tex.width > config.maxAllowedSize || tex.height > config.maxAllowedSize))
            {
                oversized.Add($"{Path.GetFileName(path)}: {tex.width}x{tex.height}");
            }
        }
        
        if (oversized.Count > 0)
        {
            string report = string.Join("\n", oversized);
            Debug.LogWarning($"Found {oversized.Count} oversized textures:\n{report}");
        }
        else
        {
            Debug.Log("No oversized textures found");
        }
    }
    
    private void FixTextureNames()
    {
        string[] guids = AssetDatabase.FindAssets("t:Texture2D");
        int renamedCount = 0;
        
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string filename = Path.GetFileNameWithoutExtension(path);
            
            if (!filename.StartsWith(config.texturePrefix))
            {
                string newName = config.texturePrefix + filename;
                string newPath = Path.GetDirectoryName(path) + "/" + newName + Path.GetExtension(path);
                
                string error = AssetDatabase.RenameAsset(path, newName);
                if (string.IsNullOrEmpty(error))
                {
                    renamedCount++;
                }
            }
        }
        
        AssetDatabase.SaveAssets();
        Debug.Log($"Renamed {renamedCount} textures");
    }
    
    private long CalculateTextureMemory(Texture2D texture)
    {
        long memory = texture.width * texture.height * 4; // Assume RGBA32
        if (texture.mipmapCount > 1)
        {
            memory = (long)(memory * 1.33f);
        }
        return memory;
    }
    
    private bool IsPowerOfTwo(int value)
    {
        return (value & (value - 1)) == 0 && value > 0;
    }
    
    private string FormatBytes(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;
        
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        
        return $"{len:0.##} {sizes[order]}";
    }
    
    #endregion
}

#endregion
