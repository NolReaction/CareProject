# Death/strike lifecycle regression

Runs production Health, NPC controller/factory, and Mission Director together.
Unity stand-ins provide deterministic callbacks and reject CharacterController.Move
on disabled/inactive controllers instead of silently accepting the invalid call.

The pre-V12 controller fails on a lethal NPC strike when the real Director parks
that same actor synchronously. Tests also cover source disable, same-object reuse,
source death, target change and hero respawn inside accepted damage callbacks.

Run with .NET 8:

`dotnet run --project Validation/DeathStrikeLifecycleTests`

This is a managed component/engine-contract regression, not Unity Play Mode or a
claim to reproduce a native Unity process crash.
