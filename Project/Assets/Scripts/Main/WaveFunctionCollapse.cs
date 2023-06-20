using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System;
using UtilityHelper;
using System.Linq;

public class WaveFunctionCollapse : MonoBehaviour
{
    [SerializeField] private GameObject levelHousing;
    [SerializeField] private ModulePrototype emptyPrototype;
    [SerializeField] private List<ModulePrototype> modulePrototypes = new List<ModulePrototype>();
    [SerializeField] private Vector3 levelDimensions = new Vector3(35, 4, 35);
    private Dictionary<Vector3, ModulePrototype> level = new Dictionary<Vector3, ModulePrototype>();
    private Dictionary<Vector3, CellVariationList> cellsVariations = new Dictionary<Vector3, CellVariationList>();

    private List<CellVariationList> fullLayerVariationLists = new List<CellVariationList>();

    private HashSet<Vector3> collapsedCells = new HashSet<Vector3>();

    private CellVariationList fullCellVariationList;

    private List<int> fullCellVariationSizes = new List<int>();

    private bool generating = false;
    private bool generationSuccess = false;

    private bool backtracked = false;
    private bool exitPropagation = false;

    private DateTime maxGenerationDateTime;
    private DateTime startTime;
    private DateTime endTime;
    double totalSubtime = 0;

    private Vector3 currentCell;

    private Vector3 startCell;

    private HashSet<Vector3> cellsToInfluence = new HashSet<Vector3>();

    private HashSet<Vector3> cellsToPropagate = new HashSet<Vector3>();
    private HashSet<Vector3> propagatedCells = new HashSet<Vector3>();

    private List<LevelState> levelStates = new List<LevelState>();
    private int recordStateInterval = 10;
    private int recordStateCounter = 0;
    private int maxBacktrackingFailures = 25;
    private int backtrackingFailuresCounter = 0;

    [SerializeField] [Range(1, 120)] public int maxGenerationTime = 30;
    [SerializeField] public bool useBacktracking = false;
    [SerializeField] public bool useRandomSeed = false;
    [SerializeField][Range(0, 1000000)] public int seed = 0;

    [SerializeField] public bool showGrid = false;
    [SerializeField] public bool showConstraints = false;
    [SerializeField][Range(5, 20)] public int viewableConstraintDistance = 8;


    void Start()
    {
        startTime = DateTime.UtcNow;
        generating = false;
        maxGenerationDateTime = DateTime.UtcNow.AddSeconds(maxGenerationTime);
        Generate();
        endTime = DateTime.UtcNow;

        Debug.Log("Generation time: " + (endTime - startTime).TotalMilliseconds + " miliseconds.");
    }

    private void Generate()
    {
        GenerationSetup();
        GenerateLevel();
        FillLevel();
    }

    private void GenerateLevel()
    {
        if (generating) return;

        generationSuccess = false;
        generating = true;

        int maxCells = (int)(levelDimensions.x * levelDimensions.y * levelDimensions.z);

        FillCellsVariations();

        //get centre cell
        currentCell.x = Mathf.Round(levelDimensions.x / 2);
        currentCell.y = Mathf.Round(levelDimensions.y / 2);
        currentCell.z = Mathf.Round(levelDimensions.z / 2);
        currentCell.y = 0;
        //Collapse centre cell. If inserting multi-cell structures at start, this is not needed as its neighbouring uncollapsed
        //  cells will have lower entropy and so have a higher priority for collapse and propagation.
        CollapseCell(currentCell, true);
        Propagate(currentCell);

        while (generating)
        {
            int size = int.MaxValue;
            DateTime start = DateTime.UtcNow;

            foreach (KeyValuePair<Vector3, CellVariationList> kvp in cellsVariations.Where(item => item.Value.list.Count() < size))
            {
                if (level.ContainsKey(kvp.Key)) continue;

                if (collapsedCells.Contains(kvp.Key)) continue;

                //set cell as current cell if it has lowest entropy
                size = kvp.Value.list.Count();
                currentCell = kvp.Key;
            }
            totalSubtime += (DateTime.UtcNow - start).TotalMilliseconds;

            CollapseCell(currentCell, false);

            if (useBacktracking)
            {
                if (!backtracked) Propagate(currentCell);
                backtracked = false;
            }
            else Propagate(currentCell);

            if (collapsedCells.Count() >= maxCells) generating = false;
            if (DateTime.UtcNow >= maxGenerationDateTime) generating = false;
        }

        Debug.Log("Cell to collapse selection total time: " + totalSubtime + " miliseconds");
    }

    private void FillLevel()
    {
        generationSuccess = true;

        if (generating || !generationSuccess) return;

        for (int x = 0; x < levelDimensions.x; ++x)
        {
            for (int y = 0; y < levelDimensions.y; ++y)
            {
                for (int z = 0; z < levelDimensions.z; ++z)
                {
                    if (level.ContainsKey(new Vector3(x, y, z)))
                    {
                        if (level[new Vector3(x, y, z)] != null)
                        {
                            Vector3 cellPos = new Vector3(x, y, z);

                            if (cellsVariations[cellPos].list.Count == 0) continue;

                            int rotationIndex = cellsVariations[cellPos].list.First().Value[1];

                            float yRot;

                            if (rotationIndex == 1) yRot = -90;
                            else if (rotationIndex == 2) yRot = -180;
                            else if (rotationIndex == 3) yRot = -270;
                            else yRot = 0;

                            Quaternion cellRot = Quaternion.Euler(0, yRot, 0);

                            Instantiate(level[cellPos].gameObject, cellPos * 3, cellRot, levelHousing.transform);
                        }
                    }
                }
            }
        }
    }

    private void GenerationSetup()
    {
        level.Clear();
        collapsedCells.Clear();

        if (!useRandomSeed) UnityEngine.Random.InitState(seed);
        else
        {
            UnityEngine.Random.InitState(DateTime.UtcNow.Millisecond * DateTime.UtcNow.Second * 104729);
            seed = UnityEngine.Random.Range(1, 1000000);
            UnityEngine.Random.InitState(seed);
        }
    }

    private bool validationFirstEmpty = false;
    private void CollapseCell(Vector3 cellToCollapse, bool firstCell = false)
    {
        if (firstCell)
        {
            List<int> variationList = new List<int>(cellsVariations[cellToCollapse].list.Keys);
            int randomIndex = GetWeightedRandomCellIndex(variationList);

            ModulePrototype modulePrototype = FindModulePrototype(cellsVariations[cellToCollapse].list.Keys.ToList()[randomIndex]);

            List<int> newList = cellsVariations[cellToCollapse].list.Values.ToArray()[randomIndex];

            cellsVariations[cellToCollapse].list.Clear();
            cellsVariations[cellToCollapse].list.Add(variationList[randomIndex], newList);

            level.Add(cellToCollapse, modulePrototype);

            if (useBacktracking)
            {
                LevelState initialState = new LevelState();
                initialState.level = new Dictionary<Vector3, ModulePrototype>(level);
                initialState.cellsVariations = new Dictionary<Vector3, CellVariationList>(cellsVariations);
                initialState.collapsedCells = new HashSet<Vector3>(collapsedCells);
                levelStates.Add(initialState);
            }
        }
        else
        {
            CheckExclusions(cellToCollapse);

            List<int> variationList = new List<int>(cellsVariations[cellToCollapse].list.Keys);
            int randomIndex = GetWeightedRandomCellIndex(variationList);

            bool validModule = true;

            if (variationList.Count == 0) RetryModuleSelection(cellToCollapse);
            else validModule = ValidateVariationList(cellToCollapse, randomIndex);

            bool hasRetried = false;

            while (!validModule)
            {
                if (variationList.Count == 0)
                {
                    if (hasRetried) validModule = true;
                    else
                    {
                        hasRetried = true;
                        RetryModuleSelection(cellToCollapse);
                        CheckExclusions(cellToCollapse);
                        variationList = new List<int>(cellsVariations[cellToCollapse].list.Keys);
                    }

                    continue;
                }

                randomIndex = GetWeightedRandomCellIndex(variationList);
                validModule = ValidateVariationList(cellToCollapse, randomIndex);

                if (!validModule) variationList.RemoveAt(randomIndex);
            }

            ModulePrototype modulePrototype;

            List<int> newList = new List<int>();
            variationList = new List<int>(cellsVariations[cellToCollapse].list.Keys);

            if (variationList.Count == 0)
            {
                modulePrototype = emptyPrototype;
                if (!cellsVariations[cellToCollapse].list.ContainsKey(0))
                {
                    newList.Add(0); newList.Add(0); newList.Add(0); newList.Add(0); newList.Add(0); newList.Add(0); newList.Add(0); newList.Add(0);
                    cellsVariations[cellToCollapse].list.Add(0, newList);
                }

                if (useBacktracking)
                {
                    if (!validationFirstEmpty)
                    {
                        int stateIndex;

                        if (backtrackingFailuresCounter >= maxBacktrackingFailures)
                        {
                            stateIndex = levelStates.Count - 2;
                            backtrackingFailuresCounter = 0;

                            levelStates.RemoveAt(2);

                            Debug.Log("Backtracking failed at depth 1, starting dept 2");
                        }
                        else stateIndex = levelStates.Count - 1;

                        level = new Dictionary<Vector3, ModulePrototype>(levelStates[stateIndex].level);
                        cellsVariations = new Dictionary<Vector3, CellVariationList>(levelStates[stateIndex].cellsVariations);
                        collapsedCells = new HashSet<Vector3>(levelStates[stateIndex].collapsedCells);

                        backtracked = true;
                        exitPropagation = true;
                        recordStateCounter = 0;
                        backtrackingFailuresCounter++;

                        return;
                    }
                }
            }
            else
            {
                modulePrototype = FindModulePrototype(variationList[randomIndex]);

                newList = cellsVariations[cellToCollapse].list.Values.ToArray()[randomIndex];

                cellsVariations[cellToCollapse].list.Clear();
                cellsVariations[cellToCollapse].list.Add(variationList[randomIndex], newList);
            }

            if (level.ContainsKey(cellToCollapse)) level.Remove(cellToCollapse);
            level.Add(cellToCollapse, modulePrototype);
        }

        if (useBacktracking)
        {
            recordStateCounter++;
            backtrackingFailuresCounter = 0;
            if (recordStateCounter >= recordStateInterval)
            {
                Debug.Log("Recording generation state for future backtracking");

                LevelState newState = new LevelState();
                newState.level = new Dictionary<Vector3, ModulePrototype>(level);
                newState.cellsVariations = new Dictionary<Vector3, CellVariationList>(cellsVariations);
                newState.collapsedCells = new HashSet<Vector3>(collapsedCells);

                if (levelStates.Count == 3)
                {
                    levelStates.RemoveAt(1);
                    levelStates.Add(newState);
                }
                else levelStates.Add(newState);

                recordStateCounter = 0;
            }
        }

        collapsedCells.Add(cellToCollapse);
    }

    private void FillCellsVariations()
    {
        cellsVariations.Clear();

        Dictionary<int, List<int>> moduleVariations = new Dictionary<int, List<int>>();

        for (int rotationIndex = 0; rotationIndex < 4; ++rotationIndex)
        {
            foreach (ModulePrototype modulePrototype in modulePrototypes)
            {
                List<int> newList = new List<int>();

                newList.Add(modulePrototype.modulePrototypeID);
                newList.Add(rotationIndex);

                int socketForwards;
                int socketLeft;
                int socketBackwards;
                int socketRight;

                if (rotationIndex == 1)
                {
                    socketForwards = modulePrototype.RightSocket;
                    socketLeft = modulePrototype.ForwardsSocket;
                    socketBackwards = modulePrototype.LeftSocket;
                    socketRight = modulePrototype.BackwardsSocket;
                }
                else if (rotationIndex == 2)
                {
                    socketForwards = modulePrototype.BackwardsSocket;
                    socketLeft = modulePrototype.RightSocket;
                    socketBackwards = modulePrototype.ForwardsSocket;
                    socketRight = modulePrototype.LeftSocket;
                }
                else if (rotationIndex == 3)
                {
                    socketForwards = modulePrototype.LeftSocket;
                    socketLeft = modulePrototype.BackwardsSocket;
                    socketBackwards = modulePrototype.RightSocket;
                    socketRight = modulePrototype.ForwardsSocket;
                }
                else
                {
                    socketForwards = modulePrototype.ForwardsSocket;
                    socketLeft = modulePrototype.LeftSocket;
                    socketBackwards = modulePrototype.BackwardsSocket;
                    socketRight = modulePrototype.RightSocket;
                }

                newList.Add(socketForwards);
                newList.Add(socketLeft);
                newList.Add(socketBackwards);
                newList.Add(socketRight);

                newList.Add(modulePrototype.UpwardsSocket);
                newList.Add(modulePrototype.DownwardsSocket);

                moduleVariations.Add(moduleVariations.Count, new List<int>(newList));
            }
        }

        fullCellVariationList = new CellVariationList();
        fullCellVariationList.list = new Dictionary<int, List<int>>(moduleVariations);

        for (int y = 0; y < levelDimensions.y; ++y)
        {
            CellVariationList newVariationList = new CellVariationList();
            newVariationList.list = new Dictionary<int, List<int>>(moduleVariations);

            foreach (int key in fullCellVariationList.list.Keys.ToArray())
            {
                if (FindModulePrototype(key).prototypeLayer != y) newVariationList.list.Remove(key);
            }

            fullCellVariationSizes.Add(newVariationList.list.Count());

            CellVariationList fullList = new CellVariationList();
            fullList.list = new Dictionary<int, List<int>>(newVariationList.list);
            fullLayerVariationLists.Add(newVariationList);

            for (int x = 0; x < levelDimensions.x; ++x)
            {
                for (int z = 0; z < levelDimensions.z; ++z)
                {
                    CellVariationList newList = new CellVariationList();
                    newList.list = new Dictionary<int, List<int>>(newVariationList.list);

                    cellsVariations.Add(new Vector3(x, y, z), newList);
                }
            }
        }
    }

    private void RemoveFromVariationsList(Vector3 cell, Dictionary<int, List<int>> toRemove)
    {
        foreach (KeyValuePair<int, List<int>> kvp in toRemove)
        {
            if (cellsVariations[cell].list.ContainsKey(kvp.Key))
            {
                cellsVariations[cell].list.Remove(kvp.Key);
            }
        }
    }
    private bool propagationFirstEmpty = false;
    private void Propagate(Vector3 centreCell)
    {
        HashSet<Vector3> propagatedFromCells = new HashSet<Vector3>();
        List<Vector3> cellsToPropagateFrom = new List<Vector3>();

        HashSet<Vector3> cellsToCollapse = new HashSet<Vector3>();

        cellsToPropagateFrom.Add(centreCell);

        while (cellsToPropagateFrom.Count > 0)
        {
            Vector3 currentCell = cellsToPropagateFrom[0];
            for (int i = 0; i <= 5; ++i)
            {
                Vector3 adjancentCell = GetAdjacentCells(currentCell, i);

                if (!collapsedCells.Contains(adjancentCell) &&
                    cellsVariations.ContainsKey(adjancentCell) &&
                    !propagatedFromCells.Contains(adjancentCell) &&
                    !cellsToCollapse.Contains(adjancentCell))
                {
                    Dictionary<int, List<int>> toRemove = Influence(adjancentCell, currentCell, i);

                    if (toRemove.Count > 0)
                    {
                        RemoveFromVariationsList(adjancentCell, toRemove);

                        if (!propagatedFromCells.Contains(adjancentCell))
                        {
                            if (!cellsToPropagateFrom.Contains(adjancentCell))
                            {
                                cellsToPropagateFrom.Add(adjancentCell);
                            }
                        }

                        if (!propagationFirstEmpty)
                        {
                            if (cellsVariations[adjancentCell].list.Count == 0)
                            {
                                propagationFirstEmpty = true;
                                Debug.Log("Propagation first empty cell: " + (adjancentCell * 3));
                            }
                        }

                        int listSize = cellsVariations[adjancentCell].list.Count;

                        if (listSize < 2)
                        {
                            if (!cellsToCollapse.Contains(adjancentCell)) cellsToCollapse.Add(adjancentCell);
                        }
                    }
                }
            }

            propagatedFromCells.Add(currentCell);
            cellsToPropagateFrom.Remove(currentCell);
        }

        foreach (Vector3 cell in cellsToCollapse)
        {
            CollapseCell(cell);
            if (exitPropagation)
            {
                exitPropagation = false;
                return;
            }
        }
    }

    private Dictionary<int, List<int>> Influence(Vector3 adjancent, Vector3 source, int direction)
    {
        Dictionary<int, List<int>> existing = cellsVariations[adjancent].list;

        Dictionary<int, List<int>> toRemove = new Dictionary<int, List<int>>();

        int facingDirection = GetOppositeDirection(direction);
        List<int> constraints = new List<int>();

        List<int> ids = new List<int>();

        if (cellsVariations[source].list.Keys.Count() == 0) constraints.Add(0);

        foreach (KeyValuePair<int, List<int>> kvp in cellsVariations[source].list)
        {
            int constraint = kvp.Value[direction + 2];
            if (!constraints.Contains(constraint)) constraints.Add(constraint);
            int id = kvp.Value[0];
            if (!ids.Contains(id)) ids.Add(id);
        }

        foreach (KeyValuePair<int, List<int>> kvp in existing)
        {
            if (!constraints.Contains(kvp.Value[facingDirection + 2])) toRemove.Add(kvp.Key, kvp.Value);
        }

        return toRemove;
    }

    private void CheckExclusions(Vector3 centreCell)
    {
        Vector3 neighbour = centreCell;
        neighbour.x += 1;
        RemoveExclusions(centreCell, neighbour, 2);
        neighbour.x -= 2;
        RemoveExclusions(centreCell, neighbour, 0);
        neighbour.x += 1;
        neighbour.z += 1;
        RemoveExclusions(centreCell, neighbour, 3);
        neighbour.z -= 2;
        RemoveExclusions(centreCell, neighbour, 1);
    }

    private void RemoveExclusions(Vector3 centreCell, Vector3 neighbour, int direction)
    {
        ModulePrototype mp;

        if (level.ContainsKey(neighbour))
        {
            mp = level[neighbour];

            if (!mp.ExcludeSelfInDirection) return;
            if (cellsVariations[neighbour].list.Count == 0) return;

            List<int> exclusions = new List<int>(mp.ExcludeDirections);
            int rotation = cellsVariations[neighbour].list.First().Value[1];

            //rotate exclusion directions with module rotation
            for (int i = 0; i < exclusions.Count; ++i)
            {
                exclusions[i] += rotation;
                if (exclusions[i] > 3) exclusions[i] -= 3;
            }

            if (exclusions.Contains(direction))
            {
                foreach (KeyValuePair<int, List<int>> kvp in cellsVariations[centreCell].list.ToArray())
                {
                    if (kvp.Value[0] == mp.modulePrototypeID)
                    {
                        cellsVariations[centreCell].list.Remove(kvp.Key);
                    }
                }
            }
        }
    }

    private bool ValidateVariationList(Vector3 cellToCollapse, int randomIndex)
    {
        Vector3 neighbour = cellToCollapse;
        List<List<int>> cellConstraints = cellsVariations[cellToCollapse].list.Values.ToList();

        neighbour.x += 1;
        if (!CheckValidJoint(cellConstraints, neighbour, randomIndex, 2, 4)) return false;

        neighbour.x -= 2;
        if (!CheckValidJoint(cellConstraints, neighbour, randomIndex, 4, 2)) return false;

        neighbour.x += 1;
        neighbour.z += 1;
        if (!CheckValidJoint(cellConstraints, neighbour, randomIndex, 3, 5)) return false;

        neighbour.z -= 2;
        if (!CheckValidJoint(cellConstraints, neighbour, randomIndex, 5, 3)) return false;

        neighbour.z += 1;
        neighbour.y += 1;
        if (!CheckValidJoint(cellConstraints, neighbour, randomIndex, 6, 7)) return false;

        neighbour.y -= 2;
        if (!CheckValidJoint(cellConstraints, neighbour, randomIndex, 7, 6)) return false;

        return true;
    }

    private bool CheckValidJoint(List<List<int>> cellConstraints, Vector3 neighbour, int randomIndex, int cellIndex, int jointIndex)
    {
        if (!level.ContainsKey(neighbour)) return true;
        return (cellConstraints[randomIndex][cellIndex] == cellsVariations[neighbour].list.Values.ToList()[0][jointIndex]);
    }

    private void RetryModuleSelection(Vector3 cell)
    {
        CellVariationList newList = new CellVariationList();
        newList.list = new Dictionary<int, List<int>>();

        List<int> validSockets = new List<int>();

        Vector3 neighbour = cell;

        neighbour.x += 1;
        validSockets.Add(GetValidSocket(neighbour, 4));

        neighbour.x -= 2;
        validSockets.Add(GetValidSocket(neighbour, 2));

        neighbour.x += 1;
        neighbour.z += 1;
        validSockets.Add(GetValidSocket(neighbour, 5));

        neighbour.z -= 2;
        validSockets.Add(GetValidSocket(neighbour, 3));

        neighbour.z += 1;
        neighbour.y += 1;
        validSockets.Add(GetValidSocket(neighbour, 7));

        neighbour.y -= 2;
        validSockets.Add(GetValidSocket(neighbour, 6));

        for (int i = 0; i < fullLayerVariationLists[(int)cell.y].list.Count(); ++i)
        {
            List<int> cellSockets = new List<int>(fullLayerVariationLists[(int)cell.y].list.Values.ToList()[i]);
            cellSockets.RemoveAt(0);
            cellSockets.RemoveAt(0);

            bool valid = true;
            for (int socketIndex = 0; socketIndex < 6; ++socketIndex)
            {
                if (validSockets[socketIndex] != -1)
                {
                    if (validSockets[socketIndex] != cellSockets[socketIndex])
                    {
                        valid = false;
                        break;
                    }
                }
            }

            if (valid)
            {
                newList.list.Add(fullLayerVariationLists[(int)cell.y].list.Keys.ToList()[i], fullLayerVariationLists[(int)cell.y].list.Values.ToList()[i]);
            }
        }

        cellsVariations[cell] = newList;
    }

    private int GetValidSocket(Vector3 neighbour, int index)
    {
        if (level.ContainsKey(neighbour))
        {
            if (cellsVariations[neighbour].list.Count > 0) return cellsVariations[neighbour].list.First().Value[index];
        }

        return -1;
    }

    //Last major attempt at alternative propagation method, isn't called by active code ********************
    #region Old Propagation

    private void OldPropagate(Vector3 centreCell)
    {
        cellsToInfluence.Clear();
        cellsToPropagate.Clear();

        startCell = centreCell;

        //enqueue - add to end
        //dequeue - return and remove first item

        Queue<Vector3> toPropagate = new Queue<Vector3>();
        HashSet<Vector3> visited = new HashSet<Vector3>();
        HashSet<Vector3> cellsToReCheck = new HashSet<Vector3>();

        cellsToReCheck.Add(centreCell);

        bool forceCheck = true;

        while (forceCheck || cellsToReCheck.Count > 0)
        {
            visited.Clear();
            toPropagate.Clear();

            HashSet<Vector3> added = new HashSet<Vector3>();

            foreach (Vector3 check in cellsToReCheck)
            {
                if (!added.Contains(check))
                {
                    added.Add(check);
                    toPropagate.Enqueue(check);
                }
                toPropagate.Enqueue(check);

                for (int direction = 0; direction <= 5; ++direction)
                {
                    Vector3 adjacentCell = GetAdjacentCells(check, direction);
                    if (!added.Contains(adjacentCell) && cellsVariations.ContainsKey(adjacentCell))
                    {
                        added.Add(adjacentCell);
                        toPropagate.Enqueue(adjacentCell);
                    }
                }
            }
            forceCheck = false;
            Dictionary<Vector3, Dictionary<int, List<int>>> toRemove = new Dictionary<Vector3, Dictionary<int, List<int>>>();

            while (toPropagate.Count > 0)
            {
                Vector3 current = toPropagate.Dequeue();
                if (visited.Contains(current))
                {
                    continue;
                }

                visited.Add(current);

                var options = cellsVariations[current].list.Keys;
                if (options.Count == 1)
                {
                    continue;
                }

                for (int direction = 0; direction <= 5; ++direction)
                {
                    Vector3 adjacentCell = GetAdjacentCells(current, direction);
                    if (!cellsToReCheck.Contains(adjacentCell))
                    {
                        continue;
                    }

                    Dictionary<int, List<int>> points = Influence(adjacentCell, current, direction);
                    Debug.Log("Influence points: " + points.Count);
                    if (points.Count > 0)
                    {
                        if (!added.Contains(adjacentCell))
                        {
                            toPropagate.Enqueue(adjacentCell);
                            added.Add(adjacentCell);
                        }

                        if (!toRemove.ContainsKey(current))
                        {
                            toRemove.Add(current, new Dictionary<int, List<int>>());
                        }

                        foreach (KeyValuePair<int, List<int>> impact in points)
                        {
                            if (!toRemove[current].ContainsKey(impact.Key)) toRemove[current].Add(impact.Key, impact.Value);
                        }
                    }
                }
            }

            HashSet<Vector3> actuallyRemoved = new HashSet<Vector3>();

            foreach (KeyValuePair<Vector3, Dictionary<int, List<int>>> kvp in toRemove)
            {
                Vector3 s = kvp.Key;
                Dictionary<int, List<int>> kills = kvp.Value;

                Dictionary<int, List<int>> current = cellsVariations[s].list;

                foreach (KeyValuePair<int, List<int>> toKill in kills)
                {
                    if (current.Contains(toKill))
                    {
                        current.Remove(toKill.Key);
                        actuallyRemoved.Add(s);
                    }
                }
            }

            cellsToReCheck.Clear();

            foreach (Vector3 item in actuallyRemoved)
            {
                cellsToReCheck.Add(item);
            }
        }
    }

    private void OldInfluence(Vector3 centreCell, Vector3 cellToCheck, int direction)
    {
        if (!collapsedCells.Contains(cellToCheck) && cellsVariations.ContainsKey(cellToCheck))
        {
            if (!propagatedCells.Contains(cellToCheck))
            {
                if (Vector3.Distance(startCell, cellToCheck) < 10)
                {
                    int startCount = cellsVariations[cellToCheck].list.Count();
                    OldInfluenceCell(centreCell, cellToCheck, direction);
                    if (startCount != cellsVariations[cellToCheck].list.Count())
                    {
                        cellsToPropagate.Add(cellToCheck);
                        propagatedCells.Remove(cellToCheck);
                    }
                    else if (!cellsToPropagate.Contains(cellToCheck)) propagatedCells.Add(cellToCheck);
                }
            }
        }
    }

    private void OldInfluenceCell(Vector3 centreCell, Vector3 neighbourCell, int direction)
    {
        if (!cellsVariations.ContainsKey(neighbourCell)) return;

        if (collapsedCells.Contains(centreCell))
        {
            int modulePrototypeID = 0;
            if (level[centreCell] != null) modulePrototypeID = level[centreCell].modulePrototypeID;

            int newDirection = direction;
            int constraint = cellsVariations[centreCell].list.ElementAt(0).Value[newDirection + 2];
            int facingDirection = GetOppositeDirection(newDirection);

            foreach (KeyValuePair<int, List<int>> kvp in new Dictionary<int, List<int>>(cellsVariations[neighbourCell].list))
            {
                int neighbourID = kvp.Value[0];
                bool sameModule = modulePrototypeID == neighbourID ? true : false;

                if (sameModule)
                {
                    if (modulePrototypeID == kvp.Value[0])
                    {
                        if (FindModulePrototype(kvp.Key).ExcludeSelfInDirection)
                        {
                            cellsVariations[neighbourCell].list.Remove(kvp.Key);
                            continue;
                        }
                    }
                }

                if (kvp.Value[facingDirection + 2] != constraint) cellsVariations[neighbourCell].list.Remove(kvp.Key);
            }
        }
        else
        {
            int newDirection = direction;
            int facingDirection = GetOppositeDirection(newDirection);

            List<int> constraints = new List<int>();

            List<int> ids = new List<int>();

            if (cellsVariations[centreCell].list.Keys.Count() == 0) constraints.Add(0);

            foreach (KeyValuePair<int, List<int>> kvp in cellsVariations[centreCell].list)
            {
                int value = kvp.Value[newDirection + 2];
                if (!constraints.Contains(value)) constraints.Add(value);
                ids.Add(kvp.Value[0]);
            }

            foreach (KeyValuePair<int, List<int>> kvp in new Dictionary<int, List<int>>(cellsVariations[neighbourCell].list))
            {
                if (!constraints.Contains(kvp.Value[facingDirection + 2])) cellsVariations[neighbourCell].list.Remove(kvp.Key);
                else
                {
                    int modulePrototypeID = kvp.Value[0];
                    if (ids.Contains(modulePrototypeID))
                    {
                        if (FindModulePrototype(modulePrototypeID).ExcludeSelfInDirection)
                        {
                            cellsVariations[neighbourCell].list.Remove(kvp.Key);
                        }
                    }
                }
            }
        }
    }

    #endregion
    //******************************************************************************************************

    private Vector3 GetAdjacentCells(Vector3 centreCell, int direction)
    {
        Vector3 cell = centreCell;

        if (direction == 0) cell.x += 1;
        else if (direction == 1) cell.z += 1;
        else if (direction == 2) cell.x -= 1;
        else if (direction == 3) cell.z -= 1;
        else if (direction == 4) cell.y += 1;
        else if (direction == 5) cell.y -= 1;

        return cell;
    }

    private ModulePrototype FindModulePrototype(int id)
    {
        if (!fullCellVariationList.list.ContainsKey(id)) return emptyPrototype;

        int prototypeID = fullCellVariationList.list[id][0];
        return modulePrototypes.Find(prototype => prototype.modulePrototypeID == prototypeID);
    }

    private int GetWeightedRandomCellIndex(List<int> cellIDs)
    {
        List<float> weights = new List<float>();

        foreach (int id in cellIDs)
        {
            if (id == 0 || !fullCellVariationList.list.ContainsKey(id)) continue;

            weights.Add(FindModulePrototype(id).Weight);
        }

        float randomWeightValue = UnityEngine.Random.Range(0.0f, weights.Sum());
        int counter = 0;
        float currentWeightValue = 0;
        int randomIndex = 0;

        foreach (float weight in weights)
        {
            currentWeightValue += weight;

            if (currentWeightValue > randomWeightValue)
            {
                randomIndex = counter;
                break;
            }

            counter++;
        }

        return randomIndex;
    }

    private int GetOppositeDirection(int direction)
    {
        if (direction == 0) return 2;
        else if (direction == 1) return 3;
        else if (direction == 2) return 0;
        else if (direction == 3) return 1;
        else if (direction == 4) return 5;
        else if (direction == 5) return 4;

        return -1;
    }

    private void OnDrawGizmos()
    {
        if (showConstraints)
        {
            foreach (Vector3 key in level.Keys)
            {
                if (cellsVariations[key].list.Count == 0) continue;

                Vector3 cellPosition = key * 3;
                if ((Camera.current.transform.position - cellPosition).magnitude < viewableConstraintDistance)
                {
                    GUI.color = Color.white;
                    ModulePrototype modulePrototype = level[key];

                    int rotationIndex = cellsVariations[key].list.ElementAt(0).Value[1];

                    string socketForwards;
                    string socketLeft;
                    string socketBackwards;
                    string socketRight;
                    string rotationIndicator;

                    if (rotationIndex == 1)
                    {
                        socketForwards = modulePrototype.RightSocket.ToString();
                        socketLeft = modulePrototype.ForwardsSocket.ToString();
                        socketBackwards = modulePrototype.LeftSocket.ToString();
                        socketRight = modulePrototype.BackwardsSocket.ToString();
                        rotationIndicator = "b";
                    }
                    else if (rotationIndex == 2)
                    {
                        socketForwards = modulePrototype.BackwardsSocket.ToString();
                        socketLeft = modulePrototype.RightSocket.ToString();
                        socketBackwards = modulePrototype.ForwardsSocket.ToString();
                        socketRight = modulePrototype.LeftSocket.ToString();
                        rotationIndicator = "c";
                    }
                    else if (rotationIndex == 3)
                    {
                        socketForwards = modulePrototype.LeftSocket.ToString();
                        socketLeft = modulePrototype.BackwardsSocket.ToString();
                        socketBackwards = modulePrototype.RightSocket.ToString();
                        socketRight = modulePrototype.ForwardsSocket.ToString();
                        rotationIndicator = "d";
                    }
                    else
                    {
                        socketForwards = modulePrototype.ForwardsSocket.ToString();
                        socketLeft = modulePrototype.LeftSocket.ToString();
                        socketBackwards = modulePrototype.BackwardsSocket.ToString();
                        socketRight = modulePrototype.RightSocket.ToString();
                        rotationIndicator = "a";
                    }

                    cellPosition.x += 1;
                    Handles.Label(cellPosition, socketForwards);

                    cellPosition.x -= 2;
                    Handles.Label(cellPosition, socketBackwards);

                    cellPosition.x += 1;
                    cellPosition.z += 1;
                    Handles.Label(cellPosition, socketLeft);

                    cellPosition.z -= 2;
                    Handles.Label(cellPosition, socketRight);

                    GUI.color = Color.yellow;

                    cellPosition.z += 1;
                    cellPosition.y += 1;
                    Handles.Label(cellPosition, modulePrototype.UpwardsSocket.ToString());

                    cellPosition.y -= 2;
                    Handles.Label(cellPosition, modulePrototype.DownwardsSocket.ToString());

                    GUI.color = Color.blue;

                    Handles.Label(key * 3, modulePrototype.modulePrototypeID.ToString() + rotationIndicator);
                }
            }
        }

        if (showGrid)
        {
            Color gridColor = Color.green;
            gridColor.a = 0.4f;
            Handles.color = gridColor;

            Vector3 p1 = Vector3.zero;
            Vector3 p2 = Vector3.zero;

            for (int x = 0; x < levelDimensions.x + 1; ++x)
            {
                for (int y = 0; y < levelDimensions.y + 1; ++y)
                {
                    p1.x = x * 3;
                    p1.y = y * 3;
                    p1.z = 0;
                    p1 += Utility.MakeVector3(-1.5f);

                    p2.x = x * 3;
                    p2.y = y * 3;
                    p2.z = levelDimensions.z * 3;
                    p2 += Utility.MakeVector3(-1.5f);

                    Handles.DrawLine(p1, p2);

                    p1.x = 0;
                    p1.y = y * 3;
                    p1.z = x * 3;
                    p1 += Utility.MakeVector3(-1.5f);

                    p2.x = levelDimensions.x * 3;
                    p2.y = y * 3;
                    p2.z = x * 3;
                    p2 += Utility.MakeVector3(-1.5f);

                    Handles.DrawLine(p1, p2);
                }

                for (int z = 0; z < levelDimensions.z + 1; ++z)
                {
                    p1.x = x * 3;
                    p1.y = 0;
                    p1.z = z * 3;
                    p1 += Utility.MakeVector3(-1.5f);

                    p2.x = x * 3;
                    p2.y = levelDimensions.y * 3;
                    p2.z = z * 3;
                    p2 += Utility.MakeVector3(-1.5f);

                    Handles.DrawLine(p1, p2);
                }
            }
        }
    }
}
