using UnityEngine;

namespace CineScape
{
    // 同一零件共享网格。圆角由真实曲面组成，法线连续，不使用方盒贴图伪装。
    public static class UpholsteredSeatMesh
    {
        public static Mesh Create(Vector3 size,float radius,bool archedBack=false)
        {
            const int steps=14;
            int count=6*(steps+1)*(steps+1);
            var vertices=new Vector3[count];var normals=new Vector3[count];var uv=new Vector2[count];
            var triangles=new int[6*steps*steps*6];
            var half=size*.5f;radius=Mathf.Min(radius,Mathf.Min(half.x,Mathf.Min(half.y,half.z)));
            var inner=half-Vector3.one*radius;
            Vector3[] axes={Vector3.right,Vector3.left,Vector3.up,Vector3.down,Vector3.forward,Vector3.back};
            int index=0;
            for(int face=0;face<6;face++)
            {
                var normal=axes[face];var horizontal=face<2?Vector3.forward:Vector3.right;
                var vertical=Vector3.Cross(normal,horizontal);
                int start=face*(steps+1)*(steps+1);
                for(int y=0;y<=steps;y++)for(int x=0;x<=steps;x++)
                {
                    var cube=normal+horizontal*(2f*x/steps-1)+vertical*(2f*y/steps-1);
                    var p=Vector3.Scale(cube,half);
                    var core=new Vector3(Mathf.Clamp(p.x,-inner.x,inner.x),Mathf.Clamp(p.y,-inner.y,inner.y),Mathf.Clamp(p.z,-inner.z,inner.z));
                    var n=(p-core).normalized;p=core+n*radius;
                    if(archedBack)
                    {
                        float nx=p.x/half.x,ny=p.y/half.y;
                        p.y+=.12f*(1-nx*nx)*Mathf.Clamp01(ny);
                        p.z+=Mathf.Sign(p.z)*.035f*(1-nx*nx)*(1-ny*ny);
                    }
                    int v=start+y*(steps+1)+x;vertices[v]=p;normals[v]=n;uv[v]=new Vector2((float)x/steps,(float)y/steps);
                    if(x<steps&&y<steps)
                    {
                        int a=v,b=v+1,c=v+steps+1,d=c+1;
                        triangles[index++]=a;triangles[index++]=b;triangles[index++]=d;
                        triangles[index++]=a;triangles[index++]=d;triangles[index++]=c;
                    }
                }
            }
            var mesh=new Mesh{name=archedBack?"圆角拱顶软包靠背":"圆角软包零件"};
            mesh.vertices=vertices;mesh.normals=normals;mesh.uv=uv;mesh.triangles=triangles;mesh.RecalculateBounds();
            return mesh;
        }
    }
}
