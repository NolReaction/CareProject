# Mission and rescue runtime regression checks

Compiles production components against narrow deterministic Unity stand-ins.
The tests exercise scene-independent control flow, health events, pooling,
recruitment, obstruction decisions and reset boundaries. Physics and rendering
are deliberately not claimed to be Unity/PhysX execution.

Run with `dotnet run --project Validation/MissionRuntimeTests`.

V9.2 adds the hidden mission-lock regression: a Director with a valid hero
reference but missing `Input.mission` must connect the HUD/input route before
blocking movement. Tests cover automatic reverse linking, lost-link repair,
missing/disabled input, recovery, owner conflicts, replacement hero references
and stable frames that must not keep cancelling the menu pointer.

The same test harness compiled with the V9.1 Director fails at startup reverse
linking. These tests execute production Director/Ally code against narrow
input/vitality/navigation stand-ins; the Unity scheduler and rendering remain
outside their scope.

V16 adds session clock/revision checks, focus/pause recovery after an explicit
component re-enable, camera replacement, missing active scene dependencies,
and damaged pool ownership. A disabled/removed living enemy must produce a
recoverable result instead of an unfinishable wave; retry repairs missing
owned actors and re-enables a parked AI component. Removing an AI component
must not leave its owned collider/root behind. The tests represent destroyed
component null references explicitly; native Unity destruction order is not
simulated.

V20 adds opt-in rhythmic courses: five increasing elimination quotas, separate
cohort and between-wave rests, gradually increasing active caps, and deterministic
standard/runner/heavy introductions. Tests run all fifty pooled lives, a thirty-enemy
rescue course, interruption/retry, long-frame scheduling and scaled death presentation.
The desired short mission duration is not inferred from test speed: a separate instant-kill
probe with the shipped tuning measures the minimum release schedule only (146.95
active seconds at 20 updates/second), excluding combat, travel and native rendering.

Terminal snapshots carry a per-run GUID, run/result revisions, actual kills,
successful rescues and elapsed time. Tests cover repeated finish callbacks,
selection, immediate retry before a reward observer updates, and distinct Director
instances. `LastRunSummary` remains available after retry even while
`HasRunSummary` is false for the newly active run; settlement observes its result
revision and persistent run ID.
