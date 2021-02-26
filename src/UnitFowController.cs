using DwarfHeim;
using DwarfHeim.World;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     A unit's fog of war controller.
///     Unit prefab (where this script is) should have a child with sphere collider (trigger).
///     Child should use layer "SpatialGrid".
///     This is to collide with <see cref="SpatialGridSquareController"/>.
/// </summary>
/// <remarks>
///     Sindre Haugland Paulshus
/// </remarks>
[System.Serializable]
public class UnitFowController : MonoBehaviour
{
    public int fowCalcArchetypeID = 0;

    public int gridIndex;

    //List of indexes which comprises the unit's view
    [HideInInspector] public List<int> myView;
    [HideInInspector] public List<int> prevView;

    private FowManager fowMan;

    //For spatial indexing
    public SphereCollider mySpatialCollider;
    public List<SpatialGridSquareController> mySquares;

    //list spot for DDA jobs
    public int myQueueSpot;

    // Start is called before the first frame update
    void Start()
    {
        //find index and update my fow
        gridIndex = FowManager.GetGridIndex(this.transform.position);
        fowMan = FowManager.GetInstance();
        fowMan.unitFows.Add(this);

        //init spatial indexing
        mySquares = new List<SpatialGridSquareController>();
        int viewDist = fowMan.fowCalcArchetypes[fowCalcArchetypeID].viewDist;
        mySpatialCollider.radius = viewDist * 2; //worldsize = 2

        //updateMyFOW(); //Deprecated
        fowMan.UpdateFow(this);
    }

    // Update is called once per frame
    void Update()
    {
        CheckUpdateFow();
    }

    /// <summary>
    ///     Checks if this unit needs to update.
    ///     Called by update loop.
    /// </summary>
    public void CheckUpdateFow()
    {
        int newGridIndex = FowManager.GetGridIndex(this.transform.position);
        if (newGridIndex != gridIndex)
        {
            gridIndex = newGridIndex;
            fowMan.UpdateFow(this);
        }
    }

    //DEPRECATED
    public void updateMyFOW()
    {
        //print("Unit: " + gameObject.name + " updating fow!");
        prevView = myView;
        //myView = fowMan.fowCalcs[ddaArchetypeID].GetFOW(gridIndex);
    }
}