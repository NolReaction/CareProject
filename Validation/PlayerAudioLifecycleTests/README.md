# Player audio lifecycle checks

Run from the repository root with .NET 8:

```sh
dotnet run --project Validation/PlayerAudioLifecycleTests/PlayerAudioLifecycleTests.csproj
```

The harness compiles the actual audio runtime scripts against small Unity API
fakes. Its 42 assertions cover bounded voices, clip replacement, pause/focus,
footstep gating, component subscription lifecycle, impact deduplication/range,
and contact feedback when no step clip is assigned. It does **not** run Unity,
PhysX, Animator evaluation, clip import or real audio output. Unity Editor and
device checks are still required.
