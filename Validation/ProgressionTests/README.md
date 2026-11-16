# Progression integration checks

Compiles the actual progression model/component, weapon magazine/definition and
mission summary against narrow Unity and PlayerPrefs substitutes. Run with
`dotnet run --project Validation/ProgressionTests` from the project root.

Coverage: terminal rewards and honest breakdown, no FreePlay/unfinished farming,
repeat-frame and component/application recreation deduplication, immediate retry
before the next progression Update, all three purchase ranks, live magazine /
reload / respawn / balance agreement, unchanged shared weapon definition,
equipped weapon changes, missing dependencies/capped magazine purchase rejection,
two-bank recovery and truncated/checksum-corrupted records, save failure and
purchase rollback, delayed retry across multiple completed runs, a bounded32
receipt FIFO with unseen-result reservation before retry, diagnostic sandbox with zero preference
writes, small shared visual mesh closure/winding and owned-resource disposal,
wallet/statistics/capacity bounds.

These checks do not execute Unity, its native PlayerPrefs backend, renderer,
actual import, physics or device storage. Native soak testing is separate.
