# Ground feedback lifecycle regression

This fixture compiles the complete production `IntercomGroundFeedback` and
`IntercomPlayerMotor` together with their real math and cadence helpers. It catches
missing project members that a syntax-only check cannot detect. Engine types and
unrelated game systems use narrow test stand-ins.

It exercises startup with/without a controller, one-shot recovery after late
controller assignment/replacement, calibration pose restoration on success and
exceptions, unavailable contact reporting and saved-pose cleanup on disable.
It executes the actual `CalibrateSoles`, `Awake`, `LateUpdate` and `OnDisable`
methods, including the production vertex-marker selection code.

It is not a Unity assembly compilation, renderer, PhysX, imported mesh skinning,
AnimationClip engine evaluation, Play Mode or device test.

Run: `dotnet run --project Validation/GroundFeedbackLifecycleTests/GroundFeedbackLifecycleTests.csproj`
