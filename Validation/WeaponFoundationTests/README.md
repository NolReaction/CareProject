# Weapon foundation tests

Run `dotnet run --project Validation/WeaponFoundationTests/WeaponFoundationTests.csproj`.
Production weapon definition/calculator, pose clock, health state/component, and projectile
projection are compiled against small Unity type doubles. Covers ideal dual/single TTK,
first-shot zero convention, fractional HP, invalid parameters,30/60/120FPS cadence,
live interval change, planar fire/fallback, death once, invulnerability and reset.
This is not a Unity engine/PhysX/Animator test.
