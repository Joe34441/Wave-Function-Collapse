using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UtilityHelper;

public class ObjectPlacer : MonoBehaviour
{
    private GridSystem gridSystem;

    private void Awake()
    {
        gridSystem = FindObjectOfType<GridSystem>();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Mouse0))
        {
            RaycastHit hit;
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out hit))
            {
                PlaceCubeNear(hit.point);
            }
        }
    }

    private void PlaceCubeNear(Vector3 position)
    {
        Vector3 newPosition = gridSystem.GetNearestPointOnGrid(position);

        GameObject newObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        newObj.transform.position = newPosition;
        newObj.transform.localScale = Utility.MakeVector3(gridSystem.cellSize);
    }
}
