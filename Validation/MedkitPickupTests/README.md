# Medkit pickup runtime contracts

`dotnet run --project Validation/MedkitPickupTests/MedkitPickupTests.csproj --configuration Release`

Compiles the production medkit component, shared health state and health receiver against narrow Unity engine stand-ins. Checks actual/partial restoration, invalid healing, no resurrection or overheal, full-health preservation, one-time/reentrant collection, distance and wall guards, pause/focus/menu/death/disabled actor gates, free play, retry generations, native resource reuse and owned-resource cleanup.

`python3 Validation/check_medkit_scene.py` checks serialized component/material links, repeat-safe installation, obstacle footprints and conservative reachability from the player spawn.

These checks do not execute Unity import, rendering, audio output, PhysX or Play Mode. Inspect both world medkits, collection effects and the HUD healing animation in the shipped Unity version before judging presentation.
