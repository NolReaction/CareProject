# Runtime turn geometry regression

Runs the production `IntercomGroundFeedback`, its turning partial, `IntercomPlayerMotor`, and the turn scheduler against narrow engine stand-ins. `RigGeometry.cs` records the repaired robot's Idle frame 1 bone transforms and sole mesh samples at the scene's measured scale (1.3790181109932065).

Checks sustained turns at 30/60/120 FPS; 18,000 deterministic random-heading frames and over 1,000 verified landings; stance ankle position and full boot orientation; immutable local bone positions/scales; a 16 cm support envelope; measured sole clearance and landing height; upper/lower heading bounds including the final torso spring; narrow inspector limits; movement cancellation/reentry; idle and airborne audio suppression; teardown.

This is real production transform math with measured rig geometry. The engine stand-ins do not execute Unity Animator, physics, skinning, import, or device rendering. Independent full-mesh checks are recorded separately in `Validation/TurnActualMeshIndependent.json`.

Run with `.NET 8`: `dotnet run --project Validation/TurningRigTests/TurningRigTests.csproj`.
