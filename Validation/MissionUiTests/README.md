# Mission UI V9

Compiles production `IntercomPlayerInput`, `IntercomWorkshopHud` and
`IntercomHudLayout` with narrow Unity/mission stand-ins. Covers safe areas, mode
selection, gameplay menu return, result retry/selection, Escape/Backspace,
multiple held fingers, cancellation and preventing menu presses from firing.

Actual HUD drawing commands are exported to `Validation/Results/V9UI/HudPreviews`.
Run `python Validation/render_mission_hud_v9.py` after this test to generate the
offline previews. Tests do not execute Unity rendering, native touch or the
mission director; the latter has its own runtime tests.
