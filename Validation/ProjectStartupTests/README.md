# First-open editor policy

Runs the actual IntercomProjectStartup editor class with minimal Unity API doubles.
Checks pristine default scene opening, a persistent project-scoped preference,
compile/import deferral, missing imports and bounded timeout, preserving dirty
or custom scenes, Play/batch cancellation and handled import errors.

This validates control flow; actual Unity import and Editor UI still need Unity.
