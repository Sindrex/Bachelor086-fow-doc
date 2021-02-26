using DwarfHeim;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
///     The Fog of War Manager! 
///     Controls when, who and how the FOW updates.
/// </summary>
/// <remarks>
///     Sindre Haugland Paulshus
/// </remarks>
public class FowManager : MonoBehaviour
{
    private static FowManager instance; //Singleton

    public List<UnitFowController> unitFows;
    private int fowArrLen;
    private Color32[] colorArray;
    public MeshRenderer owFowMap;

    //Spatial grid
    /// <summary>
    ///     Needs to be a divisor of WorldWidth and WorldHeight.
    /// </summary>
    public int spatialGridWidth = 4;
    public int spatialGridHeight = 4;
    private List<SpatialGridSquareController> spatialGrid;
    private int spatialDivisorX;
    private int spatialDivisorY;
    public GameObject spatialGridSquarePrefab;

    //world
    private int worldWidth;
    private int worldHeight;

    //update timer
    public float minUpdateTimer = 0.1f;
    private float timer;
    private bool fowNeedsUpdate = false;

    //[SerializeField]
    private HashSet<UnitFowController> unitSet;

    private Object lockObj = new Object(); //DEPRECATED

    //Fow Calc archtypes. Currently set through inspector. Could be read from file
    public int[] viewDists;
    public int[] seeThroughs;
    public FowCalcArchetype[] fowCalcArchetypes;

    //Unity jobs batch size
    public int batchSize = 4;

    private void Awake()
    {
        //Singleton
        if (instance != null && instance != this)
        {
            Destroy(this.gameObject);
        }
        else
        {
            fowArrLen = DwarfHeimManager.Instance.OWGrid.WorldWidth * DwarfHeimManager.Instance.OWGrid.WorldHeight;

            colorArray = new Color32[fowArrLen];
            unitFows = new List<UnitFowController>();
            unitSet = new HashSet<UnitFowController>();

            instance = this;
        }

        //copy width and height
        worldWidth = DwarfHeimManager.Instance.OWGrid.WorldWidth;
        worldHeight = DwarfHeimManager.Instance.OWGrid.WorldHeight;

        //init spatial index grid
        spatialDivisorX = worldWidth / spatialGridWidth;
        spatialDivisorY = worldHeight / spatialGridHeight;
        spatialGrid = new List<SpatialGridSquareController>();
        Vector3 offset = new Vector3(spatialDivisorX, 0, spatialDivisorY);
        int index = 0;
        for(int x = 0; x < spatialGridWidth; x++)
        {
            for(int y = 0; y < spatialGridWidth; y++)
            {
                GameObject prefab = Instantiate(spatialGridSquarePrefab, this.transform);
                prefab.transform.position = new Vector3(x * spatialDivisorX * 2, 0, y * spatialDivisorY * 2) + offset;
                prefab.GetComponent<BoxCollider>().size = new Vector3(spatialDivisorX * 2, 0, spatialDivisorY * 2);
                prefab.name = "SpatialViz_" + x + "_" + y;

                SpatialGridSquareController square = prefab.GetComponent<SpatialGridSquareController>();
                square.x = x;
                square.y = y;
                square.index = index;
                spatialGrid.Add(square);
                index++;
            }
        }

        //init DDA Archtypes
        fowCalcArchetypes = new FowCalcArchetype[viewDists.Length];
        for (int i = 0; i < viewDists.Length; i++)
        {
            fowCalcArchetypes[i] = new FowCalcArchetype(worldWidth, worldHeight, viewDists[i], seeThroughs[i]);
        }
    }

    //get singleton
    public static FowManager GetInstance()
    {
        return instance;
    }

    //generic update loop
    private void Update()
    {
        CheckUpdateFow();
    }

    /// <summary>
    ///     Checks if fow should be updated. Called by update loop. Uses Time.Deltatime for timer.
    /// </summary>
    public void CheckUpdateFow()
    {
        if (fowNeedsUpdate)
        {
            timer += Time.deltaTime;
            if (timer > minUpdateTimer)
            {
                timer = 0;
                SetFowArrJobs();
                CreatePNG();
                fowNeedsUpdate = false;
                unitSet = new HashSet<UnitFowController>();
            }
        }
    }

    /// <summary>
    ///     Sends signal that the FOW needs to be updated. 
    ///     Adds unit that needs to update into unitlist.
    ///     Called by <see cref="UnitFowController"/>.
    /// </summary>
    public void UpdateFow(UnitFowController sendUnit)
    {
        unitSet.Add(sendUnit);
        fowNeedsUpdate = true;
    }

    /// <summary>
    ///     Calculates and sets the FOW using Unity Jobs with <see cref="FowCalcArchetype"/>.
    ///     Called by CheckUpdateFow(), which is called by update loop.
    /// </summary>
    private void SetFowArrJobs()
    {
        FowCalcArchetype.BATCH_SIZE = batchSize;

        List<List<int>> startIndexListsPerArchtype = new List<List<int>>();
        foreach (FowCalcArchetype type in fowCalcArchetypes)
        {
            startIndexListsPerArchtype.Add(new List<int>()); //initialize
        }

        foreach (UnitFowController unit in this.unitSet) //Update the ones that moved, also: could be parallelized?
        {
            unit.myQueueSpot = startIndexListsPerArchtype[unit.fowCalcArchetypeID].Count; //save spot
            startIndexListsPerArchtype[unit.fowCalcArchetypeID].Add(unit.gridIndex); //add unit's spot to startIndex list
        }

        //calculate units' view
        int[][][] trueReturn = GetAllIndexArrays(startIndexListsPerArchtype);

        //set units' view
        SetUnitsNewView(trueReturn);

        //Include units in same square(s) for update
        HashSet<UnitFowController> unitSet = new HashSet<UnitFowController>();
        AddSpatialUnits(unitSet);

        //Set color array
        SetColorArr(unitSet);
    }

    /// <summary>
    ///     gets all new view indexes for the units. Arranges them in a 3d results array (DDA archetype, unit, indexes).
    /// </summary>
    /// <param name="startIndexListsPerArchetype"> 
    ///     List of Lists containing startindexes of units of various DDA Archetypes 
    /// </param>
    /// <returns> 
    ///     3d int results (jagged) array (DDA archetype, unit, indexes) 
    /// </returns>
    private int[][][] GetAllIndexArrays(List<List<int>> startIndexListsPerArchetype)
    {
        int[][][] trueReturn = new int[fowCalcArchetypes.Length][][];
        for (int i = 0; i < fowCalcArchetypes.Length; i++)
        {
            int[] archtypeReturn = fowCalcArchetypes[i].RunJobs(startIndexListsPerArchetype[i]); //1 return array per archtype. Needs to be split up
            int perJobLen = fowCalcArchetypes[i].perJobAlloc;

            trueReturn[i] = new int[archtypeReturn.Length][];
            int index = 0;
            for (int j = 0; j < archtypeReturn.Length; j += perJobLen)
            {
                trueReturn[i][index] = new int[perJobLen];
                for (int k = 0; k < perJobLen; k++)
                {
                    trueReturn[i][index][k] = archtypeReturn[j + k]; //copy results
                }
                index++;
            }
        }
        return trueReturn;
    }

    /// <summary>
    ///     sets the units that needs to update's view from a results array.
    /// </summary>
    /// <param name="trueReturn">
    ///     3d array gotten from GetAllIndexArrays()
    /// </param>
    private void SetUnitsNewView(int[][][] trueReturn)
    {
        Parallel.ForEach(unitSet, unit =>
        {
            int[] myArr = trueReturn[unit.fowCalcArchetypeID][unit.myQueueSpot];
            unit.prevView = unit.myView;
            List<int> newView = new List<int>();
            foreach (int i in myArr)
            {
                if (i > 0) //0 = null
                {
                    newView.Add(i);
                }
            }
            unit.myView = newView;
        });
    }

    /// <summary>
    ///     adds units into unitSet that are in the same spatial grid square as the units that need to update.
    /// </summary>
    /// <param name="unitSet">
    ///     HashSet containing all units that sent request to update their fow
    /// </param>
    private void AddSpatialUnits(HashSet<UnitFowController> unitSet)
    {
        byte[] squareIndexArr = new byte[spatialGrid.Count];
        foreach (UnitFowController unit in this.unitSet)
        {
            //Only refresh the ones that could be seeing the same square(s)
            foreach (SpatialGridSquareController square in unit.mySquares)
            {
                if (squareIndexArr[square.index] == 0) //Only check squares we havent checked before
                {
                    squareIndexArr[square.index] = 1;
                    foreach (UnitFowController squareUnit in square.myUnits)
                    {
                        unitSet.Add(squareUnit);
                    }
                }
            }
        }
    }

    /// <summary>
    ///     Sets the color array according to units' view
    /// </summary>
    /// <param name="unitSet">
    ///     HashSet containing all units that needs to update their fow.
    /// </param>
    private void SetColorArr(HashSet<UnitFowController> unitSet)
    {
        Parallel.ForEach(unitSet, unit =>
        {
            foreach (int i in unit.prevView)
            {
                //black
                colorArray[i] = new Color32(0x00, 0x00, 0x00, 0xFF); //Color32[] is thread safe
            }
        });
        Parallel.ForEach(unitSet, unit =>
        {
            foreach (int i in unit.myView)
            {
                //white
                colorArray[i] = new Color32(0xFF, 0xFF, 0xFF, 0xFF); //Color32[] is thread safe
            }
        });
    }

    /// <summary>
    ///     creates a texture of the created png color array and sets the OWFOWMap to that texture
    /// </summary>
    private void CreatePNG()
    {
        //set texture
        int width = worldWidth;
        int height = worldHeight;
        Texture2D tex = new Texture2D(width, height);

        tex.SetPixels32(colorArray);
        tex.Apply();
        owFowMap.material.mainTexture = tex;
    }

    ///DEPRECATED
    private void setFowArr()
    {
        byte[] squareIndexArr = new byte[spatialGrid.Count];

        HashSet<UnitFowController> unitSet = new HashSet<UnitFowController>();

        IReadOnlyList<UnitFowController> newUnitList = this.unitSet.ToList().AsReadOnly();

        Parallel.ForEach(newUnitList, unit =>
        {
            unit.updateMyFOW(); //update the ones that moved

            //Only refresh the ones that could be seeing the same square(s)
            foreach (SpatialGridSquareController square in unit.mySquares)
            {
                lock (lockObj)
                {
                    if (squareIndexArr[square.index] == 0)
                    {
                        squareIndexArr[square.index] = 1;
                        foreach (UnitFowController squareUnit in square.myUnits)
                        {
                            unitSet.Add(squareUnit);
                        }
                    }
                }
            }
        });        
        
        foreach (UnitFowController unit in unitSet)
        {
            foreach (int i in unit.prevView)
            {
                colorArray[i] = new Color32(0x00, 0x00, 0x00, 0xFF); //This could be a job? Maybe not
            }
        }
        foreach (UnitFowController unit in unitSet)
        {
            foreach (int i in unit.myView)
            {
                colorArray[i] = new Color32(0xFF, 0xFF, 0xFF, 0xFF); //this could be a job? Maybe not
            }
        }
    }

    //Static help method to calculate a unit's grid index by their vector3
    public static int GetGridIndex(Vector3 worldPos)
    {
        int width = DwarfHeimManager.Instance.OWGrid.WorldWidth;
        int height = DwarfHeimManager.Instance.OWGrid.WorldHeight;
        int y = (int)worldPos.z / 2;
        int x = (int)worldPos.x / 2;
        if (x > width)
        {
            x = width;
        }
        else if (x < 0)
        {
            x = 0;
        }
        if (y > height)
        {
            y = height;
        }
        else if (y < 0)
        {
            y = 0;
        }
        return y * width + x;
    }
}