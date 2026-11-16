# Front end, receipts and magazine workshop

Compiles the production front end view/controller, shared layout and player input.
Mission, progression and Unity APIs are controlled substitutes: these tests check
UI routing and presentation, while their owners test actual reward/purchase rules.

The cases cover safe-area bounds, mouse/touch ownership, held-button cancellation,
result navigation, optional/missing/replaced services, insufficient parts, one
purchase per gesture, maximum rank, save failure, effective magazine capacity,
current-run reward receipts, immutable result facts and a free-play result without
rewards. New result/upgrade labels must remain above the exact shared action bounds.
Stable receipt strings retain their cached instances between unchanged repaints.

Actual GUI commands are exported to `Validation/Results/V20UI/FrontEnd`, including
affordable/insufficient/maximum upgrades and a complete reward receipt. Raster
previews are offline illustrations of those commands, not Unity rendering evidence.

Run with .NET 8: `dotnet run --project Validation/FrontEndRuntimeTests`.
