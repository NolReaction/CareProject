# Mission HUD and pause tests

Compiles production `IntercomPlayerInput`, `IntercomWorkshopHud` and
`IntercomHudLayout` with narrow Unity/mission stand-ins. Covers mode selection,
phone-first active HUD with upper-left pause/repair-heart health, secondary objective row and upper-right equipped weapon, result actions, mouse/touch pause/resume at timeScale zero,
restart/exit confirmations, Escape/Backspace, held-pointer cancellation and
previous-scale ownership across disable, destroy, replaced missions and new runs.
Timer/progress caches are measured for allocations when displayed values stay
unchanged. Opening tutorials and repeated hints expire after five active seconds.

Actual draw commands export to `Validation/Results/V17UI/MissionPreviews`.
Run `python Validation/render_gameplay_hud.py` after the test for offline raster
previews (requires Pillow and DejaVu Sans). The preview uses the assigned repository
pistol portrait. These checks do not execute Unity rendering, native touch or the
mission director; the latter has its own runtime tests.

The small notched-window case executes simultaneous movement/reload/aim/pause through production input. Passive desktop HP/objective/timer/visible-toast presses stay UI-owned until release without blocking keyboard movement. Expired hints leave no invisible blocker. Render exports include damage, low ammunition, reload progress and portrait states.
