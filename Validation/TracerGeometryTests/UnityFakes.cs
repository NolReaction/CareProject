using System;
using System.Collections.Generic;
namespace UnityEngine
{
    public struct Vector3 { public float x,y,z;public Vector3(float a,float b,float c){x=a;y=b;z=c;} }
    public static class Mathf {public const float PI=(float)Math.PI;public static float Sin(float x)=>(float)Math.Sin(x);public static float Cos(float x)=>(float)Math.Cos(x);}
    public class Mesh
    {
        public string name;public Vector3[] vertices;public int subMeshCount;
        private readonly Dictionary<int,int[]> triangles=new Dictionary<int,int[]>();
        public void SetTriangles(int[] indices,int submesh){triangles[submesh]=indices;}
        public int[] GetTriangles(int submesh)=>triangles[submesh];
        public void RecalculateNormals(){}public void RecalculateBounds(){}public void UploadMeshData(bool noLongerReadable){}
    }
}
