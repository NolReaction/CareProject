# NPC runtime/factory integration checks (V9)

`dotnet run --project Validation/NpcRuntimeTests` compiles the production capsule
controller, factory, definition and brain against narrow stand-ins. The real
health arithmetic and shared navigation are checked by their own test projects.

Coverage includes initialization before activation, disabled visual colliders,
shared materials, early attack tracking with a final fixed commitment window,
actual reach limits, low and upper obstruction rays, death/pause/respawn
cancellation, target handoffs without stale strikes, pool reuse and new home
positions, bounded investigation of a recorded location without hidden target
tracking, stable approach sides, lateral stuck recovery, safe deferred routes,
final motion checks and owned mesh disposal.

The stand-ins provide deterministic callbacks and ray results. This validates
the controller's use of those results, not actual Unity lifecycle ordering,
Physics raycasting, CharacterController contact resolution or rendered output.
