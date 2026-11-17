# Pursuit trail contracts

Run `dotnet run --project Validation/PursuitTrailTests`.

Compiles the production Unity-independent `IntercomPursuitTrail`. Checks ordered
elbow consumption, strictly collinear tail compaction, retained gradual curves
and reversals, 512-point storage, overflow without dropping the oldest corner,
teleport and target/life resets, invalid values, stopped-target catch-up and
zero managed allocations during steady-state recording/consumption.

The adapter owns visibility, physical arrival, collision and safe projection.
`NpcRuntimeTests` executes those boundaries, including fresh visible acquisition
only after a saturated trail's last saved point is physically reached. This
fixture does not execute Unity navigation, physics or rendering.
