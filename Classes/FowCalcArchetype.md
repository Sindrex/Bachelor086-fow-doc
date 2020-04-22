# FowCalcArchetype

## Description
An archtype of the Fow Calculator with one view distance and a maximum amount of resources/walls to see through.

## Public Properties
Property | Description
--- | ---
viewDist | This archetype's view distance (Read Only).
perJobAlloc | The amount of array indexes needed to store this archetype's maximum view.

## Static Properties
Property | Description
--- | ---
TOL | The tolerance used when finding the edge deltas of the view (Read Only).
BATCH_SIZE | The batch size amount used with [IJobParallelFor](https://docs.unity3d.com/ScriptReference/Unity.Jobs.IJobParallelFor.html) scheduling.
PER_JOB_ARRAY_PADDING | The amount of safety padding added to the [IJobParallelFor](https://docs.unity3d.com/ScriptReference/Unity.Jobs.IJobParallelFor.html) result array per job.
WORLD_GRID_SPATIAL_INDEXING_PADDING | The amount of padding used when spatial indexing the world grid for obstacles.

## Constructors
Constructor | Description
--- | ---
FowCalcArchetype | Creates an archetype given a world width and height, and a view distance. Overload includes an additional int for the maximum amount of resources/walls that can be seen through.

## Public Methods
Method | Description
--- | ---
RunJobs | Calculates and returns a concatinated array of view indexes for units of this archetype given a list of their start indexes. Schedules a [IJobParallelFor](https://docs.unity3d.com/ScriptReference/Unity.Jobs.IJobParallelFor.html). 
