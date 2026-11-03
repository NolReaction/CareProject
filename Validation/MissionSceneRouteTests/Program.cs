using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using IntercomMvp;

internal static class Program
{
    private static int checks;
    private static void Check(bool value, string label) { if (!value) throw new Exception(label); checks++; }
    private static float Number(string text) => float.Parse(text, CultureInfo.InvariantCulture);
    private static void Main()
    {
        IntercomNavGrid grid = Yard();
        string scene = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "IntercomMVP.unity"));
        var blocks = new Dictionary<string, string>();
        foreach (Match m in Regex.Matches(scene, @"^--- !u!\d+ &(-?\d+)\n(.*?)(?=^--- !u!|\z)", RegexOptions.Multiline | RegexOptions.Singleline))
            blocks.Add(m.Groups[1].Value, m.Groups[2].Value);
        var points = new List<IntercomNavPoint>();
        foreach (string name in new[] { "Mission Spawn 01", "Mission Spawn 02", "Mission Spawn 03", "Mission Spawn 04", "Mission Spawn 05",
            "Mission Spawn 06", "Mission Spawn 07", "Mission Spawn 08", "Mission Spawn 09", "Rescue Ally Spawn", "Rescue Extraction", "Robot" })
        {
            string go = null;
            foreach (var item in blocks)
                if (Regex.IsMatch(item.Value, @"^  m_Name: " + Regex.Escape(name) + "$", RegexOptions.Multiline)) go = item.Value;
            Check(go != null, "authored marker exists: " + name);
            string transform = blocks[Regex.Match(go, @"m_Component:\n  - component: \{fileID: (\d+)\}").Groups[1].Value];
            Match position = Regex.Match(transform, @"m_LocalPosition: \{x: ([^,]+), y: ([^,]+), z: ([^}]+)\}");
            float x = Number(position.Groups[1].Value), z = Number(position.Groups[3].Value);
            Check(grid.TryCell(x, z, out int cx, out int cz) && grid.IsWalkable(cx, cz), "marker lies in a free conservative cell: " + name);
            Check(grid.TryProject(x, z, .4f, out IntercomNavPoint projected), "marker projects without moving across obstacles: " + name);
            points.Add(projected);
        }
        var path = new List<IntercomNavPoint>(128);
        for (int a = 0; a < points.Count; a++)
            for (int b = 0; b < points.Count; b++)
            {
                Check(grid.FindPath(points[a].X, points[a].Z, points[b].X, points[b].Z, 0, path), "every mission marker reaches every other marker");
                float x = points[a].X, z = points[a].Z;
                foreach (IntercomNavPoint p in path)
                {
                    Check(grid.SegmentClear(x, z, p.X, p.Z), "smoothed mission route stays outside expanded obstacles");
                    x = p.X; z = p.Z;
                }
            }
        // Both sides of the ally remain accessible; a single narrow gap is not
        // the only authored approach to recruitment or escort.
        foreach (float dx in new[] { -1.6f, 1.6f })
        {
            var ally = points[9];
            Check(grid.TryProject(ally.X + dx, ally.Z, .4f, out IntercomNavPoint approach), "rescue has two clear approach points");
            Check(grid.FindPath(points[11].X, points[11].Z, approach.X, approach.Z, 0, path), "hero can reach both sides of rescue point");
            Check(grid.FindPath(approach.X, approach.Z, points[10].X, points[10].Z, 0, path), "both rescue approaches connect to extraction");
        }
        Console.WriteLine($"Mission scene routes: {checks} assertions passed; 12 real scene anchors, 144 pair routes and two rescue approaches using production grid. Not Unity PhysX.");
    }

    private static IntercomNavGrid Yard()
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "DistrictLayout.json")));
        var grid = new IntercomNavGrid(-18, -16, 36, 32, .4f);
        grid.BeginObstacles();
        foreach (var group in doc.RootElement.GetProperty("groups").EnumerateArray())
        {
            var gp = group.GetProperty("position");
            foreach (var b in group.GetProperty("blocks").EnumerateArray())
            {
                if (!b.GetProperty("collider").GetBoolean() || (b.TryGetProperty("active", out var active) && !active.GetBoolean())) continue;
                var p = b.GetProperty("position"); var scale = b.GetProperty("size");
                var center = b.TryGetProperty("collisionCenter", out var c) ? c : default;
                var size = b.TryGetProperty("collisionSize", out var s) ? s : default;
                double cx = center.ValueKind == JsonValueKind.Array ? center[0].GetDouble() * scale[0].GetDouble() : 0;
                double cy = center.ValueKind == JsonValueKind.Array ? center[1].GetDouble() * scale[1].GetDouble() : 0;
                double cz = center.ValueKind == JsonValueKind.Array ? center[2].GetDouble() * scale[2].GetDouble() : 0;
                double sx = (size.ValueKind == JsonValueKind.Array ? size[0].GetDouble() : 1) * scale[0].GetDouble();
                double sy = (size.ValueKind == JsonValueKind.Array ? size[1].GetDouble() : 1) * scale[1].GetDouble();
                double sz = (size.ValueKind == JsonValueKind.Array ? size[2].GetDouble() : 1) * scale[2].GetDouble();
                double y = gp[1].GetDouble() + p[1].GetDouble() + cy;
                if (y + sy / 2 <= .18 || y - sy / 2 >= 1.35) continue;
                double angle = b.GetProperty("yaw").GetDouble() * Math.PI / 180, cos = Math.Cos(angle), sin = Math.Sin(angle);
                double x = gp[0].GetDouble() + p[0].GetDouble() + cx * cos + cz * sin;
                double z = gp[2].GetDouble() + p[2].GetDouble() - cx * sin + cz * cos;
                double hx = (Math.Abs(cos) * sx + Math.Abs(sin) * sz) / 2, hz = (Math.Abs(sin) * sx + Math.Abs(cos) * sz) / 2;
                grid.AddObstacle((float)(x - hx), (float)(z - hz), (float)(x + hx), (float)(z + hz), .4f);
            }
        }
        foreach (var target in doc.RootElement.GetProperty("targets").EnumerateArray())
        {
            var p = target.GetProperty("position"); float x = p[0].GetSingle(), z = p[2].GetSingle();
            grid.AddObstacle(x - .475f, z - .225f, x + .475f, z + .225f, .4f);
        }
        grid.EndObstacles(); return grid;
    }
}
