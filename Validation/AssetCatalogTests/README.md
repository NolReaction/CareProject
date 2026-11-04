# Asset catalog checks

This executable compiles the real editor `IntercomAssetCatalog` and typed project
navigation entries with a small filesystem substitute for Unity's AssetDatabase.

The checks cover GUID and canonical lookup, 1000 cached requests without rereading
JSON, manual resource and registry moves, project-change invalidation, separate
project roots, malformed/missing registries, safe fallback paths and parent-folder
creation. Lookup never schedules cleanup, moves resources or creates placeholders.

Run `dotnet run --project Validation/AssetCatalogTests` from the project root.
These are offline editor-logic checks; Unity's actual import and Editor UI require
opening the project in Unity.
