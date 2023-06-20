using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UtilityHelper;

public class GridSystem : MonoBehaviour
{
    [SerializeField][Range(1f, 5f)] public float cellSize;

    [SerializeField] private bool ShowCellPositions;

    private int width = 35;
    private int height = 4;
    private int depth = 35;


    public Vector3 GetNearestPointOnGrid(Vector3 position)
    {
        position -= transform.position;
        
        int xCount = Mathf.RoundToInt(position.x / cellSize);
        int yCount = Mathf.RoundToInt(position.y / cellSize);
        int zCount = Mathf.RoundToInt(position.z / cellSize);

        Vector3 result = new Vector3((float)xCount * cellSize, (float)yCount * cellSize, (float)zCount * cellSize);

        result += transform.position;

        return result;
    }

    private void OnDrawGizmos()
    {
        if (!ShowCellPositions) return;

        Gizmos.color = Color.cyan;

        for (float x = 0; x < cellSize * width; x += cellSize)
        {
            for (float y = 0; y < cellSize * height; y += cellSize)
            {
                for (float z = 0; z < cellSize * depth; z += cellSize)
                {
                    Vector3 rawPoint = Camera.current.transform.position;
                    rawPoint.x += x - ((cellSize * width) / 2) + cellSize;
                    rawPoint.y = y;
                    rawPoint.z += z - ((cellSize * depth) / 2) + cellSize;

                    Vector3 point = GetNearestPointOnGrid(rawPoint);
                    Gizmos.DrawCube(point, Utility.MakeVector3(cellSize / 5.0f));
                }
            }
        }
    }
}
