using System.Buffers;
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
        this.color = color ?? Color.white; // Use provided color or default to white
    }
}


public class MapGenScript : MonoBehaviour
{

    private const float HEX_VERTICAL_OFFSET_MULTIPLIER = 0.8660254f; // Mathf.Sqrt(3) / 2 for perfect hex

    public int mapWidth = 10; // Number of hex tiles in the x direction
    public int mapHeight = 10; // Number of hex tiles in the z direction
    public float mapElevationMultiplier = 1;
    public float mapScale = 1;
    public Gradient heightRemapCurve;
    public Gradient heightColorGradient;
    public float cellSize = 1.0f; // Size of each hex tile

    public bool cellSizeFromPrefab = true; // Use cell size from prefab if true
    public Transform hexTilePrefab; // Prefab for the hex tile
    
    HexTile[,] map;
    Matrix4x4[] matrices;
    RenderParams rp;
    public int seed = 0;
    MaterialPropertyBlock props;

    void Awake()
    {
        seed = Random.Range(-100000, 100000);
        matrices = new Matrix4x4[mapWidth * mapHeight];
        map = new HexTile[mapWidth, mapHeight];

        var mr = hexTilePrefab.GetComponent<MeshRenderer>();
        rp = new RenderParams(mr.sharedMaterial);
        props = new MaterialPropertyBlock();
    }
    void Start()
    {
        if (cellSizeFromPrefab && hexTilePrefab != null)
        {
            cellSize = hexTilePrefab.GetComponent<Renderer>().bounds.size.x;
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
            for (int z = 0; z < mapHeight; z++)
            {
                float h = Mathf.PerlinNoise(x * mapScale + seed, z * mapScale + seed);

                Vector3 basePos = GetWorldPosition(x, z);
                basePos.y = heightRemapCurve.Evaluate(h).b * mapElevationMultiplier;

                Color color = heightColorGradient.Evaluate(basePos.y);
                map[x, z] = new HexTile(basePos, 0, color);
            }
    }

    void DisplayMap()
    {
        var colors = new Vector4[mapWidth * mapHeight];
        for (int x = 0; x < mapWidth; x++)
            for (int z = 0; z < mapHeight; z++)
            {
                int index = x * mapHeight + z;
                matrices[index] = Matrix4x4.TRS(map[x, z].position, Quaternion.identity, Vector3.one);
                colors[index] = map[x, z].color;
            }

        var mesh = hexTilePrefab.GetComponent<MeshFilter>().sharedMesh;
        var mats = hexTilePrefab.GetComponent<MeshRenderer>().sharedMaterials;

        props.Clear();
        props.SetVectorArray("_Color", colors); // this is the magic line

        for (int sub = 0; sub < mats.Length; sub++)
        {
            var rpSub = new RenderParams(mats[sub])
            { matProps = props };

            Graphics.RenderMeshInstanced(rpSub, mesh, sub, matrices, matrices.Length);
        }
    }

    public Vector2Int GetGridPosition(float worldX, float worldZ)
    {
        // 1) Compute rough Y
        float rawY = worldZ / (cellSize * HEX_VERTICAL_OFFSET_MULTIPLIER);
        int gridY = Mathf.RoundToInt(rawY);

        // 2) Apply odd-row X offset
        bool oddRow = (gridY & 1) == 1; // faster than %2
        float rawX = (worldX - (oddRow ? cellSize * 0.5f : 0f)) / cellSize;
        int gridX = Mathf.RoundToInt(rawX);

        Vector2Int rounded = new Vector2Int(gridX, gridY);

        // 3) Check all 6 neighbours + self to pick the closest
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
