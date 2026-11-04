# Robot death burst regression

Compiles the production `IntercomRobotDeathEffects` component with narrow deterministic Unity contracts.

Run with .NET 8:

`dotnet run --project Validation/RobotDeathEffectsTests`

Checks bounded incremental prewarming, 200 emissions without new GameObjects/meshes/materials, default cap of 40 generic pieces plus 12 actual static chunks, shared authored asset ownership, rotating mesh-bound floor support, shrink/recycle, definition/template binding, missing-appearance fallback, pause/focus/disable/reset, and elapsed lifetime expiry during a long frame with capped motion integration. No Rigidbody, collider, mesh baking, explosion damage or audio is used by the presentation.

MissionRuntimeTests separately exercises the Director death event guards: one burst per accepted life, duplicate/stale callbacks, optional-unready fallback, root ownership, retry/menu/disable cleanup and preserving the final brief burst on victory. DeathStrikeLifecycleTests retains the lethal-hit/parked-controller contract.

These are managed component/engine-contract regressions. They do not validate native Unity rendering, animation import, draw calls, device performance or PhysX. Run the V19 native Unity checklist before claiming those checks passed.
