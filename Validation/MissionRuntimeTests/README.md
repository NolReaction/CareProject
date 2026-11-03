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
