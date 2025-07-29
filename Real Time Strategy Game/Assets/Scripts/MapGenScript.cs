using UnityEngine;

public struct HexTile
{
    public Vector3 position;
    public Color color;
    public int terrainType;

    public HexTile(Vector3 pos, int terrain, Color? color = null)
    {
        position = pos;
        terrainType = terrain;
        this.color = color ?? Color.white;
    }
}

public class MapGenScript : MonoBehaviour
{
    private const float HEX_VERTICAL_OFFSET_MULTIPLIER = 0.8660254f;

    [Header("Map Generation")]
    public int mapWidth = 10;
    public int mapHeight = 10;
    public float mapScale = 0.1f;
    public float mapAmplitude = 1f;
    public int mapExponent = 1;
    public int mapOctaves = 5;
    public float mapLacunarity = 2;
    public float mapGain = 0.2f;
    public int seed = 0;

    [Header("Map Coloring")]
    public Gradient heightColorGradient;

    [Header("Terrain")]
    public float cellSize = 1.0f;
    public bool cellSizeFromPrefab = true;
    public Transform hexTilePrefab;

    HexTile[,] map;
    Vector3 tileBounds;

    Matrix4x4[] allMatrices;
    Vector4[] allColors;

    Matrix4x4[] visibleMatrices;
    Vector4[] visibleColors;

    RenderParams rp;
    MaterialPropertyBlock props;

    void Awake()
    {
        seed = Random.Range(-100000, 100000);
        int tileCount = mapWidth * mapHeight;

        map = new HexTile[mapWidth, mapHeight];
        allMatrices = new Matrix4x4[tileCount];
        allColors = new Vector4[tileCount];
        visibleMatrices = new Matrix4x4[tileCount];
        visibleColors = new Vector4[tileCount];

        var mr = hexTilePrefab.GetComponent<MeshRenderer>();
        rp = new RenderParams(mr.sharedMaterial);
        props = new MaterialPropertyBlock();
    }

    void Start()
    {
        if (cellSizeFromPrefab && hexTilePrefab != null)
        {
            cellSize = hexTilePrefab.GetComponent<Renderer>().bounds.size.x;
            tileBounds = hexTilePrefab.GetComponent<Renderer>().bounds.size;
        }

        PopulateMap();
        DisplayMap();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            PopulateMap();
        }

        DisplayMap();
    }

    void PopulateMap()
    {
        for (int x = 0; x < mapWidth; x++)
        {
            for (int z = 0; z < mapHeight; z++)
            {
                Vector3 basePos = GetWorldPosition(x, z);
                basePos.y = GenerateHeightMap(x, z, mapScale, seed, mapLacunarity, mapGain, mapOctaves);
                Color color = heightColorGradient.Evaluate(basePos.y);

                basePos.y = Mathf.Pow(basePos.y, mapExponent);
                basePos.y -= 0.5f;
                basePos.y *= mapAmplitude;
                basePos.y += 0.5f;

                int index = x * mapHeight + z;
                map[x, z] = new HexTile(basePos, 0, color);
                allColors[index] = color;
            }
        }

        RebuildRenderData();
    }

    void RebuildRenderData()
    {
        for (int x = 0; x < mapWidth; x++)
        {
            for (int z = 0; z < mapHeight; z++)
            {
                int index = x * mapHeight + z;
                allMatrices[index] = Matrix4x4.TRS(map[x, z].position, Quaternion.identity, Vector3.one);
            }
        }
    }

    float GenerateHeightMap(int x, int y, float scale, int seed, float lacunarity, float gain, int octaves)
    {
        float h = 0;
        float amplitude = 1;
        float frequency = 1;
        scale /= 100;

        for (int octave = 0; octave < octaves; octave++)
        {
            float xCoord = (x * scale * frequency) + seed;
            float yCoord = (y * scale * frequency) + seed;
            float noiseValue = Mathf.PerlinNoise(xCoord, yCoord);
            h += noiseValue * amplitude;
            amplitude *= gain;
            frequency *= lacunarity;
        }

        return h;
    }

    void DisplayMap()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        Plane[] frustumPlanes = GeometryUtility.CalculateFrustumPlanes(cam);

        var mesh = hexTilePrefab.GetComponent<MeshFilter>().sharedMesh;
        var mats = hexTilePrefab.GetComponent<MeshRenderer>().sharedMaterials;

        int count = 0;
        for (int i = 0; i < allMatrices.Length; i++)
        {
            Vector3 pos = allMatrices[i].GetColumn(3);
            Bounds bounds = new Bounds(pos, tileBounds);

            if (!GeometryUtility.TestPlanesAABB(frustumPlanes, bounds))
                continue;

            visibleMatrices[count] = allMatrices[i];
            visibleColors[count] = allColors[i];
            count++;
        }

        if (count == 0) return;

        props.Clear();
        props.SetVectorArray("_Color", visibleColors);

        for (int sub = 0; sub < mats.Length; sub++)
        {
            var rpSub = new RenderParams(mats[sub]) { matProps = props };
            Graphics.RenderMeshInstanced(rpSub, mesh, sub, visibleMatrices, count);
        }
    }

    public Vector2Int GetGridPosition(float worldX, float worldZ)
    {
        float rawY = worldZ / (cellSize * HEX_VERTICAL_OFFSET_MULTIPLIER);
        int gridY = Mathf.RoundToInt(rawY);

        bool oddRow = (gridY & 1) == 1;
        float rawX = (worldX - (oddRow ? cellSize * 0.5f : 0f)) / cellSize;
        int gridX = Mathf.RoundToInt(rawX);

        Vector2Int rounded = new Vector2Int(gridX, gridY);

        Vector2Int[] neighbors = {
            new Vector2Int(+0,  0),
            new Vector2Int(-1,  0), new Vector2Int(+1,  0),
            new Vector2Int(oddRow ? +1 : -1, +1), new Vector2Int( 0, +1),
            new Vector2Int(oddRow ? +1 : -1, -1), new Vector2Int( 0, -1),
        };

        Vector2Int best = rounded;
        float bestDist = float.MaxValue;
        var p = new Vector2(worldX, worldZ);

        foreach (var n in neighbors)
        {
            var g = rounded + n;
            var w = GetWorldPosition(g.x, g.y);
            float d = Vector2.SqrMagnitude(p - new Vector2(w.x, w.z));
            if (d < bestDist)
            {
                bestDist = d;
                best = g;
            }
        }

        return best;
    }

    public Vector3 GetWorldPosition(int xPos, int zPos)
    {
        return
            new Vector3(xPos, 0, 0) * cellSize +
            new Vector3(0, 0, zPos) * cellSize * HEX_VERTICAL_OFFSET_MULTIPLIER +
            ((zPos % 2) == 1 ? new Vector3(1, 0, 0) * cellSize * 0.5f : Vector3.zero);
    }
}
