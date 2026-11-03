# Camera tracking regression

Runs the production `IntercomCameraTracking` ground-plane projection and tracking.
No mirrored implementation and no Unity dependency. Tests cover:

- The initial movement genuinely leaves the camera still; anticipation does not defeat the dead zone.
- Noticeable on-screen displacement while running, smooth border crossing and direction reversal.
- Orthographic and perspective viewport bounds across pitch, yaw, roll and aspect ratios.
- Safe centered fallback for near-horizontal ground-plane projections.
- Optional delayed recentering, disabled recentering, pause, invalid target, teleport reset and map clamps.
- Comparable motion at 15, 30, 60, 120 and 240 FPS; outer safety bounds during fast movement.
- V10 adaptive motion: actual velocity drives bounded prediction, while a held stick against a wall or stale aim direction does not.
- Reduced chase lag at the same running speed, initial screen freedom, no reverse drift when stopping, and braking/reversal within one second.
- Immediate reversal just after leaving the free zone: stale forward framing must not accelerate the camera; the shorter brake decreases old-direction travel during the first 0.2 seconds.
- Diagonal lead is capped in world distance; pause/teleport discard stale motion; optional no-recenter preserves the parked composition.

Run: `dotnet run --project Validation/CameraTrackingTests/CameraTrackingTests.csproj`.
The tests exercise actual production mathematics, but do not execute Unity Editor,
rendering, editor capture buttons or scene serialization. Those need the manual
Play Mode checklist in `Docs/CameraV4.md`.
