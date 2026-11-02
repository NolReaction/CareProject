# Tracer mesh validation

`dotnet run --project Validation/TracerGeometryTests` executes the production tracer mesh
builder against data-only Mesh stand-ins. It verifies triangle indices, area, outward winding,
closed manifold edges, finite bounds, and exposed two-material regions. It does not render Unity.
An optional output filename writes the generated vertices/indices as JSON for external visual review.
