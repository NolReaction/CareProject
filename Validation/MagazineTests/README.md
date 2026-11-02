# Magazine and parallel-fire regression

Runs production `IntercomMagazineState`, `IntercomWeaponMagazine`, `IntercomWeaponPoseClock`,
weapon balance/definition, reload-pointer capture and projectile-direction helpers against
narrow Unity test doubles. Tests automatic/manual reload, pause/focus/respawn, partial
successful volleys, unlimited reserve, TTK at 30/60/120 FPS, long reload pose hold, pointer
isolation from the movement stick, and non-crossing trajectories around 360 headings.

This does not execute the real Shooter Physics queries, real UI touch backend, Animator
or rendering. The source Shooter commit path is reviewed separately; actual engine checks
are in `MOBILE_TESTING.md`.
