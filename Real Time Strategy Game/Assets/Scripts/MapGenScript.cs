using UnityEngine;
using System.Collections.Generic;
using UnityEngine.InputSystem;

public enum ResourceType { Oil, Metal }

[System.Serializable]
public class ResourceSettings
{
    public ResourceType type;

    [Range(0.001f, 1f)] public float noiseScale = 0.08f;  // spatial frequency
    public float maxAmount = 100f;

    // Constraints
    public int minTerrainType = 0;
    public int maxTerrainType = 999;
    public bool requireAboveWater = true;
    public bool requireBelowWater = false;

    public Color resourceColor = Color.white;
}

public class HexTile
{
    public Vector3 position;
    public Color color;
    public int terrainType;
    public float[] resources;

    public float this[ResourceType type]
    {
        get => resources[(int)type];
        set => resources[(int)type] = value;
    }

    public HexTile(Vector3 pos, int terrain, Color? color = null)
    {
        position = pos;
        terrainType = terrain;
        this.color = color ?? Color.white;
        resources = new float[System.Enum.GetValues(typeof(ResourceType)).Length];
    }
}

public class MapGenScript : MonoBehaviour
{
    public const float HEX_VERTICAL_OFFSET_MULTIPLIER = 0.8660254f;

    [Header("Dependencies")]
    public Transform waterPrefab;
    public Transform hexTilePrefab;

    [Header("Map Generation")]
    public int mapWidth = 10;
    public int mapHeight = 10;
    public float mapScale = 0.1f;
    public float mapAmplitude = 1f;
    public int mapExponent = 1;
    public float yOffset = 0f;
    public int mapOctaves = 5;
    public float mapLacunarity = 2;
    public float mapGain = 0.2f;
    public bool useFalloff = false;
    public float mapFalloffExponent = 2f;
    public float mapFalloffStartDistance = 0.9f;
    public float waterLevel = 0f;
    public float waterBuffer = 10f;

    public bool stepHeight;
    public float stepHeightResolution = 1;

    [Header("Seed")]
    public bool randomSeed;
    [Tooltip("Random seed for map generation. If set to 0, a random seed will be used.")]
    public int seed = 0;

    [Header("Map Gradients")]
    public Gradient heightColorGradient;
    public Gradient terrainTypeGradient;

    [Header("Display")]
    public float cellSize = 1.0f;
    public bool cellSizeFromPrefab = true;
    public int chunkSize = 10;

    [Header("Resources")]
    public bool generateResources = true;
    public ResourceSettings[] resourceSettings;

    // computed
    [Header("Computed, Don't Touch")]
    public Vector2 physicalMapSize;
    private int chunksX;
    private int chunksZ;
    public HexTile[,] map;

    // prefab mesh data
    private Mesh tileMesh;
    private float tileTopYOffset;
    private Vector3[] baseVerts;
    private Vector3[] baseNormals;
    private Vector2[] baseUVs;
    private int[] baseTris;

    // parents for all GameObjects
    private Transform chunkParent;
    private Transform waterParent;

    GameObject WaterObject;
    void Awake()
    {
        // seed
        if (randomSeed)
            seed = Random.Range(-100000, 100000);

        // allocate map
        map = new HexTile[mapWidth, mapHeight];

        // compute how many chunks
        chunksX = Mathf.CeilToInt((float)mapWidth / chunkSize);
        chunksZ = Mathf.CeilToInt((float)mapHeight / chunkSize);

        // grab the tile prefab mesh once
        tileMesh = hexTilePrefab.GetComponent<MeshFilter>().sharedMesh;
        baseVerts = tileMesh.vertices;
        baseNormals = tileMesh.normals;
        baseUVs = tileMesh.uv;
        baseTris = tileMesh.triangles;

        // create a container for chunks
        chunkParent = new GameObject("ChunkParent").transform;
        chunkParent.parent = this.transform;
        waterParent = new GameObject("WaterParent").transform;
        waterParent.parent = this.transform;



        var r = hexTilePrefab.GetComponent<Renderer>();
        tileTopYOffset = r.bounds.center.y + r.bounds.extents.y;
    }

    void Start()
    {
        if (cellSizeFromPrefab && hexTilePrefab != null)
        {
            cellSize = hexTilePrefab.GetComponent<Renderer>().bounds.size.x;
        }

        PopulateMap();
        GenerateResources();
        DisplayMap();
    }

    void Update()
    {
        if (Keyboard.current.rKey.wasPressedThisFrame)
        {
            if (randomSeed)
            {
                seed = Random.Range(-100000, 100000);
            }
            
            PopulateMap();
            GenerateResources();
            DisplayMap();
        }
    }

    void PopulateMap()
    {
        physicalMapSize = new Vector2(mapWidth * cellSize, mapHeight * cellSize * HEX_VERTICAL_OFFSET_MULTIPLIER);
        // 1) First pass: compute positions/heights and track global min/max (at tile SURFACE)
        float[,] surfY = new float[mapWidth, mapHeight];
        Vector3[,] worldPos = new Vector3[mapWidth, mapHeight];
        int[,] terrType = new int[mapWidth, mapHeight];

        float globalMin = float.MaxValue;
        float globalMax = float.MinValue;

        for (int x = 0; x < mapWidth; x++)
        {
            for (int z = 0; z < mapHeight; z++)
            {
                var pos = GetWorldPosition(x, z, true);
                pos.y   = GenerateHeightMap(x, z, mapScale, seed, mapLacunarity, mapGain, mapOctaves);

                if (useFalloff)
                {
                    float falloff = CalculateFalloff(x, z);
                    pos.y *= falloff;
                }

                // terrain type can be based on raw height or modified height — keeping your order:
                int terrainType = CalculateTerrainTypes(pos.y);

                pos.y = MapHeightModifier(pos.y);

                if(stepHeight)  pos.y = Mathf.Round(pos.y * stepHeightResolution) / stepHeightResolution;

                // surface height (not mesh center)
                float surface = pos.y + tileTopYOffset;

                surfY[x, z]   = surface;
                worldPos[x, z]= pos;
                terrType[x, z]= terrainType;

                if (surface < globalMin) globalMin = surface;
                if (surface > globalMax) globalMax = surface;
            }
        }

        // Guard against flat maps
        float denom = (globalMax - globalMin);
        if (denom <= 1e-6f) denom = 1f;

        // 2) Second pass: colorize from 0..1 using ONLY heightColorGradient
        for (int x = 0; x < mapWidth; x++)
        {
            for (int z = 0; z < mapHeight; z++)
            {
                float t = (surfY[x, z] - globalMin) / denom;   // 0 at lowest, 1 at highest
                var col = heightColorGradient.Evaluate(Mathf.Clamp01(t));

                map[x, z] = new HexTile(worldPos[x, z], terrType[x, z], col);
            }
        }
    }

    float MapHeightModifier(float posY)
    {
        posY = Mathf.Pow(posY, mapExponent);
        posY = (posY - 0.5f) * mapAmplitude + 0.5f;
        posY += yOffset;
        return posY;
    }

    int CalculateTerrainTypes(float posY)
    {
        int terrainType = 0;
        for (int i = 0; i < terrainTypeGradient.colorKeys.GetLength(0); i++)
        {
            if (i == 0 && posY < terrainTypeGradient.colorKeys[i].time)
            {
                terrainType = 0;
            }
            else if (posY < terrainTypeGradient.colorKeys[i].time && posY > terrainTypeGradient.colorKeys[i - 1].time)
            {
                terrainType = i;
            }
            else if (i == terrainTypeGradient.colorKeys.GetLength(0) - 1 && posY > terrainTypeGradient.colorKeys[i - 1].time)
            {
                terrainType = terrainTypeGradient.colorKeys.GetLength(0) - 1;
            }
        }
        return terrainType;
    }

    void GenerateResources()
    {
        if (resourceSettings == null || resourceSettings.Length == 0) return;

        for (int x = 0; x < mapWidth; x++)
            for (int z = 0; z < mapHeight; z++)
            {
                var tile = map[x, z];
                if (tile == null) continue;

                float surfaceY = tile.position.y + tileTopYOffset;
                for (int i = 0; i < resourceSettings.Length; i++)
                {
                    var rs = resourceSettings[i];
                    int resourceTypeIndex = (int)rs.type;
                    
                    if (rs.requireAboveWater && surfaceY < waterLevel)
                    {
                        tile.resources[resourceTypeIndex] = 0f;
                        continue;
                    }
                    if (rs.requireBelowWater && surfaceY >= waterLevel)
                    {
                        tile.resources[resourceTypeIndex] = 0f;
                        continue;
                    }

                    float noiseValue;
                    float nx = (x + Random.Range(-10000, 10000)) * rs.noiseScale * 0.001f;
                    float ny = (z + Random.Range(-10000, 10000)) * rs.noiseScale * 0.001f;
                    noiseValue = Mathf.PerlinNoise(nx, ny);
                    noiseValue = Mathf.Clamp01(noiseValue);
                    noiseValue *= rs.maxAmount;
                    tile.resources[resourceTypeIndex] = noiseValue;
                }
            }
    }

    void DisplayMap()
    {
        // clear old water objects if they exist
        var oldWaters = GameObject.FindGameObjectsWithTag("ChunkWater");
        foreach (var w in oldWaters)
        {
            Destroy(w);
        }
        // clear old chunks
        for (int i = chunkParent.childCount - 1; i >= 0; i--)
        {
            Destroy(chunkParent.GetChild(i).gameObject);
        }

        // build new chunks and water per chunk
        for (int cx = 0; cx < chunksX; cx++)
        {
            for (int cz = 0; cz < chunksZ; cz++)
            {
                BuildChunk(cx, cz);
                GenerateWater(cx, cz);
            }
        }
    }

    void GenerateWater(int cx, int cz)
    {
    var waterObj = Instantiate(waterPrefab.gameObject);
    waterObj.name = $"ChunkWater_{cx}_{cz}";
    waterObj.tag = "ChunkWater";
    waterObj.transform.parent = waterParent;

    // Calculate chunk bounds
    int chunkTilesX = Mathf.Min(chunkSize, mapWidth - cx * chunkSize);
    int chunkTilesZ = Mathf.Min(chunkSize, mapHeight - cz * chunkSize);

    float chunkPosX = (cx * chunkSize + chunkTilesX * 0.5f) * cellSize;
    float chunkPosZ = (cz * chunkSize + chunkTilesZ * 0.5f) * cellSize * HEX_VERTICAL_OFFSET_MULTIPLIER;

    // Calculate water scale to cover the chunk bounds
    var size = waterObj.GetComponent<Renderer>().bounds.size;
    float waterScaleX = (chunkTilesX * cellSize) / size.x;
    float waterScaleZ = (chunkTilesZ * cellSize * HEX_VERTICAL_OFFSET_MULTIPLIER) / size.z;

    // Main chunk water (no buffer)
    waterObj.transform.position = new Vector3(chunkPosX, waterLevel, chunkPosZ);
    waterObj.transform.localScale = new Vector3(waterScaleX, 1, waterScaleZ);

    // Add buffer planes only at map edges and corners
    float chunkWorldMinX = cx * chunkSize * cellSize;
    float chunkWorldMaxX = chunkWorldMinX + chunkTilesX * cellSize;
    float chunkWorldMinZ = cz * chunkSize * cellSize * HEX_VERTICAL_OFFSET_MULTIPLIER;
    float chunkWorldMaxZ = chunkWorldMinZ + chunkTilesZ * cellSize * HEX_VERTICAL_OFFSET_MULTIPLIER;

    // Left edge
    if (cx == 0)
        CreateWaterPlane(
            new Vector3(chunkWorldMinX - waterBuffer * 0.5f, 0, (chunkWorldMinZ + chunkWorldMaxZ) * 0.5f),
            waterBuffer,
            chunkWorldMaxZ - chunkWorldMinZ,
            waterLevel,
            $"EdgeWater_Left_{cx}_{cz}"
        );
    // Right edge
    if (cx == chunksX - 1)
        CreateWaterPlane(
            new Vector3(chunkWorldMaxX + waterBuffer * 0.5f, 0, (chunkWorldMinZ + chunkWorldMaxZ) * 0.5f),
            waterBuffer,
            chunkWorldMaxZ - chunkWorldMinZ,
            waterLevel,
            $"EdgeWater_Right_{cx}_{cz}"
        );
    // Bottom edge
    if (cz == 0)
        CreateWaterPlane(
            new Vector3((chunkWorldMinX + chunkWorldMaxX) * 0.5f, 0, chunkWorldMinZ - waterBuffer * 0.5f),
            chunkWorldMaxX - chunkWorldMinX,
            waterBuffer,
            waterLevel,
            $"EdgeWater_Bottom_{cx}_{cz}"
        );
    // Top edge
    if (cz == chunksZ - 1)
        CreateWaterPlane(
            new Vector3((chunkWorldMinX + chunkWorldMaxX) * 0.5f, 0, chunkWorldMaxZ + waterBuffer * 0.5f),
            chunkWorldMaxX - chunkWorldMinX,
            waterBuffer,
            waterLevel,
            $"EdgeWater_Top_{cx}_{cz}"
        );
    // Corners
    if (cx == 0 && cz == 0)
        CreateWaterPlane(
            new Vector3(chunkWorldMinX - waterBuffer * 0.5f, 0, chunkWorldMinZ - waterBuffer * 0.5f),
            waterBuffer, waterBuffer, waterLevel, $"EdgeWater_CornerBL_{cx}_{cz}"
        );
    if (cx == 0 && cz == chunksZ - 1)
        CreateWaterPlane(
            new Vector3(chunkWorldMinX - waterBuffer * 0.5f, 0, chunkWorldMaxZ + waterBuffer * 0.5f),
            waterBuffer, waterBuffer, waterLevel, $"EdgeWater_CornerTL_{cx}_{cz}"
        );
    if (cx == chunksX - 1 && cz == 0)
        CreateWaterPlane(
            new Vector3(chunkWorldMaxX + waterBuffer * 0.5f, 0, chunkWorldMinZ - waterBuffer * 0.5f),
            waterBuffer, waterBuffer, waterLevel, $"EdgeWater_CornerBR_{cx}_{cz}"
        );
    if (cx == chunksX - 1 && cz == chunksZ - 1)
        CreateWaterPlane(
            new Vector3(chunkWorldMaxX + waterBuffer * 0.5f, 0, chunkWorldMaxZ + waterBuffer * 0.5f),
            waterBuffer, waterBuffer, waterLevel, $"EdgeWater_CornerTR_{cx}_{cz}"
        );
}

    void CreateWaterPlane(Vector3 center, float width, float length, float y, string name)
    {
        var go = Instantiate(waterPrefab.gameObject);
        go.name = name;
        go.tag = "ChunkWater";
        go.transform.parent = waterParent;

        var baseSize = go.GetComponent<Renderer>().bounds.size;
        float sx = Mathf.Approximately(baseSize.x, 0f) ? 1f : width / baseSize.x;
        float sz = Mathf.Approximately(baseSize.z, 0f) ? 1f : length / baseSize.z;

        go.transform.position = new Vector3(center.x, y, center.z);
        go.transform.localScale = new Vector3(sx, 1f, sz);
    }

    void BuildChunk(int cx, int cz)
    {
        var verts = new List<Vector3>();
        var normals = new List<Vector3>();
        var uvs = new List<Vector2>();
        var cols = new List<Color>();
        var tris = new List<int>();

        // iterate tiles within this chunk
        for (int lx = 0; lx < chunkSize; lx++)
        {
            for (int lz = 0; lz < chunkSize; lz++)
            {

                int wx = cx * chunkSize + lx;
                int wz = cz * chunkSize + lz;

                if (wx >= mapWidth || wz >= mapHeight) continue;
                if (map[wx, wz] == null) continue;
                var tile = map[wx, wz];

                int vertOffset = verts.Count;

                // add vertices, normals, uvs, colors
                for (int i = 0; i < baseVerts.Length; i++)
                {
                    verts.Add(baseVerts[i] + tile.position);
                    normals.Add(baseNormals[i]);
                    uvs.Add(baseUVs[i]);
                    cols.Add(tile.color);
                }

                // add triangles (shifted by vertOffset)
                for (int i = 0; i < baseTris.Length; i++)
                {
                    tris.Add(baseTris[i] + vertOffset);
                }
            }
        }

        // create the chunk mesh
        var mesh = new Mesh();
        mesh.indexFormat = verts.Count > 65000
            ? UnityEngine.Rendering.IndexFormat.UInt32
            : UnityEngine.Rendering.IndexFormat.UInt16;

        mesh.SetVertices(verts);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.SetColors(cols);
        mesh.SetTriangles(tris, 0);

        // instantiate chunk GameObject
        var go = new GameObject($"Chunk_{cx}_{cz}");
        go.transform.parent = chunkParent;
        go.transform.localPosition = Vector3.zero;

        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;

        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = hexTilePrefab.GetComponent<MeshRenderer>().sharedMaterial;

        var mc = go.AddComponent<MeshCollider>();
        mc.sharedMesh = mesh;

        // free CPU copy
        mesh.UploadMeshData(true);

        go.isStatic = true;
        go.layer = LayerMask.NameToLayer("Map");
    }

    float GenerateHeightMap(int x, int y, float scale, int seed, float lacunarity, float gain, int octaves)
    {
        float h         = 0;
        float amplitude= 1;
        float frequency= 1;
        scale /= 100;

        for (int o = 0; o < octaves; o++)
        {
            float xC = x * scale * frequency + seed;
            float yC = y * scale * frequency + seed;
            float n  = Mathf.PerlinNoise(xC, yC);
            h       += n * amplitude;
            amplitude *= gain;
            frequency *= lacunarity;
        }

        return h;
    }

    float CalculateFalloff(int x, int z)
    {
        float centerX = mapWidth * 0.5f;
        float centerZ = mapHeight * 0.5f;

        float maxDistX = centerX;
        float maxDistZ = centerZ;

        float distX = Mathf.Abs(x - centerX) / maxDistX;
        float distZ = Mathf.Abs(z - centerZ) / maxDistZ;

        float normalizedDist = Mathf.Max(distX, distZ);

        if (normalizedDist < mapFalloffStartDistance)
            return 1f;

        float remappedDist = (normalizedDist - mapFalloffStartDistance) / (1 - mapFalloffStartDistance);

        float falloffValue = Mathf.Pow(1 - remappedDist, mapFalloffExponent);

        float minFalloff = 0f;
        return Mathf.Clamp(falloffValue, minFalloff, 1f);
    }

    public Vector2Int GetGridPosition(float worldX, float worldZ)
    {
        float rawY = worldZ / (cellSize * HEX_VERTICAL_OFFSET_MULTIPLIER);
        int gridY = Mathf.RoundToInt(rawY);

        bool oddRow = (gridY & 1) == 1;
        float rawX = (worldX - (oddRow ? cellSize * 0.5f : 0f)) / cellSize;
        int gridX = Mathf.RoundToInt(rawX);

        var rounded = new Vector2Int(gridX, gridY);
        var neighbors = new Vector2Int[]
        {
            new Vector2Int( 0,  0),
            new Vector2Int(-1,  0), new Vector2Int( 1,  0),
            new Vector2Int(oddRow ? 1 : -1,  1), new Vector2Int(0,  1),
            new Vector2Int(oddRow ? 1 : -1, -1), new Vector2Int(0, -1),
        };

        var best = rounded;
        float bestD = float.MaxValue;
        var p = new Vector2(worldX, worldZ);

        foreach (var n in neighbors)
        {
            var g = rounded + n;
            var w = GetWorldPosition(g.x, g.y);
            float d = Vector2.SqrMagnitude(p - new Vector2(w.x, w.z));
            if (d < bestD)
            {
                bestD = d;
                best = g;
            }
        }

        return best;
    }

    public Vector3 GetWorldPosition(int xPos, int zPos, bool ignoreHeight = false, bool HighestPoint = false)
    {

        var Renderer = hexTilePrefab.GetComponent<Renderer>();
        return new Vector3(xPos, 0, 0) * cellSize
             + new Vector3(0, 0, zPos) * cellSize * HEX_VERTICAL_OFFSET_MULTIPLIER
             + ((zPos % 2) == 1
                ? new Vector3(1, 0, 0) * cellSize * 0.5f
                : Vector3.zero)
             + (ignoreHeight || xPos >= mapWidth || zPos >= mapHeight || xPos < 0 || zPos < 0 || map[xPos, zPos] == null ? Vector3.zero :
             new Vector3(0, map[xPos, zPos].position.y + (HighestPoint ? Renderer.bounds.center.y + Renderer.bounds.extents.y : 0), 0));
    }
}
