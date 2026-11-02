# Grounding / arm gesture integration

Compiles production GroundFeedback, Motor and CharacterAnimation together with the shared narrow engine doubles. Runs 1,000 apply/restore frames, both disable orders, preservation of an externally changed hand pose and controller teardown. GroundFeedback's own pose snapshot and restoration execute; its added torso aim correction is represented by a fixed four-degree arm delta for this ownership test. The full movement solve has separate measured tests.

Also checks that the shipped scene contains one Magazine and one CharacterAnimation component, connected to Robot with the real script GUIDs. This is static YAML validation, not Unity import or Animator/PhysX playback.

Run `dotnet run --project Validation/AnimationGroundingIntegrationTests` from the project root. All new scene files are copied as validation content.
