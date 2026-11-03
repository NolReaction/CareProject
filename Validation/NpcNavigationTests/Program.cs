using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using IntercomMvp;

static class Program
{
    static int checks;
    static void Check(bool value,string label) { if(!value)throw new Exception(label);checks++; }
    static void Block(IntercomNavGrid g,int x,int z) => g.AddObstacle(x+.4f,z+.4f,x+.6f,z+.6f,0f);
    static void Validate(IntercomNavGrid g,float x,float z,List<IntercomNavPoint> path)
    {
        Check(path.Count>0,"nonempty successful route");
        foreach(var p in path) { Check(g.SegmentClear(x,z,p.X,p.Z),"every smoothed segment stays clear");x=p.X;z=p.Z; }
        Check(g.LastExpandedNodes<=g.Count,"bounded search workspace");
    }
    static bool Bfs(IntercomNavGrid g,int sx,int sz,int gx,int gz)
    {
        if(!g.IsWalkable(sx,sz)||!g.IsWalkable(gx,gz))return false;
        var seen=new bool[g.Count];var queue=new Queue<int>();seen[sz*g.Width+sx]=true;queue.Enqueue(sz*g.Width+sx);
        while(queue.Count>0)
        {
            int p=queue.Dequeue(),x=p%g.Width,z=p/g.Width;if(x==gx&&z==gz)return true;
            for(int oz=-1;oz<=1;oz++)for(int ox=-1;ox<=1;ox++)
            {
                if((ox==0&&oz==0)||!g.IsWalkable(x+ox,z+oz))continue;
                if(ox!=0&&oz!=0&&(!g.IsWalkable(x+ox,z)||!g.IsWalkable(x,z+oz)))continue;
                int next=(z+oz)*g.Width+x+ox;if(seen[next])continue;seen[next]=true;queue.Enqueue(next);
            }
        }
        return false;
    }
    static void Main()
    {
        var path=new List<IntercomNavPoint>(256);
        var g=new IntercomNavGrid(0,0,8,8,1);
        Check(g.FindPath(.5f,.5f,7.5f,7.5f,0,path)&&path.Count==1,"clear yard direct route");Validate(g,.5f,.5f,path);
        g.BeginObstacles();for(int z=0;z<7;z++)Block(g,3,z);Check(g.EndObstacles(),"wall changes map");
        uint version=g.Version;
        Check(!g.SegmentClear(.5f,.5f,7.5f,.5f),"wall blocks direct steering");
        Check(g.FindPath(.5f,.5f,7.5f,.5f,0,path),"wall detour found");Validate(g,.5f,.5f,path);
        Check(path.Count>1,"detour retains corners");
        g.BeginObstacles();for(int z=0;z<7;z++)Block(g,3,z);Check(!g.EndObstacles()&&g.Version==version,"same occupancy does not invalidate routes");
        g.BeginObstacles();for(int z=0;z<8;z++)Block(g,3,z);g.EndObstacles();
        Check(!g.FindPath(.5f,.5f,7.5f,.5f,0,path)&&path.Count==0,"unreachable target clears stale path");
        g.BeginObstacles();g.EndObstacles();Check(g.FindPath(.5f,.5f,7.5f,.5f,0,path)&&path.Count==1,"destroyed obstacle reopens route");
        Check(!g.FindPath(-.1f,.5f,3.5f,3.5f,2,path),"out of world start rejected");
        Check(!g.TryProject(8.1f,4,2,out _),"projection never pulls off-map point through perimeter");
        Check(!g.TryProject(float.NaN,4,1,out _),"nonfinite endpoint rejected");
        Check(!g.SegmentClear(0.5f,float.PositiveInfinity,2,2),"nonfinite segment rejected");

        var pinch=new IntercomNavGrid(0,0,4,4,1);pinch.BeginObstacles();Block(pinch,1,0);Block(pinch,0,1);pinch.EndObstacles();
        Check(!pinch.SegmentClear(.5f,.5f,1.5f,1.5f),"diagonal corner cannot cut");
        Check(!pinch.FindPath(.5f,.5f,2.5f,2.5f,0,path),"A star agrees corner is sealed");
        Check(!pinch.TryProject(1.5f,.5f,.4f,out _),"snap is bounded");
        Check(pinch.TryProject(1.5f,.5f,1f,out var snap)&&pinch.TryCell(snap.X,snap.Z,out int px,out int pz)&&pinch.IsWalkable(px,pz),"blocked goal snaps only to a free cell");
        Check(!pinch.FindPath(1.5f,.5f,2.5f,2.5f,1,path),"blocked start never teleports across an obstacle");

        var edge=new IntercomNavGrid(0,0,4,4,1);edge.BeginObstacles();Block(edge,0,1);edge.EndObstacles();
        Check(!edge.SegmentClear(1,.5f,1,3.5f),"line along blocked cell edge is rejected");
        Check(!edge.SegmentClear(1,3.5f,1,.5f),"edge test symmetric");
        Check(edge.SegmentClear(2,.5f,2,3.5f),"line along free edge remains legal");

        var nearWall=new IntercomNavGrid(0,0,10,10,.4f);nearWall.BeginObstacles();nearWall.AddObstacle(4,0,4.1f,10,.4f);nearWall.EndObstacles();
        Check(nearWall.FindPath(2,5,3.65f,5,1,path),"smaller player near wall can be approached via projected endpoint");
        Validate(nearWall,2,5,path);Check(path[path.Count-1].X<4,"near-wall projection stays on same side");
        Check(!nearWall.FindPath(2,5,4.45f,5,1,path),"projection does not bridge sealed wall to target on other side");

        var rng=new Random(74219);
        for(int map=0;map<40;map++)
        {
            var random=new IntercomNavGrid(0,0,15,12,1);random.BeginObstacles();
            for(int z=0;z<12;z++)for(int x=0;x<15;x++)if(rng.NextDouble()<.25)Block(random,x,z);
            random.EndObstacles();
            for(int query=0;query<30;query++)
            {
                int sx=rng.Next(15),sz=rng.Next(12),gx=rng.Next(15),gz=rng.Next(12);
                bool expected=Bfs(random,sx,sz,gx,gz);
                bool result=random.FindPath(sx+.5f,sz+.5f,gx+.5f,gz+.5f,0,path);
                Check(expected==result,"random path existence agrees with independent BFS");
                if(result)Validate(random,sx+.5f,sz+.5f,path);
            }
        }
        CheckYard(path);
        CheckService();
        CheckWorkBudget();
        Console.WriteLine($"NPC navigation: {checks} assertions passed. Production grid/service, 1200 independent BFS comparisons, authored V7 courtyard geometry. Narrow Unity stand-ins; not Unity physics.");
    }

    static void CheckWorkBudget()
    {
        var root=new UnityEngine.GameObject();
        var wall=new UnityEngine.GameObject();root.transform.children.Add(wall.transform);
        wall.AddComponent<UnityEngine.Collider>().bounds=new UnityEngine.Bounds(
            new UnityEngine.Vector3(7,0,0),new UnityEngine.Vector3(8,2,16));
        var nav=new UnityEngine.GameObject().AddComponent<IntercomNpcNavigation>();
        nav.obstacleRoot=root.transform;nav.worldMin=new UnityEngine.Vector2(0,0);nav.worldSize=new UnityEngine.Vector2(16,16);
        nav.maximumSearchesPerFrame=8;nav.maximumExpandedNodesPerFrame=64;nav.RebuildObstacleCache();
        var start=new UnityEngine.Vector3(1,0,1);var goal=new UnityEngine.Vector3(14,0,1);
        var output=new List<UnityEngine.Vector3>();UnityEngine.Time.frameCount++;
        Check(nav.TryFindPath(start,goal,output)==IntercomNavResult.Unreachable,"one expensive sealed-area search is completed accurately");
        int expanded=nav.ExpandedNodesThisFrame;
        Check(expanded>=64,"expensive search consumes soft node budget");
        output.Add(start);
        Check(nav.TryFindPath(start,goal,output)==IntercomNavResult.Deferred&&output.Count==1,
            "node budget defers later work without clearing the caller's existing route");
        Check(nav.ExpandedNodesThisFrame==expanded,"deferred request performs no extra node expansions");
        UnityEngine.Time.frameCount++;
        Check(nav.ExpandedNodesThisFrame==0,"new frame exposes fresh work budget");
        Check(nav.TryFindPath(start,goal,output)==IntercomNavResult.Unreachable,"deferred work can resume next frame");
    }

    static void CheckService()
    {
        var root=new UnityEngine.GameObject();
        UnityEngine.Collider Add(float minX,float minY,float minZ,float maxX,float maxY,float maxZ,bool trigger=false,bool controller=false)
        {
            var go=new UnityEngine.GameObject();root.transform.children.Add(go.transform);
            UnityEngine.Collider collider=controller?go.AddComponent<UnityEngine.CharacterController>():go.AddComponent<UnityEngine.Collider>();
            collider.bounds=new UnityEngine.Bounds(new UnityEngine.Vector3(minX,minY,minZ),new UnityEngine.Vector3(maxX,maxY,maxZ));collider.isTrigger=trigger;return collider;
        }
        var obstacle=Add(4,0,0,5,2,7);
        Add(-2,-.1f,-2,12,0,12); // floor
        Add(0,0,0,10,.1f,10); // walkable concrete pad
        Add(0,0,0,10,2,10,true); // trigger
        Add(0,0,0,10,2,10,false,true); // actor controller
        Add(0,4,0,10,5,10); // overhead geometry
        var nav=new UnityEngine.GameObject().AddComponent<IntercomNpcNavigation>();nav.obstacleRoot=root.transform;
        nav.worldMin=new UnityEngine.Vector2(0,0);nav.worldSize=new UnityEngine.Vector2(10,10);nav.RebuildObstacleCache();
        var a=new UnityEngine.Vector3(1,0,1);var b=new UnityEngine.Vector3(8,0,1);
        var output=new List<UnityEngine.Vector3>(256);
        Check(nav.IsReady,"explicit scene obstacle root activates navigation");
        Check(!nav.SegmentClear(a,b),"authored tall collider blocks navigation");
        Check(nav.TryFindPath(a,b,output)==IntercomNavResult.Success&&output.Count>1,"runtime ignores floor triggers actors and overhead");
        Check(nav.TryFindPath(a,b,output)==IntercomNavResult.Success,"second request accepted");int saved=output.Count;
        Check(nav.TryFindPath(a,b,output)==IntercomNavResult.Deferred&&output.Count==saved,"shared budget defers third request without destroying cached path");
        UnityEngine.Time.frameCount++;Check(nav.TryFindPath(a,b,output)==IntercomNavResult.Success,"new frame replenishes bounded request budget");
        uint version=nav.Version;UnityEngine.Time.unscaledTime=.21f;UnityEngine.Time.frameCount++;
        Check(nav.TryFindPath(a,b,output)==IntercomNavResult.Success&&nav.Version==version,"unchanged colliders preserve route revision");
        obstacle.enabled=false;UnityEngine.Time.unscaledTime=.42f;UnityEngine.Time.frameCount++;
        Check(nav.TryFindPath(a,b,output)==IntercomNavResult.Success&&output.Count==1&&nav.Version>version,"destroyed crate collider opens shared route");
        version=nav.Version;obstacle.enabled=true;UnityEngine.Time.unscaledTime=.63f;UnityEngine.Time.frameCount++;
        Check(nav.TryFindPath(a,b,output)==IntercomNavResult.Success&&output.Count>1&&nav.Version>version,"reset obstacle closes route again");
        UnityEngine.Time.timeScale=0;int before=output.Count;
        Check(nav.TryFindPath(a,b,output)==IntercomNavResult.Deferred&&output.Count==before,"paused request preserves path without advancing search");
        UnityEngine.Time.timeScale=1;
        obstacle.gameObject.activeInHierarchy=false;UnityEngine.Time.unscaledTime=.84f;UnityEngine.Time.frameCount++;
        Check(nav.TryFindPath(a,b,output)==IntercomNavResult.Success&&output.Count==1,"inactive authored obstacle stops blocking");
        for(int i=0;i<8;i++){UnityEngine.Time.frameCount++;nav.TryFindPath(a,b,output);}
        long allocated=GC.GetAllocatedBytesForCurrentThread();
        for(int i=0;i<100;i++){UnityEngine.Time.frameCount++;nav.TryFindPath(a,b,output);nav.SegmentClear(a,b);}
        Check(GC.GetAllocatedBytesForCurrentThread()-allocated==0,"steady-state path queries allocate no managed memory");
        nav.obstacleRoot=null;UnityEngine.Time.frameCount++;
        Check(nav.TryFindPath(a,b,output)==IntercomNavResult.Unreachable&&output.Count==0,"missing root fails closed instead of ignoring walls");
    }

    static void CheckYard(List<IntercomNavPoint> path)
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"DistrictLayout.json")));
        var g=new IntercomNavGrid(-18,-16,36,32,.4f);g.BeginObstacles();
        foreach(var group in doc.RootElement.GetProperty("groups").EnumerateArray())
        {
            var gp=group.GetProperty("position");
            foreach(var b in group.GetProperty("blocks").EnumerateArray())
            {
                if(!b.GetProperty("collider").GetBoolean()||(b.TryGetProperty("active",out var active)&&!active.GetBoolean()))continue;
                var p=b.GetProperty("position");var scale=b.GetProperty("size");
                var center=b.TryGetProperty("collisionCenter",out var c)?c:default;
                var size=b.TryGetProperty("collisionSize",out var s)?s:default;
                double cx=center.ValueKind==JsonValueKind.Array?center[0].GetDouble()*scale[0].GetDouble():0;
                double cy=center.ValueKind==JsonValueKind.Array?center[1].GetDouble()*scale[1].GetDouble():0;
                double cz=center.ValueKind==JsonValueKind.Array?center[2].GetDouble()*scale[2].GetDouble():0;
                double sx=(size.ValueKind==JsonValueKind.Array?size[0].GetDouble():1)*scale[0].GetDouble();
                double sy=(size.ValueKind==JsonValueKind.Array?size[1].GetDouble():1)*scale[1].GetDouble();
                double sz=(size.ValueKind==JsonValueKind.Array?size[2].GetDouble():1)*scale[2].GetDouble();
                double y=gp[1].GetDouble()+p[1].GetDouble()+cy;
                if(y+sy/2<=.18||y-sy/2>=1.35)continue;
                double angle=b.GetProperty("yaw").GetDouble()*Math.PI/180,cos=Math.Cos(angle),sin=Math.Sin(angle);
                double x=gp[0].GetDouble()+p[0].GetDouble()+cx*cos+cz*sin;
                double z=gp[2].GetDouble()+p[2].GetDouble()-cx*sin+cz*cos;
                double hx=(Math.Abs(cos)*sx+Math.Abs(sin)*sz)/2,hz=(Math.Abs(sin)*sx+Math.Abs(cos)*sz)/2;
                g.AddObstacle((float)(x-hx),(float)(z-hz),(float)(x+hx),(float)(z+hz),.4f);
            }
        }
        foreach(var t in doc.RootElement.GetProperty("targets").EnumerateArray())
        {var p=t.GetProperty("position");float x=p[0].GetSingle(),z=p[2].GetSingle();g.AddObstacle(x-.475f,z-.225f,x+.475f,z+.225f,.4f);}
        g.EndObstacles();
        var points=new(float x,float z)[]{(-6,-3),(5,1),(1,9),(0,-12),(0,0),(-9,2),(7,5),(0,12)};
        var projected=new IntercomNavPoint[points.Length];
        for(int i=0;i<points.Length;i++)Check(g.TryProject(points[i].x,points[i].z,1,out projected[i]),"courtyard spawn/route projects to navigable cell");
        for(int i=0;i<projected.Length;i++)for(int j=0;j<projected.Length;j++)
        {
            Check(g.FindPath(projected[i].X,projected[i].Z,projected[j].X,projected[j].Z,1,path),"authored courtyard anchors connected");
            Validate(g,projected[i].X,projected[i].Z,path);
        }
    }
}
