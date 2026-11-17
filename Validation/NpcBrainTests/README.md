# NPC decision and punch checks

Run `dotnet run --project Validation/NpcBrainTests` from the project root, or use
the general `Validation/run_checks.py` runner.

The test compiles the actual `IntercomNpcBrain` and `IntercomNpcCombatMath` source
without Unity. It checks patrol, awareness and memory, leash/return, windup,
single-hit/recovery timing, interruption, death/reset, frame-rate differences,
long/invalid frame handling and the finite locked-heading melee cone predicate.

Timing regressions configure an explicit .45/.65-second profile so they remain
valid independently of the current gameplay defaults. The runtime tests exercise
the live authored profile and original early tracking/final commitment behavior.

V20 also checks the immutable Standard/Runner/Heavy multipliers against custom
base values, safe fallback for unknown enum values, eight stable approach slots,
separate capsule-sized goals, antisymmetric repulsion (including exactly
coincident actors), and directional lane braking with invalid/extreme inputs.
These helpers allocate no arrays and have no world or time state; navigation
still validates each resulting runtime movement against actual obstacles.

These are decision/geometry unit checks. CharacterController collision, line-of-
sight raycasts, navigation integration, GUI and Animator require separate tests
and a Unity playtest; this test does not simulate those systems.

V23 additionally checks nearest-side approach selection for every pool ordinal,
all eight physical directions and invalid/coincident position fallbacks.
