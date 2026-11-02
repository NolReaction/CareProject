# Stationary turn-step regression

Runs the production Unity-independent turn scheduler and planar support-pivot
helper. Covers alternating boot ownership, one landing per step, final gathering,
heading wraparound, speed/angle hysteresis, mid-step reversals, pause/airborne/reset,
malformed settings, frame-rate cadence and 4,000 independently constructed rigid
pivot cases. The geometry checks prove the planar fixed-ankle identity and unchanged
link distance; they do not simulate a skinned boot, calf reach or Unity collisions.

Run `dotnet run --project Validation/TurnStepTests/TurnStepTests.csproj`.
