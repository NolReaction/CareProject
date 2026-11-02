# Player audio lifecycle checks

Run from the repository root with .NET 8:

```sh
dotnet run --project Validation/PlayerAudioLifecycleTests/PlayerAudioLifecycleTests.csproj
```

The harness compiles the actual audio runtime scripts against small Unity API
fakes. Its 81 assertions cover bounded voices, clip replacement, pause/focus,
footstep gating, component subscription lifecycle, impact deduplication/range,
and contact feedback when no step clip is assigned. Added coverage includes:

- Actual-foot contact timing taking priority over the fallback animation clock.
- Surface banks, inherited/disabled tags, weight/pitch and persistent voice gain.
- Six footstep materials and four impact banks, explicit tags overriding target fallback,
  muted material handling, missing-bank fallback and impact gain across mix updates.
- No audio for idle, airborne, owner/trigger/null contacts; legacy ground probes
  reject absent ground, wall normals and saturated results.
- Two independent faded background loops, pause/resume, toggles and profile changes.
- Bounded contact voices, no per-contact GameObjects and exactly one subscription.

They do **not** run Unity, PhysX, Animator evaluation, clip import or real audio output. Unity Editor and
device checks are still required.
