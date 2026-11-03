# Mission-state regression checks

These tests compile the production mission state directly. They exercise exact
kill objectives, pooled actor identity, stale callbacks, terminal outcomes and
rescue eligibility. They do not simulate Unity, navigation, rendering or PhysX.

Run with `dotnet run --project Validation/MissionStateTests`.
