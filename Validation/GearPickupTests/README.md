# Collectible gear checks

Runs the real bounded ledger, pickup pool and progression reward model with narrow engine contracts.
Checks scatter/persistence, proximity collection, duplicate collection, stacked value at capacity,
robot archetype drops, once-per-run prop rewards, pause, defeat, victory sweep, retry cleanup,
freeplay restrictions and native resource reuse. Unity rendering and scene import remain separate.

`dotnet run --project Validation/GearPickupTests --configuration Release`
