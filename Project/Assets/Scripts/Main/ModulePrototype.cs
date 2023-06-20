using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class ModulePrototype : MonoBehaviour
{
    [Header("Base")]
    public int modulePrototypeID;
    public int prototypeLayer;
    [Range(0, 1)]
    public float Weight = 1.0f;
    public bool IsInterior = false;

    [Header("\nExclusions")]
    public bool ExcludeSelfInDirection = false;
    public List<int> ExcludeDirections;

    [Header("\nHorizontalSockets")]
    public int ForwardsSocket;
    public int LeftSocket;
    public int BackwardsSocket;
    public int RightSocket;

    [Header("VerticalSockets")]
    public int UpwardsSocket;
    public int DownwardsSocket;

    public List<int> GetHorizontalConstraints()
    {
        List<int> list = new List<int>();

        list.Add(ForwardsSocket);
        list.Add(LeftSocket);
        list.Add(BackwardsSocket);
        list.Add(RightSocket);

        return list;
    }

    public List<int> GetSockets()
    {
        List<int> list = new List<int>();

        list.Add(ForwardsSocket);
        list.Add(LeftSocket);
        list.Add(BackwardsSocket);
        list.Add(RightSocket);
        list.Add(UpwardsSocket);
        list.Add(DownwardsSocket);

        return list;
    }
}
