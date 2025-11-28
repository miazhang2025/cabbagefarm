using UnityEngine;

[CreateAssetMenu(fileName = "WallProfile", menuName = "Procedural Wall/Wall Profile")]
public class WallProfile : ScriptableObject
{
    [System.Serializable]
    public class ProfilePoint
    {
        public Vector2 position; // X = horizontal offset, Y = height
        public Vector2 uv;
    }

    public ProfilePoint[] points = new ProfilePoint[]
    {
        new ProfilePoint { position = new Vector2(-0.5f, 0f), uv = new Vector2(0, 0) },
        new ProfilePoint { position = new Vector2(0.5f, 0f), uv = new Vector2(1, 0) },
        new ProfilePoint { position = new Vector2(0.5f, 2f), uv = new Vector2(1, 1) },
        new ProfilePoint { position = new Vector2(-0.5f, 2f), uv = new Vector2(0, 1) }
    };

    public float width = 1f;
    public float height = 2f;
}