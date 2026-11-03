# NPC decision and punch checks

Run `dotnet run --project Validation/NpcBrainTests` from the project root, or use
the general `Validation/run_checks.py` runner.

The test compiles the actual `IntercomNpcBrain` and `IntercomNpcCombatMath` source
without Unity. It checks patrol, awareness and memory, leash/return, windup,
single-hit/recovery timing, interruption, death/reset, frame-rate differences,
long/invalid frame handling and the finite locked-heading melee cone predicate.

Timing regressions configure an explicit .45/.65-second profile so they remain
valid independently of the current gameplay defaults. The runtime tests exercise
the current V9 profile and tracking/commit behavior.

These are decision/geometry unit checks. CharacterController collision, line-of-
sight raycasts, navigation integration, GUI and Animator require separate tests
and a Unity playtest; this test does not simulate those systems.
