using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
///     Component for Spatial Indexing. 
///     Prefab should have box collider, rigidbody and be on a unique layer "SpatialGrid".
///     Spawned by <see cref="FowManager"/>. 
///     Collides with <see cref="UnitFowController"/>.
/// </summary>
/// <remarks>
///     Sindre Haugland Paulshus
/// </remarks>
[System.Serializable]
public class SpatialGridSquareController : MonoBehaviour
{
    public int x;
    public int y;
    public int index;

    /// <summary>
    ///     The units currently in this square.
    /// </summary>
    public List<UnitFowController> myUnits;

    // Start is called before the first frame update
    void Start()
    {
        myUnits = new List<UnitFowController>();
    }

    //Collider methods
    private void OnTriggerEnter(Collider other)
    {
        UnitFowController unit = other.transform.parent.GetComponent<UnitFowController>(); //might have to change
        if (unit != null)
        {
            unit.mySquares.Add(this);
            myUnits.Add(unit);
        }
    }
    private void OnTriggerExit(Collider other)
    {
        UnitFowController unit = other.transform.parent.GetComponent<UnitFowController>(); //might have to change
        if (unit != null)
        {
            unit.mySquares.Remove(this);
            myUnits.Remove(unit);
        }
    }
}