# HUD layout tests

Runs production HUD layout, drawing, pointer ownership and reload-region classes
with narrow Unity stand-ins. Tests safe rectangles at 1280×720, 1920×1080,
2340×1080 with notches, 640×360, 320×180 and portrait/small safe areas, including
minimum/maximum configurable stick sizes. Adds 1,200 reproducible random safe areas,
full capture-circle separation, exact pause action hit boundaries and tiny bounds.
The phone hierarchy is explicit: pause and health upper-left, objective below,
clock centered and weapon/reload upper-right. Pause keeps at least a 44px square
on ordinary phone-safe areas (360×300 and larger), shrinking only in tiny windows.
Every top instrument is pairwise
non-overlapping, and all five stay clear of both stick capture circles. Exports actual drawing commands and
cached texture bytes to `Validation/Results/V17UI/HudPreviews`.

Run: `dotnet run --project Validation/HudLayoutTests/HudLayoutTests.csproj` from
the repository root. Running from the test directory also works; the project
root is resolved from the working directory or compiled assembly location.

These are offline layout previews, not Unity screenshots or device validation.
