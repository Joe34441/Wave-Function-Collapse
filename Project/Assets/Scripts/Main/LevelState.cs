using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct LevelState
{
    public Dictionary<Vector3, ModulePrototype> level;// = new Dictionary<Vector3, ModulePrototype>();
    public Dictionary<Vector3, CellVariationList> cellsVariations;// = new Dictionary<Vector3, CellVariationList>();
    public HashSet<Vector3> collapsedCells;// = new HashSet<Vector3>();
}
