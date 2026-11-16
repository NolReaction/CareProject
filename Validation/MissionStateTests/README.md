# Mission-state regression checks

These tests compile the production mission state directly. They exercise exact
kill objectives, pooled actor identity, stale callbacks, terminal outcomes and
rescue eligibility. They do not simulate Unity, navigation, rendering or PhysX.

Run with `dotnet run --project Validation/MissionStateTests`.

V20 includes the pure wave policy and immutable run-summary value. Coverage checks
exact finite supplies across tiny/custom wave sizes, legacy pacing compatibility,
bounded pressure, ordered archetype introductions and malformed parameter limits.
Summary checks prevent a failed escort or free-play result from inventing rescues
or kills. None of these checks confirms native Unity scheduling or game feel.
