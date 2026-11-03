# HUD layout tests

Runs production HUD layout, drawing, pointer ownership and reload-region classes
with narrow Unity stand-ins. Tests safe rectangles at 1280×720, 1920×1080,
2340×1080 with notches, 640×360, 320×180 and portrait/small safe areas, including
minimum/maximum configurable stick sizes. Exports actual drawing commands and
cached texture bytes to `Validation/Results/V7UI/HudPreviews`.

Run: `dotnet run --project Validation/HudLayoutTests/HudLayoutTests.csproj` from
the repository root. Running from the test directory also works; the project
root is resolved from the working directory or compiled assembly location.

Preview: from repository root run `python Validation/render_hud_preview.py`.
These are offline layout previews, not Unity screenshots or device validation.
