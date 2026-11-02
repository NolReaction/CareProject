# Material contact routing

`dotnet run --project Validation/ProjectileSurfaceTests` compiles the production hit payload
and surface lookup with small Unity stand-ins. It checks compound-collider inheritance,
nearest explicit tags, disabled tags, impact/footstep controls being independent, fallback
materials and invalid inspector values. This is not a Unity/PhysX test.
