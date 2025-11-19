using UnityEngine;

public class SetShadingGradient : MonoBehaviour
{
    [SerializeField] private Texture2D shadingGradientTexture;
    
    private Material material;
    
    void Start()
    {
        // Get the material from the renderer
        Renderer renderer = GetComponent<Renderer>();
        if (renderer != null)
        {
            material = renderer.material; // Use .sharedMaterial if you want to affect all objects using this material
            
            // Assign the texture to the shader property
            if (shadingGradientTexture != null)
            {
                material.SetTexture("_ShadingGradient", shadingGradientTexture);
            }
        }
    }
    
    // Method to set the texture at runtime
    public void SetGradientTexture(Texture2D texture)
    {
        if (material != null && texture != null)
        {
            material.SetTexture("_ShadingGradient", texture);
        }
    }
    
    // Method to load texture from Resources folder
    public void LoadGradientFromResources(string resourcePath)
    {
        Texture2D texture = Resources.Load<Texture2D>(resourcePath);
        if (texture != null)
        {
            SetGradientTexture(texture);
        }
        else
        {
            Debug.LogWarning($"Could not load texture from Resources: {resourcePath}");
        }
    }
    
    // Method to load texture from file path
    public void LoadGradientFromFile(string filePath)
    {
        if (System.IO.File.Exists(filePath))
        {
            byte[] fileData = System.IO.File.ReadAllBytes(filePath);
            Texture2D texture = new Texture2D(2, 2);
            if (texture.LoadImage(fileData))
            {
                SetGradientTexture(texture);
            }
        }
    }
}

