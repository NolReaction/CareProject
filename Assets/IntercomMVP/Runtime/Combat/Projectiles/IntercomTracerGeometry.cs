using UnityEngine;

namespace IntercomMvp
{
    /// <summary>Shared finite mesh with separate exposed amber and cream surfaces.</summary>
    public static class IntercomTracerGeometry
    {
        public static Mesh Create()
        {
            // A visible cream head and amber tail share one closed mesh.
            // The old inner core was hidden inside its opaque outer shell.
            const int sides = 8;
            float[] depths = { -1f, -0.62f, -0.26f, -0.07f, 0f };
            float[] radii = { 0.015f, 0.24f, 0.5f, 0.36f, 0.025f };
            Vector3[] vertices = new Vector3[sides * 5];
            int[] tail = new int[sides * 2 * 6 + (sides - 2) * 3];
            int[] head = new int[sides * 2 * 6 + (sides - 2) * 3];
            for (int ring = 0; ring < 5; ring++)
                for (int i = 0; i < sides; i++)
                {
                    float angle = i * Mathf.PI * 2f / sides;
                    vertices[ring * sides + i] = new Vector3(Mathf.Cos(angle) * radii[ring],
                        Mathf.Sin(angle) * radii[ring], depths[ring]);
                }
            int tailIndex = 0, headIndex = 0;
            for (int ring = 0; ring < 4; ring++)
                for (int i = 0; i < sides; i++)
                {
                    int a = ring * sides + i, b = ring * sides + (i + 1) % sides;
                    int c = a + sides, d = b + sides;
                    int[] indices = ring < 2 ? tail : head;
                    int n = ring < 2 ? tailIndex : headIndex;
                    indices[n++] = a; indices[n++] = b; indices[n++] = c;
                    indices[n++] = b; indices[n++] = d; indices[n++] = c;
                    if (ring < 2) tailIndex = n; else headIndex = n;
                }
            for (int i = 1; i < sides - 1; i++)
            {
                tail[tailIndex++] = 0; tail[tailIndex++] = i + 1; tail[tailIndex++] = i;
                head[headIndex++] = 32; head[headIndex++] = 32 + i; head[headIndex++] = 32 + i + 1;
            }
            Mesh mesh = new Mesh { name = "Intercom Chunky Cream-Tip Tracer", vertices = vertices, subMeshCount = 2 };
            mesh.SetTriangles(tail, 0); mesh.SetTriangles(head, 1);
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); mesh.UploadMeshData(true);
            return mesh;
        }

    }
}
