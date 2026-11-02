# Camera tracking regression

Runs the production `IntercomCameraTracking` ground-plane projection and tracking.
No mirrored implementation and no Unity dependency. Tests cover:

- The initial movement genuinely leaves the camera still; anticipation does not defeat the dead zone.
- Noticeable on-screen displacement while running, smooth border crossing and direction reversal.
- Orthographic and perspective viewport bounds across pitch, yaw, roll and aspect ratios.
- Safe centered fallback for near-horizontal ground-plane projections.
- Optional delayed recentering, disabled recentering, pause, invalid target, teleport reset and map clamps.
- Comparable motion at 15, 30, 60, 120 and 240 FPS; outer safety bounds during fast movement.

Run: `dotnet run --project Validation/CameraTrackingTests/CameraTrackingTests.csproj`.
The tests exercise actual production mathematics, but do not execute Unity Editor,
rendering, editor capture buttons or scene serialization. Those need the manual
Play Mode checklist in `Docs/CameraV4.md`.
