# Native verification driver safety

Compiles the actual Editor-only native-run driver and executes its entry/cancellation paths
against narrow deterministic Unity/Editor API substitutes. Checks dirty-scene/import guards,
runtime-only profile copies, temporary progression isolation, constructor failure, manual stop,
runtime exception, wall-clock timeout, script reload, prior scene/time-scale restoration and
explicit FAIL/CANCELLED evidence. These are driver safety tests, not a native Unity run.

The separate PerformanceCaptureTests execute the real recording component/history/summary,
including supported/unavailable counters, separate CSV export and allocation-free accumulation.
Run the actual Editor verification from Docs/Native_Validation.md to obtain native evidence.
