using UnityEngine;

[ExecuteInEditMode]
public class GenerationAlgorithm : MonoBehaviour
{
    public Material Display;

    public Vector2Int TextureSize;
    public int mapSeed;

    public float mapScale;
    public float mapLacunarity;
    public float mapGain;
    [Range(1,20)]
    public int mapOctaves;

    [Header("3D Terrain Generation")]
    public bool generate3DTerrain = false;
    public float terrainScale = 1f;
    public float heightMultiplier = 10f;
    public Material terrainMaterial;

    // Store previous values to detect changes
    private Vector2Int previousTextureSize;
    private float previousMapScale;
    private int previousMapSeed;
    private int previousMapOctaves;
    private float previousMapLacunarity;
    private float previousMapGain;
    private bool previousGenerate3DTerrain;
    private float previousTerrainScale;
    private float previousHeightMultiplier;

    void Start()
    {
        // Initialize previous values and generate initial texture
        previousTextureSize = TextureSize;
        previousMapScale = mapScale;
        previousMapSeed = mapSeed;
        previousMapOctaves = mapOctaves;
        previousMapLacunarity = mapLacunarity;
        previousMapGain = mapGain;
        
        if (Display != null && TextureSize.x > 0 && TextureSize.y > 0)
        {
            DisplayTexture(GenerateMap(TextureSize.x, TextureSize.y, mapScale, mapSeed, mapLacunarity, mapGain, mapOctaves));
        }
    }

    void Update()
    {
        // Check if any values have changed
        if (HasValuesChanged())
        {
            // Update previous values
            previousTextureSize = TextureSize;
            previousMapScale = mapScale;
            previousMapSeed = mapSeed;
            previousMapOctaves = mapOctaves;
            previousMapLacunarity = mapLacunarity;
            previousMapGain = mapGain;
            
            // Only generate if we have valid values and a material
            if (Display != null && TextureSize.x > 0 && TextureSize.y > 0)
            {
                DisplayTexture(GenerateMap(TextureSize.x, TextureSize.y, mapScale, mapSeed, mapLacunarity, mapGain, mapOctaves));
            }
        }
    }

    bool HasValuesChanged()
    {
        return previousTextureSize != TextureSize || 
               previousMapScale != mapScale || 
               previousMapSeed != mapSeed ||
               previousMapOctaves != mapOctaves ||
               previousMapLacunarity != mapLacunarity ||
               previousMapGain != mapGain;
    }


    //Main Function
    Color[,] GenerateMap(int width, int height, float scale, int seed, float lacunarity, float gain, int octaves)
    {
        Color[,] ColorMap = new Color[width, height];

        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
                float h = 0;
                float amplitude = 1;
                float frequency = 1;
                float maxValue = 0; // Used for normalizing result to 0.0 - 1.0
                for (int octave = 0; octave < octaves; octave++)
                {
                    float xCoord = ((float)x / width * scale * frequency) + seed;
                    float yCoord = ((float)y / height * scale * frequency) + seed;

                    float noiseValue = Mathf.PerlinNoise(xCoord, yCoord);
                    h += noiseValue * amplitude;

                    maxValue += amplitude;
                    amplitude *= gain;
                    frequency *= lacunarity;
                }
                
                // Normalize the height value
                h /= maxValue;
                ColorMap[x, y] = new Color(h, h, h, 1);
            }

        return ColorMap;
    }

    void DisplayTexture(Color[,] texture)
    {
        // Get dimensions of the 2D color array
        int width = texture.GetLength(0);
        int height = texture.GetLength(1);

        // Create a new Texture2D with the same dimensions
        Texture2D newTexture = new Texture2D(width, height);

        // Convert 2D array to 1D array for SetPixels
        Color[] pixels = new Color[width * height];
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
            {
            // Fix pixel mapping - Unity's texture coordinates start from bottom-left
           pixels[(height - 1 - y) * width + x] = texture[x, y];
            }

        // Set the pixels and apply changes
        newTexture.SetPixels(pixels);
        newTexture.filterMode = FilterMode.Point;
        newTexture.Apply();

        // Apply the texture to the material's main texture
        Display.mainTexture = newTexture;
    }
}
