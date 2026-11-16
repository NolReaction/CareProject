# Cosmetic combat regression tests

The project compiles the actual IntercomCombatEffects, WeaponDefinition,
ProjectileImpact and FootstepSurface with controlled Unity stand-ins.
It executes real event subscription, the cosmetic pools and material routing.

Checks cover per-hand casing cadence, unchanged muzzleflash frequency, both hands,
25% larger default casing, configurable ejection disable/rate, finite settings,
bounded pools, zero new objects/materials/meshes over sustained fire and hits,
focus/pause/disable/re-enable cleanup, effect expiration, separate scenery/robot
bursts and the broader comic metal glints. Rendering, exact trajectories and
PhysX contact execution are outside this harness.

V20 also checks accepted robot damage (one brief warm core flash and two shared
brass chips), declined robot contact (one small glance without core confirmation),
non-finite impact positions and full elapsed expiry with a capped cosmetic motion
step after a hitch. The fixed pool and shared meshes/materials remain unchanged.

Run: dotnet run --project Validation/CombatEffectsTests/CombatEffectsTests.csproj.
