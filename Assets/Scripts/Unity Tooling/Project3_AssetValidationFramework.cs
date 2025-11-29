using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System;

/// <summary>
/// Asset Validation Framework - Foundation Project 3
/// Comprehensive validation system with custom rules, reporting, and auto-fixing
/// Usage: Window > Asset Validation Framework
/// </summary>

#region Validation Interfaces and Base Classes

public interface IAssetValidator
{
    string ValidatorName { get; }
    string Description { get; }
    bool IsEnabled { get; set; }
    ValidationResult Validate(UnityEngine.Object asset, string assetPath);
    bool CanAutoFix { get; }
    bool AutoFix(UnityEngine.Object asset, string assetPath);
}

[System.Serializable]
public class ValidationResult
{
    public enum Severity
    {
        Info,
        Warning,
        Error
    }
    
    public bool passed;
    public Severity severity;
    public string message;
    public UnityEngine.Object asset;
    public string assetPath;
    public string validatorName;
    public bool canAutoFix;
    
    public ValidationResult(bool passed, string message, Severity severity = Severity.Error)
    {
        this.passed = passed;
        this.message = message;
        this.severity = severity;
    }
}

public abstract class BaseAssetValidator : IAssetValidator
{
    public abstract string ValidatorName { get; }
    public abstract string Description { get; }
    public bool IsEnabled { get; set; } = true;
    public virtual bool CanAutoFix => false;
    
    public abstract ValidationResult Validate(UnityEngine.Object asset, string assetPath);
    
    public virtual bool AutoFix(UnityEngine.Object asset, string assetPath)
    {
        return false;
    }
    
    protected ValidationResult Pass(string message = "Validation passed")
    {
        return new ValidationResult(true, message, ValidationResult.Severity.Info);
    }
    
    protected ValidationResult Warning(string message)
    {
        return new ValidationResult(false, message, ValidationResult.Severity.Warning);
    }
    
    protected ValidationResult Error(string message)
    {
        return new ValidationResult(false, message, ValidationResult.Severity.Error);
    }
}

#endregion

#region Concrete Validators

// Naming Convention Validator
public class NamingConventionValidator : BaseAssetValidator
{
    public override string ValidatorName => "Naming Convention";
    public override string Description => "Validates asset naming conventions (prefixes, suffixes)";
    public override bool CanAutoFix => true;
    
    private static readonly Dictionary<string, string> TypePrefixes = new Dictionary<string, string>
    {
        { "Prefab", "PRF_" },
        { "Material", "MAT_" },
        { "Texture2D", "TEX_" },
        { "Mesh", "MSH_" },
        { "AnimationClip", "ANM_" },
        { "AudioClip", "SFX_" }
    };
    
    public override ValidationResult Validate(UnityEngine.Object asset, string assetPath)
    {
        string typeName = asset.GetType().Name;
        
        if (TypePrefixes.TryGetValue(typeName, out string expectedPrefix))
        {
            if (!asset.name.StartsWith(expectedPrefix))
            {
                var result = Warning($"Expected prefix '{expectedPrefix}' but got '{asset.name}'");
                result.canAutoFix = true;
                return result;
            }
        }
        
        return Pass();
    }
    
    public override bool AutoFix(UnityEngine.Object asset, string assetPath)
    {
        string typeName = asset.GetType().Name;
        
        if (TypePrefixes.TryGetValue(typeName, out string prefix))
        {
            string newName = prefix + asset.name;
            string error = AssetDatabase.RenameAsset(assetPath, newName);
            return string.IsNullOrEmpty(error);
        }
        
        return false;
    }
}

// Texture Size Validator
public class TextureSizeValidator : BaseAssetValidator
{
    public override string ValidatorName => "Texture Size";
    public override string Description => "Validates texture dimensions and max size";
    public int MaxSize { get; set; } = 2048;
    public bool RequirePowerOfTwo { get; set; } = true;
    
    public override ValidationResult Validate(UnityEngine.Object asset, string assetPath)
    {
        Texture2D texture = asset as Texture2D;
        if (texture == null) return Pass();
        
        // Check max size
        if (texture.width > MaxSize || texture.height > MaxSize)
        {
            return Error($"Texture size ({texture.width}x{texture.height}) exceeds maximum {MaxSize}");
        }
        
        // Check power of 2
        if (RequirePowerOfTwo)
        {
            if (!IsPowerOfTwo(texture.width) || !IsPowerOfTwo(texture.height))
            {
                return Warning($"Texture is not power of 2: {texture.width}x{texture.height}");
            }
        }
        
        return Pass();
    }
    
    private bool IsPowerOfTwo(int value)
    {
        return (value & (value - 1)) == 0 && value > 0;
    }
}

// Mesh Poly Count Validator
public class MeshPolyCountValidator : BaseAssetValidator
{
    public override string ValidatorName => "Mesh Poly Count";
    public override string Description => "Validates mesh triangle/vertex counts";
    public int MaxTriangles { get; set; } = 10000;
    public int MaxVertices { get; set; } = 10000;
    
    public override ValidationResult Validate(UnityEngine.Object asset, string assetPath)
    {
        Mesh mesh = asset as Mesh;
        if (mesh == null)
        {
            // Check if it's a GameObject with MeshFilter
            GameObject go = asset as GameObject;
            if (go != null)
            {
                MeshFilter filter = go.GetComponentInChildren<MeshFilter>();
                if (filter != null && filter.sharedMesh != null)
                {
                    mesh = filter.sharedMesh;
                }
            }
        }
        
        if (mesh == null) return Pass();
        
        int triangles = mesh.triangles.Length / 3;
        int vertices = mesh.vertexCount;
        
        if (triangles > MaxTriangles)
        {
            return Error($"Mesh has {triangles} triangles (max: {MaxTriangles})");
        }
        
        if (vertices > MaxVertices)
        {
            return Error($"Mesh has {vertices} vertices (max: {MaxVertices})");
        }
        
        return Pass();
    }
}

// Read/Write Enabled Validator
public class ReadWriteEnabledValidator : BaseAssetValidator
{
    public override string ValidatorName => "Read/Write Enabled";
    public override string Description => "Checks for unnecessary Read/Write enabled on meshes";
    public override bool CanAutoFix => true;
    
    public override ValidationResult Validate(UnityEngine.Object asset, string assetPath)
    {
        ModelImporter importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
        
        if (importer != null && importer.isReadable)
        {
            var result = Warning("Mesh has Read/Write enabled (doubles memory usage)");
            result.canAutoFix = true;
            return result;
        }
        
        return Pass();
    }
    
    public override bool AutoFix(UnityEngine.Object asset, string assetPath)
    {
        ModelImporter importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
        
        if (importer != null)
        {
            importer.isReadable = false;
            importer.SaveAndReimport();
            return true;
        }
        
        return false;
    }
}

// Material Shader Validator
public class MaterialShaderValidator : BaseAssetValidator
{
    public override string ValidatorName => "Material Shader";
    public override string Description => "Validates materials have valid shaders";
    
    public override ValidationResult Validate(UnityEngine.Object asset, string assetPath)
    {
        Material material = asset as Material;
        if (material == null) return Pass();
        
        if (material.shader == null)
        {
            return Error("Material has no shader assigned");
        }
        
        if (material.shader.name.Contains("Hidden/InternalErrorShader"))
        {
            return Error("Material has invalid/missing shader");
        }
        
        return Pass();
    }
}

// Missing Reference Validator
public class MissingReferenceValidator : BaseAssetValidator
{
    public override string ValidatorName => "Missing References";
    public override string Description => "Checks for missing component references";
    
    public override ValidationResult Validate(UnityEngine.Object asset, string assetPath)
    {
        GameObject go = asset as GameObject;
        if (go == null) return Pass();
        
        Component[] components = go.GetComponentsInChildren<Component>(true);
        List<string> missingRefs = new List<string>();
        
        foreach (Component comp in components)
        {
            if (comp == null)
            {
                missingRefs.Add("Missing Component");
                continue;
            }
            
            SerializedObject so = new SerializedObject(comp);
            SerializedProperty prop = so.GetIterator();
            
            while (prop.NextVisible(true))
            {
                if (prop.propertyType == SerializedPropertyType.ObjectReference)
                {
                    if (prop.objectReferenceValue == null && 
                        prop.objectReferenceInstanceIDValue != 0)
                    {
                        missingRefs.Add($"{comp.GetType().Name}.{prop.name}");
                    }
                }
            }
        }
        
        if (missingRefs.Count > 0)
        {
            return Error($"Found {missingRefs.Count} missing references: {string.Join(", ", missingRefs)}");
        }
        
        return Pass();
    }
}

// Prefab Variant Validator
public class PrefabVariantValidator : BaseAssetValidator
{
    public override string ValidatorName => "Prefab Variant";
    public override string Description => "Validates prefab variant connections";
    
    public override ValidationResult Validate(UnityEngine.Object asset, string assetPath)
    {
        GameObject go = asset as GameObject;
        if (go == null) return Pass();
        
        PrefabAssetType assetType = PrefabUtility.GetPrefabAssetType(go);
        
        if (assetType == PrefabAssetType.Variant)
        {
            GameObject parent = PrefabUtility.GetCorrespondingObjectFromSource(go);
            if (parent == null)
            {
                return Error("Prefab variant has lost connection to base prefab");
            }
        }
        
        return Pass();
    }
}

#endregion

#region Validation Manager

[CreateAssetMenu(fileName = "ValidationConfig", menuName = "Pipeline/Validation Config")]
public class ValidationConfig : ScriptableObject
{
    [System.Serializable]
    public class ValidatorConfig
    {
        public string validatorType;
        public bool enabled = true;
        public SerializableDict parameters = new SerializableDict();
    }
    
    [System.Serializable]
    public class SerializableDict
    {
        public List<string> keys = new List<string>();
        public List<string> values = new List<string>();
    }
    
    public List<ValidatorConfig> validatorConfigs = new List<ValidatorConfig>();
    public bool validateOnImport = false;
    public bool autoFixOnValidate = false;
}

public class ValidationManager
{
    private List<IAssetValidator> validators = new List<IAssetValidator>();
    private ValidationConfig config;
    
    public ValidationManager()
    {
        InitializeValidators();
    }
    
    private void InitializeValidators()
    {
        validators.Add(new NamingConventionValidator());
        validators.Add(new TextureSizeValidator());
        validators.Add(new MeshPolyCountValidator());
        validators.Add(new ReadWriteEnabledValidator());
        validators.Add(new MaterialShaderValidator());
        validators.Add(new MissingReferenceValidator());
        validators.Add(new PrefabVariantValidator());
    }
    
    public List<IAssetValidator> GetValidators()
    {
        return validators;
    }
    
    public List<ValidationResult> ValidateAsset(UnityEngine.Object asset, string assetPath)
    {
        List<ValidationResult> results = new List<ValidationResult>();
        
        foreach (var validator in validators)
        {
            if (validator.IsEnabled)
            {
                ValidationResult result = validator.Validate(asset, assetPath);
                result.asset = asset;
                result.assetPath = assetPath;
                result.validatorName = validator.ValidatorName;
                results.Add(result);
            }
        }
        
        return results;
    }
    
    public List<ValidationResult> ValidateAssets(UnityEngine.Object[] assets)
    {
        List<ValidationResult> allResults = new List<ValidationResult>();
        
        for (int i = 0; i < assets.Length; i++)
        {
            string path = AssetDatabase.GetAssetPath(assets[i]);
            var results = ValidateAsset(assets[i], path);
            allResults.AddRange(results);
        }
        
        return allResults;
    }
    
    public int AutoFixIssues(List<ValidationResult> results)
    {
        int fixedCount = 0;
        
        foreach (var result in results)
        {
            if (!result.passed && result.canAutoFix)
            {
                var validator = validators.Find(v => v.ValidatorName == result.validatorName);
                if (validator != null && validator.AutoFix(result.asset, result.assetPath))
                {
                    fixedCount++;
                }
            }
        }
        
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        
        return fixedCount;
    }
}

#endregion

#region Editor Window

public class AssetValidationWindow : EditorWindow
{
    private ValidationManager validationManager;
    private Vector2 scrollPosition;
    private Vector2 resultsScrollPosition;
    
    private List<ValidationResult> validationResults = new List<ValidationResult>();
    private bool showPassedTests = false;
    private bool autoFixEnabled = true;
    
    // Filters
    private string searchFilter = "";
    private ValidationResult.Severity severityFilter = ValidationResult.Severity.Error;
    private bool filterBySeverity = false;
    
    // Stats
    private int totalTests = 0;
    private int passedTests = 0;
    private int failedTests = 0;
    private int warningTests = 0;
    private int errorTests = 0;
    
    private GUIStyle errorStyle;
    private GUIStyle warningStyle;
    private GUIStyle passStyle;
    
    [MenuItem("Window/Asset Validation Framework")]
    public static void ShowWindow()
    {
        AssetValidationWindow window = GetWindow<AssetValidationWindow>("Asset Validation");
        window.minSize = new Vector2(600, 400);
        window.Show();
    }
    
    private void OnEnable()
    {
        validationManager = new ValidationManager();
    }
    
    private void OnGUI()
    {
        InitializeStyles();
        
        DrawToolbar();
        DrawValidatorsList();
        DrawResultsPanel();
        DrawStatsPanel();
    }
    
    private void InitializeStyles()
    {
        if (errorStyle == null)
        {
            errorStyle = new GUIStyle(EditorStyles.helpBox);
            errorStyle.normal.textColor = new Color(1f, 0.3f, 0.3f);
        }
        
        if (warningStyle == null)
        {
            warningStyle = new GUIStyle(EditorStyles.helpBox);
            warningStyle.normal.textColor = new Color(1f, 0.8f, 0f);
        }
        
        if (passStyle == null)
        {
            passStyle = new GUIStyle(EditorStyles.helpBox);
            passStyle.normal.textColor = new Color(0.3f, 1f, 0.3f);
        }
    }
    
    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        {
            if (GUILayout.Button("Validate Selected", EditorStyles.toolbarButton, GUILayout.Width(120)))
            {
                ValidateSelected();
            }
            
            if (GUILayout.Button("Validate All", EditorStyles.toolbarButton, GUILayout.Width(100)))
            {
                ValidateAll();
            }
            
            if (GUILayout.Button("Clear Results", EditorStyles.toolbarButton, GUILayout.Width(100)))
            {
                ClearResults();
            }
            
            GUILayout.FlexibleSpace();
            
            autoFixEnabled = GUILayout.Toggle(autoFixEnabled, "Auto-Fix", EditorStyles.toolbarButton, GUILayout.Width(70));
            
            if (GUILayout.Button("Fix Issues", EditorStyles.toolbarButton, GUILayout.Width(80)))
            {
                FixIssues();
            }
        }
        EditorGUILayout.EndHorizontal();
    }
    
    private void DrawValidatorsList()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Height(150));
        {
            EditorGUILayout.LabelField("Validators", EditorStyles.boldLabel);
            
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            {
                var validators = validationManager.GetValidators();
                
                foreach (var validator in validators)
                {
                    EditorGUILayout.BeginHorizontal();
                    {
                        validator.IsEnabled = EditorGUILayout.Toggle(validator.IsEnabled, GUILayout.Width(20));
                        EditorGUILayout.LabelField(validator.ValidatorName, GUILayout.Width(200));
                        EditorGUILayout.LabelField(validator.Description, EditorStyles.miniLabel);
                        
                        if (validator.CanAutoFix)
                        {
                            EditorGUILayout.LabelField("✓ Auto-fix", EditorStyles.miniLabel, GUILayout.Width(70));
                        }
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }
            EditorGUILayout.EndScrollView();
        }
        EditorGUILayout.EndVertical();
    }
    
    private void DrawResultsPanel()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        {
            // Header
            EditorGUILayout.BeginHorizontal();
            {
                EditorGUILayout.LabelField($"Validation Results ({validationResults.Count})", EditorStyles.boldLabel);
                
                GUILayout.FlexibleSpace();
                
                showPassedTests = GUILayout.Toggle(showPassedTests, "Show Passed", GUILayout.Width(100));
                
                filterBySeverity = GUILayout.Toggle(filterBySeverity, "Filter:", GUILayout.Width(50));
                if (filterBySeverity)
                {
                    severityFilter = (ValidationResult.Severity)EditorGUILayout.EnumPopup(severityFilter, GUILayout.Width(80));
                }
            }
            EditorGUILayout.EndHorizontal();
            
            // Search
            searchFilter = EditorGUILayout.TextField("Search", searchFilter);
            
            EditorGUILayout.Space();
            
            // Results list
            resultsScrollPosition = EditorGUILayout.BeginScrollView(resultsScrollPosition, GUILayout.ExpandHeight(true));
            {
                var filteredResults = GetFilteredResults();
                
                foreach (var result in filteredResults)
                {
                    DrawValidationResult(result);
                }
                
                if (filteredResults.Count == 0)
                {
                    EditorGUILayout.LabelField("No results to display", EditorStyles.centeredGreyMiniLabel);
                }
            }
            EditorGUILayout.EndScrollView();
        }
        EditorGUILayout.EndVertical();
    }
    
    private void DrawValidationResult(ValidationResult result)
    {
        GUIStyle style = result.passed ? passStyle : 
                        (result.severity == ValidationResult.Severity.Warning ? warningStyle : errorStyle);
        
        EditorGUILayout.BeginVertical(style);
        {
            EditorGUILayout.BeginHorizontal();
            {
                // Icon
                string icon = result.passed ? "✓" : 
                             (result.severity == ValidationResult.Severity.Warning ? "⚠" : "✗");
                EditorGUILayout.LabelField(icon, GUILayout.Width(20));
                
                // Validator name
                EditorGUILayout.LabelField(result.validatorName, EditorStyles.boldLabel, GUILayout.Width(150));
                
                // Message
                EditorGUILayout.LabelField(result.message, EditorStyles.wordWrappedLabel);
                
                // Auto-fix button
                if (!result.passed && result.canAutoFix)
                {
                    if (GUILayout.Button("Fix", GUILayout.Width(50)))
                    {
                        var validator = validationManager.GetValidators().Find(v => v.ValidatorName == result.validatorName);
                        if (validator != null)
                        {
                            validator.AutoFix(result.asset, result.assetPath);
                            ValidateSelected(); // Re-validate
                        }
                    }
                }
                
                // Ping button
                if (GUILayout.Button("→", GUILayout.Width(30)))
                {
                    EditorGUIUtility.PingObject(result.asset);
                    Selection.activeObject = result.asset;
                }
            }
            EditorGUILayout.EndHorizontal();
            
            // Asset path
            EditorGUILayout.LabelField(result.assetPath, EditorStyles.miniLabel);
        }
        EditorGUILayout.EndVertical();
        
        EditorGUILayout.Space(2);
    }
    
    private void DrawStatsPanel()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Height(60));
        {
            EditorGUILayout.LabelField("Statistics", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            {
                EditorGUILayout.LabelField($"Total: {totalTests}", GUILayout.Width(100));
                
                GUI.color = Color.green;
                EditorGUILayout.LabelField($"Passed: {passedTests}", GUILayout.Width(100));
                
                GUI.color = Color.yellow;
                EditorGUILayout.LabelField($"Warnings: {warningTests}", GUILayout.Width(120));
                
                GUI.color = Color.red;
                EditorGUILayout.LabelField($"Errors: {errorTests}", GUILayout.Width(100));
                
                GUI.color = Color.white;
                
                GUILayout.FlexibleSpace();
                
                if (totalTests > 0)
                {
                    float passRate = (float)passedTests / totalTests * 100f;
                    EditorGUILayout.LabelField($"Pass Rate: {passRate:F1}%", EditorStyles.boldLabel);
                }
            }
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndVertical();
    }
    
    private List<ValidationResult> GetFilteredResults()
    {
        return validationResults.Where(r =>
        {
            // Filter passed tests
            if (!showPassedTests && r.passed) return false;
            
            // Filter by severity
            if (filterBySeverity && r.severity != severityFilter) return false;
            
            // Search filter
            if (!string.IsNullOrEmpty(searchFilter))
            {
                bool matchesName = r.validatorName.ToLower().Contains(searchFilter.ToLower());
                bool matchesMessage = r.message.ToLower().Contains(searchFilter.ToLower());
                bool matchesPath = r.assetPath.ToLower().Contains(searchFilter.ToLower());
                
                if (!matchesName && !matchesMessage && !matchesPath) return false;
            }
            
            return true;
        }).ToList();
    }
    
    #region Actions
    
    private void ValidateSelected()
    {
        UnityEngine.Object[] selected = Selection.objects;
        
        if (selected.Length == 0)
        {
            EditorUtility.DisplayDialog("No Selection", "Please select assets to validate", "OK");
            return;
        }
        
        validationResults.Clear();
        
        EditorUtility.DisplayProgressBar("Validating", "Assets", 0);
        
        for (int i = 0; i < selected.Length; i++)
        {
            string path = AssetDatabase.GetAssetPath(selected[i]);
            var results = validationManager.ValidateAsset(selected[i], path);
            validationResults.AddRange(results);
            
            float progress = (float)(i + 1) / selected.Length;
            EditorUtility.DisplayProgressBar("Validating", path, progress);
        }
        
        EditorUtility.ClearProgressBar();
        
        UpdateStats();
        
        Debug.Log($"Validation complete: {selected.Length} assets checked");
    }
    
    private void ValidateAll()
    {
        if (!EditorUtility.DisplayDialog("Validate All", 
            "This will validate all assets in the project. Continue?", "Yes", "No"))
        {
            return;
        }
        
        validationResults.Clear();
        
        string[] assetGUIDs = AssetDatabase.FindAssets("");
        
        EditorUtility.DisplayProgressBar("Validating", "All Assets", 0);
        
        for (int i = 0; i < assetGUIDs.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(assetGUIDs[i]);
            UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
            
            if (asset != null)
            {
                var results = validationManager.ValidateAsset(asset, path);
                validationResults.AddRange(results);
            }
            
            float progress = (float)(i + 1) / assetGUIDs.Length;
            if (i % 10 == 0) // Update progress every 10 assets
            {
                EditorUtility.DisplayProgressBar("Validating", path, progress);
            }
        }
        
        EditorUtility.ClearProgressBar();
        
        UpdateStats();
        
        Debug.Log($"Validation complete: {assetGUIDs.Length} assets checked");
    }
    
    private void FixIssues()
    {
        var fixableResults = validationResults.Where(r => !r.passed && r.canAutoFix).ToList();
        
        if (fixableResults.Count == 0)
        {
            EditorUtility.DisplayDialog("No Issues", "No auto-fixable issues found", "OK");
            return;
        }
        
        int fixedCount = validationManager.AutoFixIssues(fixableResults);
        
        EditorUtility.DisplayDialog("Auto-Fix Complete", $"Fixed {fixedCount} issues", "OK");
        
        // Re-validate
        ValidateSelected();
    }
    
    private void ClearResults()
    {
        validationResults.Clear();
        UpdateStats();
    }
    
    private void UpdateStats()
    {
        totalTests = validationResults.Count;
        passedTests = validationResults.Count(r => r.passed);
        failedTests = validationResults.Count(r => !r.passed);
        warningTests = validationResults.Count(r => r.severity == ValidationResult.Severity.Warning);
        errorTests = validationResults.Count(r => r.severity == ValidationResult.Severity.Error);
    }
    
    #endregion
}

#endregion
