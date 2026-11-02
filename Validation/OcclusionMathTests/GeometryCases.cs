using System;
using System.IO;
using System.Numerics;
using System.Text.RegularExpressions;
using IntercomMvp;
using Point = IntercomMvp.IntercomOcclusionGeometry.Point;

internal static class GeometryCases
{
    private static int assertions;
    private static void Check(bool pass, string reason)
    { assertions++; if (!pass) throw new Exception(reason); }
    private static Point P(Vector3 v) => new Point(v.X, v.Y, v.Z);
    private static Vector3 V(Point p) => new Vector3(p.x, p.y, p.z);
    private static string Root()
    {
        foreach (string start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
            for (var d = new DirectoryInfo(start); d != null; d = d.Parent)
                if (Directory.Exists(Path.Combine(d.FullName, "Assets", "IntercomMVP"))) return d.FullName;
        throw new Exception("Run from the project folder so actual authored meshes can be checked.");
    }
    private static (Point[], int[]) Load(string name)
    {
        string data = File.ReadAllText(Path.Combine(Root(), "Assets/IntercomMVP/Art/PrototypeEnvironment/Modules", name + ".mesh"));
        byte[] vertices = Convert.FromHexString(Regex.Match(data, @"_typelessdata: (\w+)").Groups[1].Value);
        byte[] indices = Convert.FromHexString(Regex.Match(data, @"m_IndexBuffer: (\w+)").Groups[1].Value);
        int count = int.Parse(Regex.Match(data, @"m_VertexCount: (\d+)").Groups[1].Value);
        Check(vertices.Length == count * 32, "authored vertex stride remains position/normal/UV 32 bytes");
        var points = new Point[count]; var triangles = new int[indices.Length / 2];
        for (int i = 0; i < count; i++) points[i] = new Point(BitConverter.ToSingle(vertices, i * 32),
            BitConverter.ToSingle(vertices, i * 32 + 4), BitConverter.ToSingle(vertices, i * 32 + 8));
        for (int i = 0; i < triangles.Length; i++) triangles[i] = BitConverter.ToUInt16(indices, i * 2);
        return (points, triangles);
    }

    // Camera geometry independently reconstructed from the authored 35/10 degree
    // isometric camera and 1.680m capsule. Endpoints stay in front of its torso.
    private static readonly Vector3 Forward = new Vector3(.14224426f, -.57357644f, .80670728f);
    private static readonly Vector3 Right = new Vector3(.98480775f, 0f, -.17364818f);
    private static readonly Vector3 Up = new Vector3(.0996005f, .81915204f, .56486252f);
    private static float Extent(Vector3 axis, Vector3 extent)
        => Math.Abs(axis.X) * extent.X + Math.Abs(axis.Y) * extent.Y + Math.Abs(axis.Z) * extent.Z;
    private static IntercomOcclusionMath.Projection Project(Vector3 center, Vector3 extent, Vector3 camera)
    {
        Vector3 d = center - camera;
        Check(IntercomOcclusionMath.ProjectBox(Vector3.Dot(d, Right), Vector3.Dot(d, Up), Vector3.Dot(d, Forward),
            Extent(Right, extent), Extent(Up, extent), Extent(Forward, extent), true, 4.6f, 16f / 9f, .05f, out var p), "camera projection valid");
        return p;
    }
    private static (Vector3[], Vector3[], IntercomOcclusionMath.Projection, Vector3) Samples(Vector3 hero)
    {
        Vector3 camera = hero + new Vector3(0, .65f, 0) - Forward * 12.24f;
        Vector3 center = hero + new Vector3(.000963f, .8410953f + 1.6800449f * .1f, .07660083f);
        Vector3 extent = new Vector3(.30240807f * .75f + .015f, 1.6800449f * .275f + .015f, .30240807f * .75f + .015f);
        var projection = Project(center, extent, camera);
        Vector3[] start = new Vector3[9], end = new Vector3[9];
        float hx = Extent(Right, extent), hy = Extent(Up, extent);
        for (int y = 0; y < 3; y++) for (int x = 0; x < 3; x++)
        {
            Vector3 plane = center + Right * ((x - 1) * .64f * hx) + Up * ((y - 1) * .64f * hy);
            start[y * 3 + x] = plane - Forward * (Vector3.Dot(plane - camera, Forward) - .05f);
            end[y * 3 + x] = start[y * 3 + x] + Forward * (projection.nearDepth - .025f - .05f);
        }
        return (start, end, projection, camera);
    }
    private static int Covered(IntercomOcclusionGeometry mesh, Vector3 position, float yaw, (Vector3[], Vector3[], IntercomOcclusionMath.Projection, Vector3) rays)
    {
        int covered = 0;
        var inverse = Quaternion.CreateFromAxisAngle(Vector3.UnitY, -yaw * MathF.PI / 180);
        for (int i = 0; i < 9; i++)
            if (mesh.Intersects(P(Vector3.Transform(rays.Item1[i] - position, inverse)),
                P(Vector3.Transform(rays.Item2[i] - position, inverse)))) covered++;
        return covered;
    }
    private static bool IndependentTriangle(Vector3 start, Vector3 end, Vector3 a, Vector3 b, Vector3 c)
    {
        // Plane crossing followed by three oriented edge tests, independent of
        // production's Moller-Trumbore barycentric test and BVH traversal.
        Vector3 normal = Vector3.Cross(b - a, c - a), delta = end - start;
        double denominator = Vector3.Dot(normal, delta);
        if (Math.Abs(denominator) < 1e-9) return false;
        double t = Vector3.Dot(normal, a - start) / denominator;
        if (t < 0 || t > 1) return false;
        Vector3 p = start + delta * (float)t;
        return Vector3.Dot(Vector3.Cross(b - a, p - a), normal) >= 0
            && Vector3.Dot(Vector3.Cross(c - b, p - b), normal) >= 0
            && Vector3.Dot(Vector3.Cross(a - c, p - c), normal) >= 0;
    }

    internal static void Run()
    {
        var (vertices, indices) = Load("Warehouse");
        var mesh = new IntercomOcclusionGeometry(vertices, indices);
        var rays = Samples(new Vector3(-9, 0, 5.5f));
        Vector3 min = V(vertices[0]), max = min;
        foreach (Point vertex in vertices) { min = Vector3.Min(min, V(vertex)); max = Vector3.Max(max, V(vertex)); }
        var bounds = Project((min + max) * .5f + new Vector3(-9, 0, 10.8f), (max - min) * .5f, rays.Item4);
        Check(IntercomOcclusionMath.BlocksCore(bounds, rays.Item3, .45f), "reproduces OLD screenshot bug: unrelated AABB corners invent torso coverage");
        Check(Covered(mesh, new Vector3(-9, 0, 10.8f), 0, rays) == 0, "warehouse behind hero stays fully opaque");
        var (cratePoints, crateIndices) = Load("CargoCrate"); var crate = new IntercomOcclusionGeometry(cratePoints, crateIndices);
        Check(Covered(crate, new Vector3(-13, .05f, 7.45f), 8, rays) == 0, "left loading crate remains opaque");
        Check(Covered(crate, new Vector3(-5, .05f, 7.5f), -8, rays) == 0, "right loading crate remains opaque");
        var rearRays = Samples(new Vector3(-9, 0, 15.5f));
        Check(Covered(mesh, new Vector3(-9, 0, 10.8f), 0, rearRays) >= 5, "same warehouse in FRONT of hero must fade");
        Check(Covered(crate, new Vector3(-13, .05f, 7.45f), 8, rearRays) == 0, "nearby sibling crate is not a contributing occluder");
        Check(Covered(mesh, new Vector3(-9, 0, 10.8f), 0, Samples(new Vector3(-1, 0, 15.5f))) == 0, "walking out beside a faded building clears all rays");

        // Large combined mesh with a genuine hole: its bounds intersect, faces do not.
        var holePoints = new[] {new Point(-3,-3,3),new Point(-1,-3,3),new Point(-1,3,3),new Point(-3,3,3),
            new Point(1,-3,3),new Point(3,-3,3),new Point(3,3,3),new Point(1,3,3)};
        var hole = new IntercomOcclusionGeometry(holePoints, new[] {0,1,2,0,2,3,4,5,6,4,6,7});
        Check(!hole.Intersects(new Point(0,0,0),new Point(0,0,10)), "doorway gap is not replaced by a solid bounding box");
        Check(hole.Intersects(new Point(2,0,0),new Point(2,0,10)), "visible wall beside doorway occludes");
        Check(!hole.Intersects(new Point(2,0,0),new Point(2,0,2.99f)), "triangle beyond torso endpoint never occludes");
        Check(!hole.Intersects(new Point(2,0,3.01f),new Point(2,0,10)), "triangle behind near-plane start never occludes");
        Check(hole.Intersects(new Point(2,0,10),new Point(2,0,0)), "reverse face still closes a thin shell");

        var random = new Random(5011);
        for (int n = 0; n < 700; n++)
        {
            Vector3 a = new Vector3((float)random.NextDouble()*18-9,(float)random.NextDouble()*10-2,(float)random.NextDouble()*18-9);
            Vector3 b = new Vector3((float)random.NextDouble()*18-9,(float)random.NextDouble()*10-2,(float)random.NextDouble()*18-9);
            bool reference = false;
            for (int i = 0; i < indices.Length && !reference; i += 3)
                reference = IndependentTriangle(a,b,V(vertices[indices[i]]),V(vertices[indices[i+1]]),V(vertices[indices[i+2]]));
            Check(mesh.Intersects(P(a),P(b)) == reference, "cached BVH agrees with independent actual-mesh plane test " + n);
        }
        // Stable acquisition, a brief exit at an edge, full recovery, re-entry.
        var state = new IntercomOcclusionState();
        state.Update(true, 0, .02f, .10f, .24f, .20f, .25f);
        state.Update(true, .06f, .06f, .10f, .24f, .20f, .25f);
        Check(!state.Fading && state.Opacity == 1, "one short occlusion never flashes transparency");
        state.Update(false, .08f, .02f, .10f, .24f, .20f, .25f);
        state.Update(true, .11f, .03f, .10f, .24f, .20f, .25f);
        state.Update(true, .24f, .13f, .10f, .24f, .20f, .25f);
        state.Update(true, .34f, .10f, .10f, .24f, .20f, .25f);
        Check(state.Fading && state.Opacity == .25f, "sustained actual cover fades");
        state.Update(false, .45f, .11f, .10f, .24f, .20f, .25f);
        Check(state.Fading && state.Opacity == .25f, "brief edge clearing holds fade");
        state.Update(false, .60f, .15f, .10f, .24f, .20f, .25f);
        Check(!state.Fading && state.Opacity == 1f, "cleared sightline restores original opacity");
        state.Update(true, .62f, .02f, .10f, .24f, .20f, .25f);
        Check(!state.Fading, "re-entry reacquires rather than using a stale timestamp");
        state.Reset(); Check(!state.Fading && state.Opacity == 1, "disable/reset clears hysteresis");
        foreach (int fps in new[] {30,60,120})
        {
            var steady = new IntercomOcclusionState();
            for(int i=0;i<fps;i++) steady.Update(true,(float)i/fps,1f/fps,.1f,.24f,.2f,.25f);
            Check(steady.Fading && steady.Opacity == .25f, "steady opacity independent of frame rate " + fps);
            for(int i=0;i<fps;i++) steady.Update(false,1f+(float)i/fps,1f/fps,.1f,.24f,.2f,.25f);
            Check(!steady.Fading && steady.Opacity == 1f, "recovery independent of frame rate " + fps);
        }
        Console.WriteLine($"PASS V5 actual warehouse/crates/foreground/gap/finite-ray/hysteresis regression + 700 independent mesh ray comparisons, {assertions} assertions.");
    }
}
