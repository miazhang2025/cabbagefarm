using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;

/// <summary>
/// Prefab Variant Generator - Foundation Project 4
/// Data-driven system for creating prefab variants with material/mesh combinations
/// Usage: Window > Prefab Variant Generator
/// </summary>

#region Configuration Classes

[CreateAssetMenu(fileName = "VariantConfig", menuName = "Pipeline/Prefab Variant Config")]
public class PrefabVariantConfig : ScriptableObject
{
    [System.Serializable]
    public class VariantSet
    {
        public string setName = "New Variant Set";
        public GameObject basePrefab;
        public List<MaterialVariant> materialVariants = new List<MaterialVariant>();
        public List<MeshVariant> meshVariants = new List<MeshVariant>();
        public string outputPath = "Assets/Generated/Variants";
        public bool generateAllCombinations = false;
        public string namingPattern = "{BASE}_{MATERIAL}_{MESH}";
    }
    
    [System.Serializable]
    public class MaterialVariant
    {
        public string variantName;
        public List<MaterialReplacement> replacements = new List<MaterialReplacement>();
    }
    
    [System.Serializable]
    public class MaterialReplacement
    {
        public string targetRendererName;
        public Material originalMaterial;
        public Material replacementMaterial;
    }
    
    [System.Serializable]
    public class MeshVariant
    {
        public string variantName;
        public List<MeshReplacement> replacements = new List<MeshReplacement>();
    }
    
    [System.Serializable]
    public class MeshReplacement
    {
        public string targetMeshFilterName;
        public Mesh replacementMesh;
    }
    
    [Header("Variant Sets")]
    public List<VariantSet> variantSets = new List<VariantSet>();
    
    [Header("Generation Settings")]
    public bool createFolderStructure = true;
    public bool overwriteExisting = false;
    //public bool autoAddToVersion Control = false;
    
    [Header("Metadata")]
    public bool embedMetadata = true;
    public bool trackDependencies = true;
}

[System.Serializable]
public class PrefabMetadata : ScriptableObject
{
    public GameObject sourcePrefab;
    public string variantSetName;
    public string materialVariantName;
    public string meshVariantName;
    public System.DateTime creationDate;
    public List<string> dependencies = new List<string>();
    public string generatorVersion = "1.0";
}

#endregion

#region Variant Generator Core

public class PrefabVariantGenerator
{
    private PrefabVariantConfig config;
    
    public PrefabVariantGenerator(PrefabVariantConfig config)
    {
        this.config = config;
    }
    
    public List<GameObject> GenerateVariants(PrefabVariantConfig.VariantSet variantSet)
    {
        List<GameObject> generatedPrefabs = new List<GameObject>();
        
        if (variantSet.basePrefab == null)
        {
            Debug.LogError("Base prefab is null!");
            return generatedPrefabs;
        }
        
        // Create output directory
        if (config.createFolderStructure)
        {
            CreateFolderStructure(variantSet.outputPath);
        }
        
        if (variantSet.generateAllCombinations)
        {
            // Generate all combinations
            generatedPrefabs = GenerateAllCombinations(variantSet);
        }
        else
        {
            // Generate individual variants
            generatedPrefabs = GenerateIndividualVariants(variantSet);
        }
        
        return generatedPrefabs;
    }
    
    private List<GameObject> GenerateAllCombinations(PrefabVariantConfig.VariantSet variantSet)
    {
        List<GameObject> generatedPrefabs = new List<GameObject>();
        
        // If no variants defined, create simple copy
        if (variantSet.materialVariants.Count == 0 && variantSet.meshVariants.Count == 0)
        {
            GameObject variant = CreateVariant(variantSet, null, null);
            if (variant != null)
                generatedPrefabs.Add(variant);
            return generatedPrefabs;
        }
        
        // Generate combinations
        if (variantSet.materialVariants.Count == 0)
        {
            // Only mesh variants
            foreach (var meshVar in variantSet.meshVariants)
            {
                GameObject variant = CreateVariant(variantSet, null, meshVar);
                if (variant != null)
                    generatedPrefabs.Add(variant);
            }
        }
        else if (variantSet.meshVariants.Count == 0)
        {
            // Only material variants
            foreach (var matVar in variantSet.materialVariants)
            {
                GameObject variant = CreateVariant(variantSet, matVar, null);
                if (variant != null)
                    generatedPrefabs.Add(variant);
            }
        }
        else
        {
            // Both material and mesh variants - create all combinations
            foreach (var matVar in variantSet.materialVariants)
            {
                foreach (var meshVar in variantSet.meshVariants)
                {
                    GameObject variant = CreateVariant(variantSet, matVar, meshVar);
                    if (variant != null)
                        generatedPrefabs.Add(variant);
                }
            }
        }
        
        return generatedPrefabs;
    }
    
    private List<GameObject> GenerateIndividualVariants(PrefabVariantConfig.VariantSet variantSet)
    {
        List<GameObject> generatedPrefabs = new List<GameObject>();
        
        // Generate material variants
        foreach (var matVar in variantSet.materialVariants)
        {
            GameObject variant = CreateVariant(variantSet, matVar, null);
            if (variant != null)
                generatedPrefabs.Add(variant);
        }
        
        // Generate mesh variants
        foreach (var meshVar in variantSet.meshVariants)
        {
            GameObject variant = CreateVariant(variantSet, null, meshVar);
            if (variant != null)
                generatedPrefabs.Add(variant);
        }
        
        return generatedPrefabs;
    }
    
    private GameObject CreateVariant(
        PrefabVariantConfig.VariantSet variantSet,
        PrefabVariantConfig.MaterialVariant materialVariant,
        PrefabVariantConfig.MeshVariant meshVariant)
    {
        // Instantiate base prefab
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(variantSet.basePrefab);
        
        // Apply material variant
        if (materialVariant != null)
        {
            ApplyMaterialVariant(instance, materialVariant);
        }
        
        // Apply mesh variant
        if (meshVariant != null)
        {
            ApplyMeshVariant(instance, meshVariant);
        }
        
        // Generate name
        string variantName = GenerateVariantName(
            variantSet,
            materialVariant?.variantName,
            meshVariant?.variantName);
        
        // Create prefab path
        string prefabPath = Path.Combine(variantSet.outputPath, variantName + ".prefab");
        
        // Check if exists
        if (File.Exists(prefabPath) && !config.overwriteExisting)
        {
            Debug.LogWarning($"Prefab already exists: {prefabPath}. Skipping.");
            Object.DestroyImmediate(instance);
            return null;
        }
        
        // Save as prefab variant
        GameObject prefabVariant = PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
        
        // Clean up instance
        Object.DestroyImmediate(instance);
        
        // Create metadata
        if (config.embedMetadata)
        {
            CreateMetadata(prefabVariant, variantSet, materialVariant, meshVariant);
        }
        
        // Track dependencies
        if (config.trackDependencies)
        {
            TrackDependencies(prefabVariant, variantSet, materialVariant, meshVariant);
        }
        
        return prefabVariant;
    }
    
    private void ApplyMaterialVariant(GameObject instance, PrefabVariantConfig.MaterialVariant materialVariant)
    {
        foreach (var replacement in materialVariant.replacements)
        {
            Transform target = instance.transform.Find(replacement.targetRendererName);
            
            if (target == null)
            {
                // Try searching in children
                target = FindInChildren(instance.transform, replacement.targetRendererName);
            }
            
            if (target != null)
            {
                Renderer renderer = target.GetComponent<Renderer>();
                if (renderer != null && replacement.replacementMaterial != null)
                {
                    Material[] materials = renderer.sharedMaterials;
                    
                    // Replace matching materials
                    for (int i = 0; i < materials.Length; i++)
                    {
                        if (materials[i] == replacement.originalMaterial)
                        {
                            materials[i] = replacement.replacementMaterial;
                        }
                    }
                    
                    renderer.sharedMaterials = materials;
                }
            }
            else
            {
                Debug.LogWarning($"Renderer not found: {replacement.targetRendererName}");
            }
        }
    }
    
    private void ApplyMeshVariant(GameObject instance, PrefabVariantConfig.MeshVariant meshVariant)
    {
        foreach (var replacement in meshVariant.replacements)
        {
            Transform target = instance.transform.Find(replacement.targetMeshFilterName);
            
            if (target == null)
            {
                target = FindInChildren(instance.transform, replacement.targetMeshFilterName);
            }
            
            if (target != null)
            {
                MeshFilter meshFilter = target.GetComponent<MeshFilter>();
                if (meshFilter != null && replacement.replacementMesh != null)
                {
                    meshFilter.sharedMesh = replacement.replacementMesh;
                }
            }
            else
            {
                Debug.LogWarning($"MeshFilter not found: {replacement.targetMeshFilterName}");
            }
        }
    }
    
    private Transform FindInChildren(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name)
                return child;
            
            Transform found = FindInChildren(child, name);
            if (found != null)
                return found;
        }
        return null;
    }
    
    private string GenerateVariantName(
        PrefabVariantConfig.VariantSet variantSet,
        string materialVariantName,
        string meshVariantName)
    {
        string baseName = variantSet.basePrefab.name;
        string pattern = variantSet.namingPattern;
        
        pattern = pattern.Replace("{BASE}", baseName);
        pattern = pattern.Replace("{MATERIAL}", materialVariantName ?? "Default");
        pattern = pattern.Replace("{MESH}", meshVariantName ?? "Default");
        pattern = pattern.Replace("{SET}", variantSet.setName);
        
        return pattern;
    }
    
    private void CreateMetadata(
        GameObject prefab,
        PrefabVariantConfig.VariantSet variantSet,
        PrefabVariantConfig.MaterialVariant materialVariant,
        PrefabVariantConfig.MeshVariant meshVariant)
    {
        string prefabPath = AssetDatabase.GetAssetPath(prefab);
        string metadataPath = Path.ChangeExtension(prefabPath, ".meta.asset");
        
        PrefabMetadata metadata = ScriptableObject.CreateInstance<PrefabMetadata>();
        metadata.sourcePrefab = variantSet.basePrefab;
        metadata.variantSetName = variantSet.setName;
        metadata.materialVariantName = materialVariant?.variantName;
        metadata.meshVariantName = meshVariant?.variantName;
        metadata.creationDate = System.DateTime.Now;
        
        AssetDatabase.CreateAsset(metadata, metadataPath);
    }
    
    private void TrackDependencies(
        GameObject prefab,
        PrefabVariantConfig.VariantSet variantSet,
        PrefabVariantConfig.MaterialVariant materialVariant,
        PrefabVariantConfig.MeshVariant meshVariant)
    {
        List<string> dependencies = new List<string>();
        
        // Add base prefab
        dependencies.Add(AssetDatabase.GetAssetPath(variantSet.basePrefab));
        
        // Add material dependencies
        if (materialVariant != null)
        {
            foreach (var replacement in materialVariant.replacements)
            {
                if (replacement.replacementMaterial != null)
                {
                    dependencies.Add(AssetDatabase.GetAssetPath(replacement.replacementMaterial));
                }
            }
        }
        
        // Add mesh dependencies
        if (meshVariant != null)
        {
            foreach (var replacement in meshVariant.replacements)
            {
                if (replacement.replacementMesh != null)
                {
                    dependencies.Add(AssetDatabase.GetAssetPath(replacement.replacementMesh));
                }
            }
        }
        
        // Store in metadata
        string prefabPath = AssetDatabase.GetAssetPath(prefab);
        string metadataPath = Path.ChangeExtension(prefabPath, ".meta.asset");
        
        PrefabMetadata metadata = AssetDatabase.LoadAssetAtPath<PrefabMetadata>(metadataPath);
        if (metadata != null)
        {
            metadata.dependencies = dependencies;
            EditorUtility.SetDirty(metadata);
        }
    }
    
    private void CreateFolderStructure(string path)
    {
        if (!AssetDatabase.IsValidFolder(path))
        {
            string[] folders = path.Split('/');
            string currentPath = folders[0];
            
            for (int i = 1; i < folders.Length; i++)
            {
                string newPath = currentPath + "/" + folders[i];
                
                if (!AssetDatabase.IsValidFolder(newPath))
                {
                    AssetDatabase.CreateFolder(currentPath, folders[i]);
                }
                
                currentPath = newPath;
            }
        }
    }
}

#endregion

#region Editor Window

public class PrefabVariantGeneratorWindow : EditorWindow
{
    private PrefabVariantConfig config;
    private Vector2 scrollPosition;
    private int selectedSetIndex = 0;
    
    private bool showMaterialVariants = true;
    private bool showMeshVariants = true;
    private bool showGenerationSettings = true;
    
    [MenuItem("Window/Prefab Variant Generator")]
    public static void ShowWindow()
    {
        PrefabVariantGeneratorWindow window = GetWindow<PrefabVariantGeneratorWindow>("Prefab Variant Generator");
        window.minSize = new Vector2(600, 500);
        window.Show();
    }
    
    private void OnEnable()
    {
        LoadConfig();
    }
    
    private void LoadConfig()
    {
        string[] guids = AssetDatabase.FindAssets("t:PrefabVariantConfig");
        
        if (guids.Length > 0)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            config = AssetDatabase.LoadAssetAtPath<PrefabVariantConfig>(path);
        }
        else
        {
            CreateDefaultConfig();
        }
    }
    
    private void CreateDefaultConfig()
    {
        config = CreateInstance<PrefabVariantConfig>();
        
        string path = "Assets/PrefabVariantConfig.asset";
        AssetDatabase.CreateAsset(config, path);
        AssetDatabase.SaveAssets();
        
        Selection.activeObject = config;
        Debug.Log($"Created Prefab Variant Config at: {path}");
    }
    
    private void OnGUI()
    {
        if (config == null)
        {
            EditorGUILayout.HelpBox("No config found. Creating default...", MessageType.Info);
            if (GUILayout.Button("Create Config"))
            {
                CreateDefaultConfig();
            }
            return;
        }
        
        DrawToolbar();
        
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        {
            DrawConfigHeader();
            DrawVariantSetSelector();
            
            if (config.variantSets.Count > 0 && selectedSetIndex < config.variantSets.Count)
            {
                var currentSet = config.variantSets[selectedSetIndex];
                DrawVariantSetEditor(currentSet);
            }
            
            DrawGenerationPanel();
        }
        EditorGUILayout.EndScrollView();
    }
    
    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
        {
            if (GUILayout.Button("New Set", EditorStyles.toolbarButton, GUILayout.Width(70)))
            {
                CreateNewVariantSet();
            }
            
            if (GUILayout.Button("Duplicate", EditorStyles.toolbarButton, GUILayout.Width(70)))
            {
                DuplicateCurrentSet();
            }
            
            EditorGUI.BeginDisabledGroup(config.variantSets.Count == 0);
            if (GUILayout.Button("Delete Set", EditorStyles.toolbarButton, GUILayout.Width(80)))
            {
                DeleteCurrentSet();
            }
            EditorGUI.EndDisabledGroup();
            
            GUILayout.FlexibleSpace();
            
            if (GUILayout.Button("Save", EditorStyles.toolbarButton, GUILayout.Width(50)))
            {
                SaveConfig();
            }
        }
        EditorGUILayout.EndHorizontal();
    }
    
    private void DrawConfigHeader()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        {
            EditorGUILayout.LabelField("Prefab Variant Generator", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            {
                EditorGUILayout.LabelField("Config:", GUILayout.Width(50));
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.ObjectField(config, typeof(PrefabVariantConfig), false);
                EditorGUI.EndDisabledGroup();
            }
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndVertical();
    }
    
    private void DrawVariantSetSelector()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        {
            EditorGUILayout.LabelField("Variant Sets", EditorStyles.boldLabel);
            
            if (config.variantSets.Count == 0)
            {
                EditorGUILayout.HelpBox("No variant sets defined. Click 'New Set' to create one.", MessageType.Info);
                return;
            }
            
            string[] setNames = config.variantSets.Select(s => s.setName).ToArray();
            selectedSetIndex = GUILayout.SelectionGrid(selectedSetIndex, setNames, 3);
            selectedSetIndex = Mathf.Clamp(selectedSetIndex, 0, config.variantSets.Count - 1);
        }
        EditorGUILayout.EndVertical();
    }
    
    private void DrawVariantSetEditor(PrefabVariantConfig.VariantSet variantSet)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        {
            // Basic settings
            variantSet.setName = EditorGUILayout.TextField("Set Name", variantSet.setName);
            variantSet.basePrefab = (GameObject)EditorGUILayout.ObjectField(
                "Base Prefab", variantSet.basePrefab, typeof(GameObject), false);
            
            variantSet.outputPath = EditorGUILayout.TextField("Output Path", variantSet.outputPath);
            variantSet.namingPattern = EditorGUILayout.TextField("Naming Pattern", variantSet.namingPattern);
            
            EditorGUILayout.HelpBox(
                "Naming tokens: {BASE}, {MATERIAL}, {MESH}, {SET}", 
                MessageType.Info);
            
            variantSet.generateAllCombinations = EditorGUILayout.Toggle(
                "Generate All Combinations", variantSet.generateAllCombinations);
            
            EditorGUILayout.Space();
            
            // Material variants
            DrawMaterialVariants(variantSet);
            
            EditorGUILayout.Space();
            
            // Mesh variants
            DrawMeshVariants(variantSet);
        }
        EditorGUILayout.EndVertical();
    }
    
    private void DrawMaterialVariants(PrefabVariantConfig.VariantSet variantSet)
    {
        showMaterialVariants = EditorGUILayout.Foldout(
            showMaterialVariants, 
            $"Material Variants ({variantSet.materialVariants.Count})", 
            true);
        
        if (!showMaterialVariants) return;
        
        EditorGUI.indentLevel++;
        
        for (int i = 0; i < variantSet.materialVariants.Count; i++)
        {
            DrawMaterialVariant(variantSet.materialVariants[i], i, variantSet);
        }
        
        if (GUILayout.Button("+ Add Material Variant"))
        {
            variantSet.materialVariants.Add(new PrefabVariantConfig.MaterialVariant
            {
                variantName = $"MaterialVariant{variantSet.materialVariants.Count + 1}"
            });
        }
        
        EditorGUI.indentLevel--;
    }
    
    private void DrawMaterialVariant(
        PrefabVariantConfig.MaterialVariant variant, 
        int index, 
        PrefabVariantConfig.VariantSet variantSet)
    {
        EditorGUILayout.BeginVertical(GUI.skin.box);
        {
            EditorGUILayout.BeginHorizontal();
            {
                variant.variantName = EditorGUILayout.TextField("Variant Name", variant.variantName);
                
                if (GUILayout.Button("×", GUILayout.Width(25)))
                {
                    variantSet.materialVariants.RemoveAt(index);
                    return;
                }
            }
            EditorGUILayout.EndHorizontal();
            
            // Replacements
            EditorGUILayout.LabelField("Material Replacements:", EditorStyles.miniLabel);
            
            for (int i = 0; i < variant.replacements.Count; i++)
            {
                DrawMaterialReplacement(variant.replacements[i], i, variant);
            }
            
            if (GUILayout.Button("+ Add Replacement"))
            {
                variant.replacements.Add(new PrefabVariantConfig.MaterialReplacement());
            }
        }
        EditorGUILayout.EndVertical();
    }
    
    private void DrawMaterialReplacement(
        PrefabVariantConfig.MaterialReplacement replacement, 
        int index,
        PrefabVariantConfig.MaterialVariant variant)
    {
        EditorGUILayout.BeginHorizontal();
        {
            replacement.targetRendererName = EditorGUILayout.TextField(
                "Renderer", replacement.targetRendererName, GUILayout.Width(150));
            
            replacement.originalMaterial = (Material)EditorGUILayout.ObjectField(
                replacement.originalMaterial, typeof(Material), false, GUILayout.Width(100));
            
            EditorGUILayout.LabelField("→", GUILayout.Width(20));
            
            replacement.replacementMaterial = (Material)EditorGUILayout.ObjectField(
                replacement.replacementMaterial, typeof(Material), false, GUILayout.Width(100));
            
            if (GUILayout.Button("×", GUILayout.Width(25)))
            {
                variant.replacements.RemoveAt(index);
            }
        }
        EditorGUILayout.EndHorizontal();
    }
    
    private void DrawMeshVariants(PrefabVariantConfig.VariantSet variantSet)
    {
        showMeshVariants = EditorGUILayout.Foldout(
            showMeshVariants, 
            $"Mesh Variants ({variantSet.meshVariants.Count})", 
            true);
        
        if (!showMeshVariants) return;
        
        EditorGUI.indentLevel++;
        
        for (int i = 0; i < variantSet.meshVariants.Count; i++)
        {
            DrawMeshVariant(variantSet.meshVariants[i], i, variantSet);
        }
        
        if (GUILayout.Button("+ Add Mesh Variant"))
        {
            variantSet.meshVariants.Add(new PrefabVariantConfig.MeshVariant
            {
                variantName = $"MeshVariant{variantSet.meshVariants.Count + 1}"
            });
        }
        
        EditorGUI.indentLevel--;
    }
    
    private void DrawMeshVariant(
        PrefabVariantConfig.MeshVariant variant, 
        int index,
        PrefabVariantConfig.VariantSet variantSet)
    {
        EditorGUILayout.BeginVertical(GUI.skin.box);
        {
            EditorGUILayout.BeginHorizontal();
            {
                variant.variantName = EditorGUILayout.TextField("Variant Name", variant.variantName);
                
                if (GUILayout.Button("×", GUILayout.Width(25)))
                {
                    variantSet.meshVariants.RemoveAt(index);
                    return;
                }
            }
            EditorGUILayout.EndHorizontal();
            
            // Replacements
            EditorGUILayout.LabelField("Mesh Replacements:", EditorStyles.miniLabel);
            
            for (int i = 0; i < variant.replacements.Count; i++)
            {
                DrawMeshReplacement(variant.replacements[i], i, variant);
            }
            
            if (GUILayout.Button("+ Add Replacement"))
            {
                variant.replacements.Add(new PrefabVariantConfig.MeshReplacement());
            }
        }
        EditorGUILayout.EndVertical();
    }
    
    private void DrawMeshReplacement(
        PrefabVariantConfig.MeshReplacement replacement, 
        int index,
        PrefabVariantConfig.MeshVariant variant)
    {
        EditorGUILayout.BeginHorizontal();
        {
            replacement.targetMeshFilterName = EditorGUILayout.TextField(
                "MeshFilter", replacement.targetMeshFilterName, GUILayout.Width(150));
            
            EditorGUILayout.LabelField("→", GUILayout.Width(20));
            
            replacement.replacementMesh = (Mesh)EditorGUILayout.ObjectField(
                replacement.replacementMesh, typeof(Mesh), false);
            
            if (GUILayout.Button("×", GUILayout.Width(25)))
            {
                variant.replacements.RemoveAt(index);
            }
        }
        EditorGUILayout.EndHorizontal();
    }
    
    private void DrawGenerationPanel()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        {
            showGenerationSettings = EditorGUILayout.Foldout(showGenerationSettings, "Generation Settings", true);
            
            if (showGenerationSettings)
            {
                EditorGUI.indentLevel++;
                
                config.createFolderStructure = EditorGUILayout.Toggle("Create Folders", config.createFolderStructure);
                config.overwriteExisting = EditorGUILayout.Toggle("Overwrite Existing", config.overwriteExisting);
                config.embedMetadata = EditorGUILayout.Toggle("Embed Metadata", config.embedMetadata);
                config.trackDependencies = EditorGUILayout.Toggle("Track Dependencies", config.trackDependencies);
                
                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.Space();
            
            EditorGUI.BeginDisabledGroup(config.variantSets.Count == 0 || 
                selectedSetIndex >= config.variantSets.Count ||
                config.variantSets[selectedSetIndex].basePrefab == null);
            
            if (GUILayout.Button("Generate Current Set", GUILayout.Height(40)))
            {
                GenerateCurrentSet();
            }
            
            if (GUILayout.Button("Generate All Sets"))
            {
                GenerateAllSets();
            }
            
            EditorGUI.EndDisabledGroup();
        }
        EditorGUILayout.EndVertical();
    }
    
    #region Actions
    
    private void CreateNewVariantSet()
    {
        var newSet = new PrefabVariantConfig.VariantSet
        {
            setName = $"VariantSet{config.variantSets.Count + 1}",
            outputPath = "Assets/Generated/Variants"
        };
        
        config.variantSets.Add(newSet);
        selectedSetIndex = config.variantSets.Count - 1;
        SaveConfig();
    }
    
    private void DuplicateCurrentSet()
    {
        if (selectedSetIndex < config.variantSets.Count)
        {
            var original = config.variantSets[selectedSetIndex];
            var duplicate = JsonUtility.FromJson<PrefabVariantConfig.VariantSet>(
                JsonUtility.ToJson(original));
            duplicate.setName += " (Copy)";
            
            config.variantSets.Add(duplicate);
            selectedSetIndex = config.variantSets.Count - 1;
            SaveConfig();
        }
    }
    
    private void DeleteCurrentSet()
    {
        if (EditorUtility.DisplayDialog("Delete Variant Set", 
            "Are you sure you want to delete this variant set?", "Yes", "No"))
        {
            config.variantSets.RemoveAt(selectedSetIndex);
            selectedSetIndex = Mathf.Max(0, selectedSetIndex - 1);
            SaveConfig();
        }
    }
    
    private void GenerateCurrentSet()
    {
        var variantSet = config.variantSets[selectedSetIndex];
        
        PrefabVariantGenerator generator = new PrefabVariantGenerator(config);
        
        EditorUtility.DisplayProgressBar("Generating Variants", "Please wait...", 0);
        
        var generated = generator.GenerateVariants(variantSet);
        
        EditorUtility.ClearProgressBar();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        
        Debug.Log($"Generated {generated.Count} prefab variants");
        EditorUtility.DisplayDialog("Generation Complete", 
            $"Generated {generated.Count} prefab variants", "OK");
    }
    
    private void GenerateAllSets()
    {
        if (!EditorUtility.DisplayDialog("Generate All", 
            "Generate variants for all sets?", "Yes", "No"))
        {
            return;
        }
        
        PrefabVariantGenerator generator = new PrefabVariantGenerator(config);
        int totalGenerated = 0;
        
        for (int i = 0; i < config.variantSets.Count; i++)
        {
            var variantSet = config.variantSets[i];
            
            EditorUtility.DisplayProgressBar("Generating Variants", 
                $"Set {i + 1}/{config.variantSets.Count}", 
                (float)i / config.variantSets.Count);
            
            var generated = generator.GenerateVariants(variantSet);
            totalGenerated += generated.Count;
        }
        
        EditorUtility.ClearProgressBar();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        
        Debug.Log($"Generated {totalGenerated} total prefab variants");
        EditorUtility.DisplayDialog("Generation Complete", 
            $"Generated {totalGenerated} total prefab variants", "OK");
    }
    
    private void SaveConfig()
    {
        EditorUtility.SetDirty(config);
        AssetDatabase.SaveAssets();
        Debug.Log("Config saved");
    }
    
    #endregion
}

#endregion
