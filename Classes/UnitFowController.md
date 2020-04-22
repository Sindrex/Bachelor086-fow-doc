# UnitFowController
Inherits from [MonoBehaviour](https://docs.unity3d.com/ScriptReference/MonoBehaviour.html).

## Description
Component used in Spatial Indexing of units.

## Public Properties
Property | Description
--- | ---
fowCalcArchetypeID        | The ID of the fowCalcArchetype this unit uses.
gridIndex                 | The index in the world grid.
myView                    | The indexes of the world grid that makes up the unit's view.
prevView                  | The indexes of the world grid that makes up the unit's previous view.
mySpatialCollider         | The collider that interacts with the [SpatialGridController](https://github.com/Sindrex/Bachelor086-fow-doc/blob/master/SpatialGridController.md)
mySquares                 | The squares of the spatial grid that this unit's view reaches.

## Public Methods
Method | Description
--- | ---
CheckUpdateFow  | Checks whether or not the attached unit has moved, and if so sends request to [FowManager](https://github.com/Sindrex/Bachelor086-fow-doc/blob/master/FowManager.md) that the fow should update.
