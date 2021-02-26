using System.Collections;
using System.Collections.Generic;
using Unity.Jobs;
using Unity.Collections;
using UnityEngine;
using DwarfHeim;
using DwarfHeim.World;
using Unity.Burst;
using System;
using System.Diagnostics;
using UnityEngine;

/// <summary>
///     An archtype of the Fow calculator with one view distance and a count of max see through resources/walls.
/// </summary>
/// <remarks>
///     Sindre Haugland Paulshus
/// </remarks>
public class FowCalcArchetype
{
    public static readonly float TOL = 0.25f;
    public static int BATCH_SIZE = 8;
    public static int PER_JOB_ARRAY_PADDING = 50;
    public static int WORLD_GRID_SPATIAL_INDEXING_PADDING = 10;

    //Holds DDA-related vars, private vars that wont change
    private readonly int width;
    private readonly int height;
    private readonly int tileCount;
    public readonly int viewDist; //public for access from UnitFOW
    private readonly int maxSeeThrough;

    //hard copy, so we dont need to access Worldgrid all the time
    private readonly WorldGrid.TerrainType wall;
    private readonly WorldGrid.TileOccupation resource;
    private readonly WorldGrid.TileOccupation building;

    //Edge deltas
    private readonly int[] staticX;
    private readonly int[] staticY;

    /// <summary>
    ///     The amount of space needed to store a unit's maximum view.
    /// </summary>
    public readonly int perJobAlloc;

    public FowCalcArchetype(int width, int height, int viewDist)
    {
        this.width = width;
        this.height = height;
        this.viewDist = viewDist;
        tileCount = width * height;
        maxSeeThrough = 0;

        //pre-calculate deltas for the given viewdist
        (staticX, staticY) = GetEdgeDeltas(viewDist);
        UnityEngine.Debug.Log(">>> New Archetype");
        for (int i = 0; i < staticX.Length; i++)
        {
            UnityEngine.Debug.Log("x/y: " + staticX[i] + "/" + staticY[i]);
        }

        //copy
        wall = WorldGrid.TerrainType.Wall;
        resource = WorldGrid.TileOccupation.Resource;
        building = WorldGrid.TileOccupation.Building;

        int padding = PER_JOB_ARRAY_PADDING;
        perJobAlloc = (int)(3.14f * viewDist * viewDist) + padding; //pi*r^2
    }
    public FowCalcArchetype(int width, int height, int viewDist, int maxSeeThrough) : this(width, height, viewDist)
    {
        this.maxSeeThrough = maxSeeThrough;
    }

    /// <summary>
    ///     Calculates the view indexes for all units of a given DDA Archetype
    /// </summary>
    /// <param name="startIndexesList">
    ///     A list of units' start indexes
    /// </param>
    /// <returns>
    ///     One array of ints for all view indexes. 
    ///     Each unit has <see cref="perJobAlloc"/> amount of space in the array.
    ///     Same order as the input list.
    /// </returns>
    public int[] RunJobs(List<int> startIndexesList)
    {
        int worldArrLen = width * height;
        NativeArray<byte> occupation = new NativeArray<byte>(worldArrLen, Allocator.TempJob); 
        NativeArray<byte> terrain = new NativeArray<byte>(worldArrLen, Allocator.TempJob);
        WorldGrid OWGrid = DwarfHeimManager.Instance.OWGrid;

        byte[][] indexBytes = new byte[width][];
        for(int i = 0; i < width; i++) //init indexBytes
        {
            indexBytes[i] = new byte[height];
        }
        int spatialPadding = WORLD_GRID_SPATIAL_INDEXING_PADDING;
        foreach(int startIndex in startIndexesList)
        {            
            int x0 = MakeX(startIndex);
            int y0 = MakeY(startIndex);
            int minX = FowCalcJob.NewX(x0 - viewDist - spatialPadding, width);
            int minY = FowCalcJob.NewY(y0 - viewDist - spatialPadding, height);
            int maxX = FowCalcJob.NewX(x0 + viewDist + spatialPadding, width);
            int maxY = FowCalcJob.NewY(y0 + viewDist + spatialPadding, height);
            for (int x1 = minX; x1 < maxX; x1++) //Spatial indexing (square)
            {
                for (int y1 = minY; y1 < maxY; y1++)
                {
                    if(indexBytes[x1][y1] == 0)
                    {
                        int index = MakeIndex(x1, y1);
                        terrain[index] = (byte)OWGrid._terrain[index];
                        occupation[index] = (byte)OWGrid._occupation[index];
                        indexBytes[x1][y1] = 1;
                    }
                }
            }
        }

        NativeArray<int> natStaticX = new NativeArray<int>(staticX, Allocator.TempJob);
        NativeArray<int> natStaticY = new NativeArray<int>(staticY, Allocator.TempJob);
        NativeArray<int> startIndexes = new NativeArray<int>(startIndexesList.ToArray(), Allocator.TempJob); //startindexes of the units of this archetype
        NativeArray<int> results = new NativeArray<int>(perJobAlloc * startIndexes.Length, Allocator.TempJob); //1 concatinated array for all!

        FowCalcJob job = new FowCalcJob
        {
            startIndexes = startIndexes,

            width = width,
            height = height,
            tileCount = tileCount,
            viewDist = viewDist,
            maxSeeThrough = maxSeeThrough,

            wall = (byte)wall,
            resource = (byte)resource,
            building = (byte)building,

            _terrain = terrain,
            _occupation = occupation,
            staticDeltaX = natStaticX,
            staticDeltaY = natStaticY,
            results = results,

            perJobAlloc = perJobAlloc
        };

        JobHandle handle = job.Schedule(startIndexes.Length, BATCH_SIZE);
        handle.Complete(); //wait

        int[] returnArr = results.ToArray();

        occupation.Dispose();
        terrain.Dispose();
        natStaticX.Dispose();
        natStaticY.Dispose();
        startIndexes.Dispose();
        results.Dispose();

        return returnArr;
    }

    /// <summary>
    ///     Calculates the archetype's edge deltas (where the edge index is in relation to startindex).
    ///     Returns two arrays: x values and y values.
    /// </summary>
    private (int[] x, int[] y) GetEdgeDeltas(int viewDist)
    {
        //(1/8) * (2*pi*r) -> 1/4 * 3.14 * r -> 0.785 * r
        int octantEst = viewDist;
        int[] edgeArrayX = new int[octantEst];
        int[] edgeArrayY = new int[octantEst];

        float viewDistsq = ((viewDist + TOL) * (viewDist + TOL));

        //up right octant
        int dy = viewDist;
        int dx = 0;
        int index = 0;
        while (dy >= dx)
        {
            if (dx * dx + dy * dy <= viewDistsq) //inside circle
            {
                edgeArrayX[index] = dx;
                edgeArrayY[index] = dy;
                dx++;
                index++;
            }
            else
            {
                dy--;
            }

        }
        //UnityEngine.Debug.Log(">>>NEW ARCHETYPE>>>");
        int[] newEdgeArrayX = new int[index * 8];
        int[] newEdgeArrayY = new int[index * 8];
        for (int i = 0; i < index; i++)
        {
            newEdgeArrayX[i] = edgeArrayX[i];
            newEdgeArrayY[i] = edgeArrayY[i];
            //UnityEngine.Debug.Log("x/y: " + edgeArrayX[i] + "/" + edgeArrayY[i]);
        }
        
        #region other octants
        int octantCount = index;
        //right top oct
        for (int j = 0; j < octantCount; j++)
        {
            dx = newEdgeArrayX[j];
            dy = newEdgeArrayY[j];
            newEdgeArrayX[index] = dy;
            newEdgeArrayY[index] = dx;
            index++;
        }
        //left top oct
        for (int j = 0; j < octantCount; j++)
        {
            dx = newEdgeArrayX[j];
            dy = newEdgeArrayY[j];
            newEdgeArrayX[index] = -dx;
            newEdgeArrayY[index] = dy;
            index++;
        }
        //top left oct
        for (int j = 0; j < octantCount; j++)
        {
            dx = newEdgeArrayX[j];
            dy = newEdgeArrayY[j];
            newEdgeArrayX[index] = -dy;
            newEdgeArrayY[index] = dx;
            index++;
        }
        //bot left oct
        for (int j = 0; j < octantCount; j++)
        {
            dx = newEdgeArrayX[j];
            dy = newEdgeArrayY[j];
            newEdgeArrayX[index] = -dy;
            newEdgeArrayY[index] = -dx;
            index++;
        }
        //bot right oct
        for (int j = 0; j < octantCount; j++)
        {
            dx = newEdgeArrayX[j];
            dy = newEdgeArrayY[j];
            newEdgeArrayX[index] = dy;
            newEdgeArrayY[index] = -dx;
            index++;
        }
        //left bot oct
        for (int j = 0; j < octantCount; j++)
        {
            dx = newEdgeArrayX[j];
            dy = newEdgeArrayY[j];
            newEdgeArrayX[index] = -dx;
            newEdgeArrayY[index] = -dy;
            index++;
        }
        //right bot oct
        for (int j = 0; j < octantCount; j++)
        {
            dx = newEdgeArrayX[j];
            dy = newEdgeArrayY[j];
            newEdgeArrayX[index] = dx;
            newEdgeArrayY[index] = -dy;
            index++;
        }
        #endregion
        return (newEdgeArrayX, newEdgeArrayY);
    }

    //Helper methods
    private int MakeX(int index) //spaceX :D
    {
        return index % width;
    }
    private int MakeY(int index)
    {
        return index / width;
    }
    private int MakeIndex(int x, int y)
    {
        return y * width + x;
    }

    /// <summary>
    ///     1 parallelized-foreach job per archetype of the fow calculator.
    ///     Uses Unity jobs.
    /// </summary>
    [BurstCompile]
    struct FowCalcJob : IJobParallelFor //parallellized DDA-algorithms
    {
        //World
        public int width;
        public int height;

        //worldgrid
        [ReadOnly] [NativeDisableParallelForRestriction]
        public NativeArray<byte> _terrain; //0 = nothing
        [ReadOnly] [NativeDisableParallelForRestriction]
        public NativeArray<byte> _occupation;

        public byte wall;
        public byte resource;
        public byte building;

        //per unique unit
        [ReadOnly]
        public NativeArray<int> startIndexes; //important!

        //Per unique DDA config
        public int tileCount;
        public int viewDist;
        public int maxSeeThrough;

        //precalculated outside
        [ReadOnly] [NativeDisableParallelForRestriction]
        public NativeArray<int> staticDeltaX;
        [ReadOnly] [NativeDisableParallelForRestriction]
        public NativeArray<int> staticDeltaY;

        [WriteOnly] [NativeDisableParallelForRestriction]
        public NativeArray<int> results; //concatinated array (ie index 0 has 0 to x, next x to y etc). Needs to be made into 1 list after job is done (on main thread)

        public int perJobAlloc;

        //can only array access index
        public void Execute(int i) //edge[i]
        {
            NativeArray<int> myView = GetFOW(startIndexes[i]);
            for(int j = 0; j < myView.Length; j++)
            {
                results[i * perJobAlloc + j] = myView[j];
            }
        }

        //>>> DDA part

        /// <summary>
        ///     Calculates the FOW for a given startindex.
        /// </summary>
        /// <param name="startIndex">
        ///     The unit's start index in the worldgrid.
        /// </param>
        /// <returns>
        ///     NativeArray<int> with the indexes for the fow. May have padding with 0s at the end.
        /// </returns>
        private NativeArray<int> GetFOW(int startIndex)
        {
            //1. get edge tiles/indexes and start index (gotta know start and stop)
            //2. Find line between start and edge pieces with DDA, stop if wall
            //3. Profit!

            NativeArray<byte> viewIndexBytes = new NativeArray<byte>(tileCount, Allocator.Temp);

            //make lines!
            int x0 = MakeX(startIndex);
            int y0 = MakeY(startIndex);

            for(int i = 0; i < staticDeltaX.Length; i++) //use precalculated values
            {
                //find edge
                int nx = x0 + staticDeltaX[i];
                int ny = y0 + staticDeltaY[i];
                nx = NewX(nx);
                ny = NewY(ny);

                //make line immediately
                int x1 = nx;
                int y1 = ny;

                float dx = x1 - x0;
                float dy = y1 - y0;

                float x = x0;
                float y = y0;

                float a = 0; //slope
                if (dx != 0)
                {
                    a = dy / dx;
                    a = Abs(a);
                }

                IterateMakeLine(dx, dy, x, y, x1, y1, a, viewIndexBytes);

                //add inner index too
                dx = nx - x0;
                dy = ny - y0;
                nx = DecrementX((int)dx, (int)dy, nx);
                ny = DecrementY((int)dx, (int)dy, ny);

                x1 = nx;
                y1 = ny;

                dx = x1 - x0;
                dy = y1 - y0;

                x = x0;
                y = y0;

                a = 0; //slope
                if (dx != 0)
                {
                    a = dy / dx;
                    a = Abs(a);
                }

                IterateMakeLine(dx, dy, x, y, x1, y1, a, viewIndexBytes);
            }
            return ByteArrayToIndexes(viewIndexBytes, x0, y0, perJobAlloc);
        }

        //Converts a byte array to array of indexes
        private NativeArray<int> ByteArrayToIndexes(NativeArray<byte> arr, int x0, int y0, int length)
        {
            NativeArray<int> myList = new NativeArray<int>(length, Allocator.Temp);
            int minX = NewX(x0 - viewDist);
            int minY = NewY(y0 - viewDist);
            int maxX = NewX(x0 + viewDist);
            int maxY = NewY(y0 + viewDist);

            int index = 0;
            for (int x1 = minX; x1 < maxX; x1++) //Spatial indexing (square)
            {
                for (int y1 = minY; y1 < maxY; y1++)
                {
                    int edgeIndex = MakeIndex(x1, y1);
                    if (arr[edgeIndex] == 1)
                    {
                        myList[index] = edgeIndex;
                        index++;
                    }
                }
            }
            return myList;
        }

        //help method to make lines and set byte[index] to 1
        private void IterateMakeLine(float dx, float dy, float x, float y, float x1, float y1, float a, NativeArray<byte> viewIndexes)
        {
            int seenThroughCount = 0;

            //iterate
            if (dx == 0) //straight up or down
            {
                while (y != y1) //sequential
                {
                    y += Sign(dy);

                    int newIndex = MakeIndex(RoundToInt(x), RoundToInt(y));

                    if (!CheckContinueLine(newIndex, seenThroughCount, out seenThroughCount))
                    {
                        viewIndexes[newIndex] = 1;
                        break;
                    }
                    viewIndexes[newIndex] = 1;
                }
            }
            else
            {
                if (a > 1) //steep slope
                {
                    float rev = 1 / a;
                    while (y != y1)
                    {
                        x += rev * Sign(dx);
                        y += Sign(dy);

                        int newIndex = MakeIndex(RoundToInt(x), RoundToInt(y));

                        if (!CheckContinueLine(newIndex, seenThroughCount, out seenThroughCount))
                        {
                            viewIndexes[newIndex] = 1;
                            break;
                        }
                        viewIndexes[newIndex] = 1;
                    }
                }
                else //a<1, slow slope
                {
                    while (x != x1)
                    {
                        x += Sign(dx); //1 * sign(dx)
                        y += a * Sign(dy);

                        int newIndex = MakeIndex(RoundToInt(x), RoundToInt(y));

                        if (!CheckContinueLine(newIndex, seenThroughCount, out seenThroughCount))
                        {
                            viewIndexes[newIndex] = 1;
                            break;
                        }
                        viewIndexes[newIndex] = 1;
                    }
                }
            }
        }

        //Private rounding method. Simpler and more efficient than Mathf.RoundToInt
        private int RoundToInt(float val)
        {
            int valInt = (int)val;
            if (val - valInt > 0.4f) //ie 0.5 or up is rounded to 1
            {
                return valInt + 1;
            }
            return valInt;
        }

        //Private abs method
        private float Abs(float val)
        {
            if (val < 0)
            {
                return -val;
            }
            return val;
            //return val * Sign(val); //possible solution, but likely not faster
        }

        //Private help method to find sign factor
        private int Sign(float val)
        {
            if (val < 0)
            {
                return -1;
            }
            return 1;
        }

        //Help method to check if line should continue
        private bool CheckContinueLine(int newIndex, int seenThroughCount, out int seen)
        {
            seen = seenThroughCount;
            if (maxSeeThrough > 0)
            {
                int check = CheckTileSeeThrough(newIndex, seenThroughCount);
                if (check < 0)
                {
                    return false; //break
                }
                else if (check == 0)
                {
                    seen++; //return true
                }
            }
            else if (IsObstructingView(newIndex))
            {
                return false; //break
            }
            return true; //add
        }

        //Help method for GetFOW with seeThrough, kinda works like compareTo with return values -1, 0, 1
        private int CheckTileSeeThrough(int index, int seenThrough)
        {
            int maxSeeThrough = this.maxSeeThrough;
            bool obstructing = IsObstructingView(index);
            if (obstructing)
            {
                if (seenThrough < maxSeeThrough)
                {
                    return 0; //its obstructing, but we can see through
                }
                return -1; //its obstructing, but we cant see through
            }
            else if (seenThrough > 0)
            {
                return -1; //not obstructing, but have seen through something already
            }
            return 1; //its not obstructing
        }

        //help method to check if the tile at index obstructs view
        private bool IsObstructingView(int index)
        {
            if (_terrain[index] == wall
                || _occupation[index] == resource
                || _occupation[index] == building
              )
            {
                return true;
            }
            return false;
        }

        //help methods that de/increments x/y with largest delta
        private int DecrementX(int dx, int dy, int x)
        {
            if (dx * dx >= dy * dy) //dec/inc-rement X if it's abs(dx) is largest
            {
                if (dx > 0)
                {
                    x--;
                }
                else
                {
                    x++;
                }
            }
            return x;
        }
        private int DecrementY(int dx, int dy, int y)
        {
            if (dx * dx >= dy * dy) //dec/inc-rement Y if it's abs(dy) is largest
            {
                return y;
            }
            else
            {
                if (dy > 0)
                {
                    y--;
                }
                else
                {
                    y++;
                }
            }
            return y;
        }

        #region x, y and index help methods
        //help methods to make a new x- or y-value that are inside the grid
        int NewX(int x)
        {
            if (x >= width)
            {
                x = width - 1;
            }
            else if (x < 0)
            {
                x = 0;
            }
            return x;
        }
        int NewY(int y)
        {
            if (y >= height)
            {
                y = height - 1;
            }
            else if (y < 0)
            {
                y = 0;
            }
            return y;
        }

        //help methods to covert index to x-y coordinates and back
        private int MakeX(int index) //spaceX
        {
            return index % width;
        }
        private int MakeY(int index)
        {
            return index / width;
        }
        private int MakeIndex(int x, int y)
        {
            return y * width + x;
        }

        //static help methods
        public static int MakeX(int index, int width)
        {
            return index % width;
        }
        public static int MakeY(int index, int width)
        {
            return index / width;
        }
        public static int MakeIndex(int x, int y, int width)
        {
            return y * width + x;
        }
        public static int NewX(int x, int width)
        {
            if (x >= width)
            {
                x = width - 1;
            }
            else if (x < 0)
            {
                x = 0;
            }
            return x;
        }
        public static int NewY(int y, int height)
        {
            if (y >= height)
            {
                y = height - 1;
            }
            else if (y < 0)
            {
                y = 0;
            }
            return y;
        }
        #endregion
    }
}