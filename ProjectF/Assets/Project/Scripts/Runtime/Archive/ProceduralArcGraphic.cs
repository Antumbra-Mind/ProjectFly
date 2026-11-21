using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class ProceduralArcGraphic : MonoBehaviour
{
    [SerializeField] private int textureSize = 256;
    [SerializeField, Range(0.02f, 0.3f)] private float thickness = 0.08f;

    private void Awake()
    {
        Image img = GetComponent<Image>();
        img.sprite = CreateRingSprite(textureSize, thickness);
        img.type = Image.Type.Filled;
        img.fillMethod = Image.FillMethod.Radial360;
        img.fillOrigin = (int)Image.Origin360.Bottom;
        img.fillClockwise = true;
    }

    private Sprite CreateRingSprite(int size, float ringThickness)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;

        float half = size * 0.5f;
        float outerRadius = half - 2f;
        float innerRadius = outerRadius * (1f - ringThickness);

        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(half, half));
                // М'яке згладжування країв (anti-aliasing 1.5px)
                float outerAlpha = Mathf.Clamp01((outerRadius - dist) / 1.5f);
                float innerAlpha = Mathf.Clamp01((dist - innerRadius) / 1.5f);
                float alpha = outerAlpha * innerAlpha;

                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();

        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }
}