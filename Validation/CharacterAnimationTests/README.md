# Character gesture regression

Runs production `IntercomCharacterAnimation` and its envelope math against small
Unity stand-ins. Checks reload completion before the next shot, no cumulative
rotation without Animator evaluation, ownership-aware restore, unchanged local
bone positions/scales, wall filtering, pause/disable and idle cancellation.

The separate `AnimationGroundingIntegrationTests` project combines the real
GroundFeedback restoration with the character component to check their ordering.

Run with .NET 8:

```sh
dotnet run --project Validation/CharacterAnimationTests
```

This does not execute Unity Animator, PhysX or native rendering. Blender pose
inspection is reported separately in `CharacterAnimationV6Review.json`.
