# Reactive prop checks

This executable compiles the production `IntercomPropState`, `IntercomReactiveProp`
`IntercomPropHitResponse`, `IntercomPropHitTint`, `IntercomTargetDummy`
and `IIntercomDamageable`. Narrow Unity stand-ins track collider flags, transforms,
material property blocks and reset versions; System.Numerics supplies rotation math.

Checks cover invalid and terminal damage; relay timing at 30/60/120 FPS and long
frames; paused repair; visual transitions; collider consequences; authored disabled
colliders; respawn while paused; component disable/re-enable; replacement reset
motor; original transform/property-block restoration; no shared material mutation
or per-frame material/property-block creation. Authored debris swap tests cover
the brief last-hit squash, active hierarchy safety, collision release, repeated
break/reset cycles, paused respawn, disable/re-enable and invalid owner references.
Folded bumper cases check authored lift, lift during the bent state, exact reset
and finite fallback poses for invalid lift/angle settings. Exact mesh clearance
is checked separately by the courtyard geometry review, not by these stand-ins.

Run `dotnet run --project Validation/ReactivePropTests/ReactivePropTests.csproj`.
This does not substitute for importing, rendering or shooting at props in Unity.

Cartoon feedback checks exercise accepted hits immediately tinting red; the
positive squash and opposite settling stretch; restoration of exact baseline
scales/property blocks; preserved alpha and unrelated overrides; empty-block
cleanup; repeated hit/reset cycles without drift; no new materials/property
blocks; invalid tuning fallback; and safe target reset without a mesh reference.
Collider owners remain unscaled throughout both prop and target feedback.
