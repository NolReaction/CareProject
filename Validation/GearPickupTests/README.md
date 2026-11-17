# Collectible gear checks

Runs the real bounded ledger, pickup pool and progression reward model with narrow engine contracts.
Checks scatter/persistence, proximity collection, duplicate collection, stacked value at capacity,
robot archetype drops, once-per-run prop rewards, pause, defeat, victory sweep, retry cleanup,
freeplay restrictions and native resource reuse. Unity rendering and scene import remain separate.

`dotnet run --project Validation/GearPickupTests --configuration Release`

V23 checks also cover deterministic 75% ordinary/runner drop occurrence across 4096 life seeds,
fewer physical tokens with preserved standard/heavy expected value, an audio voice shared by a
magnet batch with 90ms spacing and four subtle pitches, silent virtual victory settlement,
focus/pause/menu/disabled-owner gates and owned audio cleanup.

The run writes `Source/Audio/Previews/V23_GearPickup.wav` from the exact PCM passed to the
production `AudioClip.SetData` call (16-bit PCM export, mono 22050 Hz). It checks zero-valued
endpoints, finite samples and headroom; native audio mixing/output still needs a Unity listen.
