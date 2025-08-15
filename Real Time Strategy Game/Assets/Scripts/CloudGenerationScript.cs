using UnityEngine;
using UnityEngine.InputSystem;

public class CloudGenerationScript : MonoBehaviour
{
    public Material cloudMaterial;
    public MapGenScript map;
    public Mesh tileMesh;
    [Range(1, 1023)]public int layers = 10;
    public float cloudHeight = 5f;
    public float altitude = 10f;
    public float cloudBuffer = 10f;

    private Vector2 layerSize;
    private Matrix4x4[] matrices;

    void Start()
    {
        RebuildMatrices();
    }

    void Update()
    {
        if (Keyboard.current.rKey.wasPressedThisFrame)
        {
            RebuildMatrices();
        }
        
        RenderClouds();
    }

    void RebuildMatrices()
    {
        matrices = new Matrix4x4[layers];
        layerSize = new Vector2(map.physicalMapSize.x, map.physicalMapSize.y);
        float layerHeight = cloudHeight / layers;
        for (int i = 0; i < layers; i++)
        {
            matrices[i] = Matrix4x4.TRS(new Vector3(map.physicalMapSize.x / 2, altitude + (layerHeight * i), map.physicalMapSize.y / 2),
            Quaternion.Euler(90, 0, 0),
            new Vector3(layerSize.x + cloudBuffer, layerSize.y + cloudBuffer, 1));
        }
    }

    void RenderClouds()
    {
        if (matrices == null)
        {
            print("Matrices not initialized or size mismatch. Call RebuildMatrices first.");
            return;
        }
        Graphics.DrawMeshInstanced(tileMesh, 0, cloudMaterial, matrices, matrices.Length);
    }
}
