# NPC runtime/factory integration checks (V11)

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

V11 also executes the actual controller arrival: warning/underground phase,
collision and vulnerability lease, no hidden melee, frozen time/focus/pause,
death/disable/respawn cancellation, exact final pose, and 100 pooled
arrival/death cycles without new objects/materials/destruction. Distant sight
samples are counted over100 updates; close-strike rays and geometry/target
invalidation remain immediate, and cached perception retains the last sampled
location rather than reading later movement through a wall.

The stand-ins provide deterministic callbacks and ray results. This validates
the controller's use of those results, not actual Unity lifecycle ordering,
Physics raycasting, CharacterController contact resolution or rendered output.
