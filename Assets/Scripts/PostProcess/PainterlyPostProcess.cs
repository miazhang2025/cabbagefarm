using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[System.Serializable, VolumeComponentMenu("Custom/Painterly Post Process")]
public class PainterlyPostProcess : VolumeComponent, IPostProcessComponent
{
    [Header("Shading Settings")]
    public ClampedFloatParameter painterlySmoothness = new ClampedFloatParameter(0.1f, 0f, 1f);
    public TextureParameter shadingGradient = new TextureParameter(null);
    public TextureParameter painterlyGuide = new TextureParameter(null);
    
    [Header("Lighting")]
    public ColorParameter specularColor = new ColorParameter(Color.white, true, false, true);
    public ClampedFloatParameter smoothness = new ClampedFloatParameter(0.5f, 0f, 1f);
    public ClampedFloatParameter normalBias = new ClampedFloatParameter(0.2f, 0f, 1f);
    
    [Header("Brush Effects")]
    public TextureParameter brushTexture = new TextureParameter(null);
    public ClampedFloatParameter brushScale = new ClampedFloatParameter(1f, 0.1f, 10f);
    public ClampedFloatParameter brushStrength = new ClampedFloatParameter(0.5f, 0f, 1f);
    
    [Header("Edge Erosion")]
    public ClampedFloatParameter edgeThreshold = new ClampedFloatParameter(0.3f, 0f, 1f);
    public ClampedFloatParameter edgeWidth = new ClampedFloatParameter(2f, 0.5f, 10f);
    public ClampedFloatParameter erosionStrength = new ClampedFloatParameter(0.5f, 0f, 1f);
    
    [Header("Effect Intensity")]
    public ClampedFloatParameter intensity = new ClampedFloatParameter(1f, 0f, 1f);

    public bool IsActive() => intensity.value > 0f && shadingGradient.value != null;
    public bool IsTileCompatible() => false;
}