using UnityEngine;

public class BuildingControl : MonoBehaviour
{

    public bool DebugMode = false;
    public MapGenScript map;

    Camera cam;
    Vector3 selectedPosition;
    Vector3 selectedTile;
    void Start()
    {
        cam = Camera.main;
    }

    void Update()
    {
        Select();
    }

    void Select()
    {
        var pointer = UnityEngine.InputSystem.Pointer.current;
        if (pointer == null) return;

        Vector2 screenPos = pointer.position.ReadValue();
        Ray ray = cam.ScreenPointToRay(screenPos);
        if (Physics.Raycast(ray, out var hit))
        {
            selectedPosition = hit.point;
            var gridPosition = map.GetGridPosition(selectedPosition.x, selectedPosition.z);
            selectedTile = map.GetWorldPosition(gridPosition.x, gridPosition.y, false, true);
            if (gridPosition.x >= 0 && gridPosition.x < map.mapWidth && gridPosition.y >= 0 && gridPosition.y < map.mapHeight)
                print(map.map[gridPosition.x, gridPosition.y].resources[0]);
        }
    }

    void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(selectedPosition, 0.5f);
        Gizmos.color = Color.green;
        Gizmos.DrawSphere(selectedTile, 0.5f);
    }
}
