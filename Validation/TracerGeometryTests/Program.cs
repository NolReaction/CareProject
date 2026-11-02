using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using IntercomMvp;
using UnityEngine;
class Program
{
    static int checks;
    static void Check(bool value,string reason){checks++;if(!value)throw new Exception(reason);}
    static void Main(string[] args)
    {
        Mesh mesh=IntercomTracerGeometry.Create();
        Check(mesh.subMeshCount==2,"separate exposed head/tail materials");
        Check(mesh.vertices.Length==40,"bounded shared geometry");
        var edges=new Dictionary<(int,int),int>();var directed=new Dictionary<(int,int),int>();
        for(int sub=0;sub<2;sub++)
        {
            int[] tris=mesh.GetTriangles(sub);Check(tris.Length%3==0,"whole triangles");
            for(int t=0;t<tris.Length;t+=3)
            {
                int ia=tris[t],ib=tris[t+1],ic=tris[t+2];
                Check(ia>=0&&ib>=0&&ic>=0&&ia<mesh.vertices.Length&&ib<mesh.vertices.Length&&ic<mesh.vertices.Length,"valid vertex indices");
                var a=mesh.vertices[ia];var b=mesh.vertices[ib];var c=mesh.vertices[ic];
                double ux=b.x-a.x,uy=b.y-a.y,uz=b.z-a.z,vx=c.x-a.x,vy=c.y-a.y,vz=c.z-a.z;
                double nx=uy*vz-uz*vy,ny=uz*vx-ux*vz,nz=ux*vy-uy*vx;
                Check(nx*nx+ny*ny+nz*nz>1e-12,"nondegenerate triangle");
                double cx=(a.x+b.x+c.x)/3.0,cy=(a.y+b.y+c.y)/3.0,cz=(a.z+b.z+c.z)/3.0;
                if(Math.Abs(a.z-b.z)<1e-7&&Math.Abs(a.z-c.z)<1e-7)
                    Check(a.z<-.99f?nz<0:nz>0,"end caps face outward");
                else Check(nx*cx+ny*cy>0,"side winding points outward");
                Check(sub==0?cz<=-.26+1e-6:cz>=-.26-1e-6,"cream head and amber tail share boundary, not overlapping shells");
                foreach(var e in new[]{(ia,ib),(ib,ic),(ic,ia)})
                {
                    var key=(Math.Min(e.Item1,e.Item2),Math.Max(e.Item1,e.Item2));edges[key]=edges.GetValueOrDefault(key)+1;
                    directed[e]=directed.GetValueOrDefault(e)+1;
                }
            }
        }
        foreach(var e in edges){Check(e.Value==2,"closed manifold mesh");Check(directed.GetValueOrDefault(e.Key)==1&&directed.GetValueOrDefault((e.Key.Item2,e.Key.Item1))==1,"edge orientation consistent");}
        foreach(var v in mesh.vertices)
        {
            Check(float.IsFinite(v.x)&&float.IsFinite(v.y)&&float.IsFinite(v.z),"finite vertices");
            Check(Math.Abs(v.x)<=.500001f&&Math.Abs(v.y)<=.500001f&&v.z>=-1.000001f&&v.z<=0,"tip never ahead of collision point");
        }
        if(args.Length==1)File.WriteAllText(args[0],JsonSerializer.Serialize(new{vertices=mesh.vertices.Select(v=>new[]{v.x,v.y,v.z}).ToArray(),tail=mesh.GetTriangles(0),head=mesh.GetTriangles(1),width=IntercomTracerStyle.Width(.15f),length=IntercomTracerStyle.Length(.68f,2f)}));
        Console.WriteLine("Tracer geometry: "+checks+" assertions passed. Executed production mesh construction with data-only Unity stand-ins; no Unity rendering.");
    }
}
