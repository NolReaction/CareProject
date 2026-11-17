# Enemy audio lifecycle checks

Run `dotnet run --project Validation/EnemyAudioTests/EnemyAudioTests.csproj --configuration Release`.

Compiles the production shared enemy-audio component and real health receiver against narrow actor/audio stand-ins. Verifies main-thread resource preparation, cue edges, variant changes, one-time death, synchronous mission parking, stale health/run rejection, range, pause/focus/menu silence, no catch-up on resume, retry reuse, callback cleanup, finite PCM, quiet clip boundaries and headroom.

Twelve generated clips use three variants each: arrival servo, windup servo, swing whoosh, chassis rattle. The four low-priority shared voices use bounded kind cooldowns and reject quieter cues while higher-priority voices occupy the pool. The last accepted death may leave a brief tail during Victory; Selection and Defeat stop all voices.

The fixture saves the exact PCM sent to `AudioClip.SetData`, sequenced with half-second gaps, as `Source/Audio/Previews/V23_EnemySounds.wav`. This preview is the raw bank, before runtime volume, archetype pitch and distance attenuation.

`python3 Validation/check_enemy_audio_scene.py` verifies authored component/material-free wiring and repeat-safe installation. Native Unity output, spatial attenuation and the subjective mix still require Play Mode/device listening.
