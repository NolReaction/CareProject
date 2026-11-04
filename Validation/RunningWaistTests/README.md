# Running waist integration

Executes production `IntercomPlayerMotor` and `IntercomGroundFeedback` with the
shared narrow transform/engine stand-ins. Covers full Ground Update/LateUpdate,
30/60/120 Hz, free 20-degree standing aim, planted turns beyond 28 degrees,
moving upper/lower separation bounded to 42 degrees, rigid leg preservation,
360-degree reversals, the damped Speed-to-Idle gap, both turn/run handoffs,
focus loss and exact pose restoration. This does not execute Unity or PhysX.

Run with .NET 8:

`dotnet run --project Validation/RunningWaistTests`
