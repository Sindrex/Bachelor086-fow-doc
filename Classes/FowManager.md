# FowManager
Inherits from [MonoBehaviour](https://docs.unity3d.com/ScriptReference/MonoBehaviour.html).

## Description
Controls when, for who and how the Fog of War updates. Clientside singleton.

## Public Properties
Property | Description
--- | ---
unitFows | Units that have requested an update to their view.
owFowMap | The [MeshRenderer](https://docs.unity3d.com/ScriptReference/MeshRenderer.html) that renders the unit's view in the overworld.
spatialGridWidth | The amount of squares that make up the spatial grid's width. Must be a divisor of worldWidth and worldHeight!
spatialGridPrefab | The saved [GameObject](https://docs.unity3d.com/ScriptReference/GameObject.html) that is instantiated to make the spatial grid.
minUpdateTimer | The minimum amount of seconds (float) that needs to pass before the fog of war can update again.
viewDists | The view distances for the separate [FowCalcArchetype](https://github.com/Sindrex/Bachelor086-fow-doc/blob/master/Classes/FowCalcArchetype.md)s.
seeThroughs | The maximum amount of resources/walls to see through for the separate [FowCalcArchetype](https://github.com/Sindrex/Bachelor086-fow-doc/blob/master/Classes/FowCalcArchetype.md)s.
fowCalcArchetypes | The created [FowCalcArchetype](https://github.com/Sindrex/Bachelor086-fow-doc/blob/master/Classes/FowCalcArchetype.md)s based on viewDists and seeThroughs.
batchSize | The batch size used for scheduling jobs in [FowCalcArchetype](https://github.com/Sindrex/Bachelor086-fow-doc/blob/master/Classes/FowCalcArchetype.md).

## Public Methods
Method | Description
--- | ---
CheckUpdateFow | Called by update loop. Checks if fow should be updated. Uses [Time.deltaTime](https://docs.unity3d.com/ScriptReference/Time-deltaTime.html) for timer.
UpdateFow | Sends signal that the FOW needs to be updated. Adds unit that needs to update into unitlist. Called by [UnitFowController](https://github.com/Sindrex/Bachelor086-fow-doc/blob/master/Classes/UnitFowController.md).

## Static Methods
Method | Description
--- | ---
GetInstance | Returns the instance of this singleton.
GetGridIndex | Returns an index in the world grid based on the [Vector3](https://docs.unity3d.com/ScriptReference/Vector3.html).
