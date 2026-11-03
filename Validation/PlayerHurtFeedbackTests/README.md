# Player hurt V10

This console harness compiles the actual `IntercomPlayerHurtFeedback`,
`IntercomHurtResponse`, `IntercomHealth`, health arithmetic and damage types.
Unity lifecycle methods are invoked explicitly. The narrow Unity/AudioSource,
motor and vitality stand-ins do not simulate Unity animation, sound playback,
PhysX, device frame timing or the Editor.

Run with `dotnet run --project PlayerHurtFeedbackTests.csproj`.

The tests cover accepted versus rejected health events, directional bounded
impulses, frame-rate-independent decay, zero managed allocation in the response
loop after warmup, one pre-created audio voice, cooldown, alternating clips,
focus/pause/menu/death/respawn/rebinding cleanup and lethal damage opening the
mission result screen synchronously. The latter must not cut off the already
started final impact sound.

GroundFeedback owns the actual torso transforms. Its integration is checked in
the separate ground-feedback lifecycle suite; this harness never pretends to
measure rendered animation quality.
