# Shooter / magazine integration

Runs the complete production `IntercomSimpleShooter` with the production magazine component/state, weapon definition, projectile direction, swept-hit selection and surface resolver. Controlled physics results let the test exercise the real `Spawn` and `LateUpdate` paths.

Cases cover clear, blocked and partially blocked barrels; one ammo commit before callbacks; actual parallel projectile positions; empty and partially consumed magazines; queued attempts; pool exhaustion; pause/focus/disable; material hit routing and per-projectile damage snapshots. Confirmed visual recoil occurs only after a real projectile is created.

Physics queries and the pose event source are narrow doubles. Mesh generation is covered by separate tracer tests. This test does not execute Unity, PhysX, Animator, rendering or the actual GUI.

Run `dotnet run --project Validation/ShooterIntegrationTests` from the project root. The common `Validation/run_checks.py` discovers this project automatically.
