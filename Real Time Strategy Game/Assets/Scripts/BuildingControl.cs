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
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit))
        {
            selectedPosition = hit.point;
            var gridPosition = map.GetGridPosition(selectedPosition.x, selectedPosition.z);
            selectedTile = map.GetWorldPosition(gridPosition.x, gridPosition.y, false, true);
            if (map.map[gridPosition.x, gridPosition.y] != null)
            {
                print(map.map[gridPosition.x, gridPosition.y].resources[0]);
            }
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
