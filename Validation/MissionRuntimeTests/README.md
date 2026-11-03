# Mission and rescue runtime regression checks

Compiles production components against narrow deterministic Unity stand-ins.
The tests exercise scene-independent control flow, health events, pooling,
recruitment, obstruction decisions and reset boundaries. Physics and rendering
are deliberately not claimed to be Unity/PhysX execution.

Run with `dotnet run --project Validation/MissionRuntimeTests`.
