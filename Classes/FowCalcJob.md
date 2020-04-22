# FowCalcJob
Struct inside [FowCalcArchetype](https://github.com/Sindrex/Bachelor086-fow-doc/blob/master/Classes/FowCalcArchetype.md). Inherits from [IJobParallelFor](https://docs.unity3d.com/ScriptReference/Unity.Jobs.IJobParallelFor.html).

## Description
A parallelized for loop for a given archetype. Uses a DDA algorithm to calculate the indexes that make up this archetype's view given a start index.

## Public Properties
Property | Description
--- | ---
width | The world grid's width.
height | The world grid's height.
\_terrain | The terrain grid (Read Only).
\_occupation | The occupation (buildings) grid (Read Only).
wall | A byte value symbolising a wall.
resource | A byte value symbolising a resource.
building | A byte value symbolising a building.
startIndexes | The list of start indexes which will be executed upon.
tileCount | The length of the world grid array (ie. the amount of tiles in the grid).
viewDist | The view distance of this archetype.
maxSeeThrough | The maximum amount of resources/walls that can be seen through.
staticDeltaX | The pre-calculated edge deltas' x values (Read Only).
staticDeltaY | The pre-calculated edge deltas' y values (Read Only).
results | The results array (Write Only).
perJobAlloc | The amount of indexes in the results array given to each parallel job (per start index).

## Public Methods
Method | Description
--- | ---
Execute | Calculates the view for a given start index at position i in the start indexes array.

## Static Methods
Method | Description
--- | ---
MakeX | Returns the x value of a given index.
MakeY | Returns the y value of a given index.
MakeIndex | Makes an index given an x and y value.
NewX | Returns an x value inside the world grid, given an x value that could possibly be outside the grid.
NewY | Returns a y value inside the world grid, given an y value that could possibly be outside the grid.
