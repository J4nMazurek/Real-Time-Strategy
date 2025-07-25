using System.Buffers;
using UnityEngine;

public struct HexTile
{
    public Vector3 position;
    public int terrainType;
    public Transform tileObject;


    public HexTile(Vector3 pos, int terrain, Transform tileObject = null)
    {
        position = pos;
        terrainType = terrain;
        this.tileObject = tileObject;
    }
}

public class MapGenScript : MonoBehaviour
{
    private const float HEX_VERTICAL_OFFSET_MULTIPLIER = 0.8660254f; // Mathf.Sqrt(3) / 2 for perfect hex

    public int mapWidth = 10; // Number of hex tiles in the x direction
    public int mapHeight = 10; // Number of hex tiles in the z direction
    public float mapElevationMultiplier = 1;
    public float mapScale = 1;

    public bool mapTimeOffset;
    public Vector2 mapOffsetDirection = Vector2.right;

    public float cellSize = 1.0f; // Size of each hex tile

    public Vector3 TestPosition = new Vector3(0, 0, 0); // Test position for debugging

    public bool cellSizeFromPrefab = true; // Use cell size from prefab if true
    public Transform hexTilePrefab; // Prefab for the hex tile

    public HexTile[,] map;

    void Awake()
    {
        map = new HexTile[mapWidth, mapHeight];
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
        Vector2Int gridPos = GetGridPosition(TestPosition.x, TestPosition.z);
        Debug.Log($"Grid Position: {gridPos.x}, {gridPos.y}");
        map[gridPos.x, gridPos.y].tileObject.GetComponent<Renderer>().materials[1].color = Color.red;

        if (Input.GetKey(KeyCode.G))
        {
            for (int x = 0; x < mapWidth; x++)
            {
                for (int y = 0; y < mapHeight; y++)
                {
                    Destroy(map[x, y].tileObject.gameObject);
                }
            }
            PopulateMap();
            DisplayMap();
        }
    }

    void PopulateMap()
    {
        for (int x = 0; x < mapWidth; x++)
        {
            for (int z = 0; z < mapHeight; z++)
            {
                float terrainHeight = Mathf.PerlinNoise(x * mapScale + (mapTimeOffset ? mapOffsetDirection.x * Time.time : 0), z * mapScale + (mapTimeOffset ? mapOffsetDirection.y * Time.time : 0)) * mapElevationMultiplier;

                Vector3 XZPos = GetWorldPosition(x, z);
                map[x, z].position = new Vector3(XZPos.x, terrainHeight, XZPos.z);
            }
        }
    }

    void DisplayMap()
    {
        for (int x = 0; x < mapWidth; x++)
        {
            for (int z = 0; z < mapHeight; z++)
            {
                Vector3 worldPos = map[x, z].position;
                Transform hexTile = Instantiate(hexTilePrefab, worldPos, Quaternion.identity, transform);
                hexTile.name = $"HexTile_{x}_{z}";
                map[x, z] = new HexTile(worldPos, 0, hexTile);
            }
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

    void OnDrawGizmos()
    {
        Gizmos.DrawWireSphere(TestPosition, 0.1f);
    }
}
