using UnityEngine;
using UnityEngine.Rendering;

namespace CineScape
{
    public class CinemaSceneController : MonoBehaviour
    {
        public const float Width = 24, Depth = 30, ScreenWidth = 16;
        public const int SeatRows=9, SeatsPerRow=13;
        public const float FirstSeatRow=7, SeatRowSpacing=2.3f, SeatRowRise=.13f;
        public static float SeatingHeight(float z) => Mathf.Clamp((z-FirstSeatRow)/SeatRowSpacing,0,SeatRows-1)*SeatRowRise;
        public Vector3 ScreenCenter => new Vector3(0, 5.5f, 1.05f);
        public Transform VideoSurface { get; private set; }
        Material wall, seat, metal, floor, glow, trim, headrest;
        Mesh backMesh, cushionMesh, headMesh, armMesh;
        GameObject Soft(string name,Vector3 position,Mesh mesh,Material material)
        {
            var o=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));
            o.transform.SetParent(transform,false);o.transform.localPosition=position;
            o.GetComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=o.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;
            return o;
        }
        Material Mat(string name, Color color, bool unlit = false)
        {
            var template = Resources.Load<Material>(unlit ? "CinemaUnlit" : "CinemaLit");
            var m = template != null ? new Material(template) : new Material(Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit"));
            m.name = name; m.enableInstancing = true; m.SetColor("_BaseColor", color); return m;
        }
        GameObject Box(string name, Vector3 p, Vector3 size, Material material)
        {
            var o = GameObject.CreatePrimitive(PrimitiveType.Cube);
            o.name = name; o.transform.SetParent(transform, false); o.transform.localPosition = p; o.transform.localScale = size;
            o.GetComponent<Renderer>().sharedMaterial = material;
            o.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            Destroy(o.GetComponent<Collider>()); return o;
        }
        public void Generate()
        {
            wall = Mat("深色吸音墙", new Color(.11f,.125f,.15f));
            seat = Mat("酒红织物", new Color(.42f,.045f,.075f));
            headrest=Mat("座椅头枕",new Color(.52f,.07f,.105f));
            metal = Mat("深色扶手", new Color(.085f,.09f,.11f));
            floor = Mat("地毯", new Color(.14f,.105f,.085f));
            trim=Mat("墙面立体线条",new Color(.19f,.16f,.13f));
            glow = Mat("暖色通道灯", new Color(.75f,.37f,.12f), true);
            seat.SetFloat("_Smoothness",.24f);headrest.SetFloat("_Smoothness",.28f);
            backMesh=UpholsteredSeatMesh.Create(new Vector3(1,1.12f,.38f),.18f,true);
            cushionMesh=UpholsteredSeatMesh.Create(new Vector3(.96f,.3f,.88f),.145f);
            headMesh=UpholsteredSeatMesh.Create(new Vector3(.8f,.34f,.2f),.095f);
            armMesh=UpholsteredSeatMesh.Create(new Vector3(.19f,.21f,.86f),.095f);
            Box("地面", new Vector3(0,-.2f,15), new Vector3(24,.4f,30), floor);
            Box("天花板", new Vector3(0,11,15), new Vector3(24,.3f,30), wall);
            Box("前墙", new Vector3(0,5.5f,0), new Vector3(24,11,.4f), wall);
            Box("后墙", new Vector3(0,5.5f,30), new Vector3(24,11,.4f), wall);
            Box("舞台", new Vector3(0,.35f,2), new Vector3(19,.7f,3), metal);
            Box("银幕黑色边框", ScreenCenter + new Vector3(0,0,-.15f), new Vector3(16.6f,9.6f,.15f), metal);
            for(int j=0;j<6;j++)
            {
                float z=4+j*4.4f;
                Box("天花横梁",new Vector3(0,10.65f,z),new Vector3(23.5f,.3f,.32f),trim);
                Box("顶部间接灯带",new Vector3(0,10.42f,z),new Vector3(16,.035f,.08f),glow);
            }
            var fill=new GameObject("影院柔和顶光");fill.transform.SetParent(transform,false);
            fill.transform.localRotation=Quaternion.Euler(65,-25,0);
            var fillLight=fill.AddComponent<Light>();fillLight.type=LightType.Directional;
            fillLight.color=new Color(1,.88f,.76f);fillLight.intensity=.7f;fillLight.shadows=LightShadows.None;
            var screenMat = Mat("无光照银幕", Color.black, true);
            var screen = GameObject.CreatePrimitive(PrimitiveType.Quad);
            screen.name = "视频银幕"; screen.transform.SetParent(transform, false);
            screen.transform.localPosition = ScreenCenter; screen.transform.localRotation = Quaternion.Euler(0,180,0);
            screen.transform.localScale = new Vector3(16,9,1); screen.GetComponent<Renderer>().sharedMaterial = screenMat;
            Destroy(screen.GetComponent<Collider>()); VideoSurface = screen.transform;
            for(int side = -1; side <= 1; side += 2)
            {
                Box("侧墙", new Vector3(side*12,5.5f,15), new Vector3(.4f,11,30), wall);
                Box("侧通道", new Vector3(side*10,.03f,17), new Vector3(2,.06f,24), metal);
                Box("墙面横向腰线",new Vector3(side*11.6f,2.35f,15),new Vector3(.12f,.14f,29),trim);
                for(int j=0;j<7;j++)
                {
                    Box("吸音装饰板", new Vector3(side*11.7f,5,4+j*4), new Vector3(.18f,6,2.5f), metal);
                    Box("立体墙柱",new Vector3(side*11.5f,5.4f,2+j*4),new Vector3(.38f,10.2f,.3f),trim);
                    Box("暖色壁灯", new Vector3(side*11.5f,3,4+j*4), new Vector3(.15f,.3f,1), glow);
                    var lamp = new GameObject("无阴影辅助灯"); lamp.transform.SetParent(transform);
                    lamp.transform.position = new Vector3(side*10.7f,4,4+j*4);
                    var l = lamp.AddComponent<Light>(); l.type = LightType.Point; l.color = new Color(1,.66f,.35f);
                    l.intensity = 2.6f; l.range = 8; l.shadows = LightShadows.None;
                }
            }
            for(int row=0;row<SeatRows;row++)
            {
                float z=FirstSeatRow+row*SeatRowSpacing, y=row*SeatRowRise;
                Box("座椅平台",new Vector3(0,y-.08f,z),new Vector3(18,.16f,2.3f),floor);
                for(int side=-1;side<=1;side+=2)
                {
                    Box("两侧阶梯过道",new Vector3(side*10,y-.04f,z),new Vector3(2,.12f,SeatRowSpacing),floor);
                    Box("过道台阶灯",new Vector3(side*10,y+.025f,z-1.05f),new Vector3(1.6f,.025f,.05f),glow);
                }
                for(int col=0;col<SeatsPerRow;col++)
                {
                    float x=(col-(SeatsPerRow-1)*.5f)*1.24f;
                    // 中央为连续座位区，过道只在两侧。
                    Soft("坐垫",new Vector3(x,y+.58f,z),cushionMesh,seat);
                    var back=Soft("立体靠背",new Vector3(x,y+1.13f,z+.42f),backMesh,seat);
                    back.transform.localRotation=Quaternion.Euler(7,0,0);
                    var cushion=Soft("座椅头枕",new Vector3(x,y+1.43f,z+.18f),headMesh,headrest);
                    cushion.transform.localRotation=Quaternion.Euler(7,0,0);
                    Box("座椅基座",new Vector3(x,y+.24f,z),new Vector3(.55f,.48f,.5f),metal);
                    for(int s=-1;s<=1;s+=2) Soft("软包扶手",new Vector3(x+s*.52f,y+.82f,z),armMesh,headrest);
                }
            }
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.24f,.23f,.22f);
        }
        void OnDestroy()
        {
            foreach(var m in new[]{wall,seat,metal,floor,glow,trim,headrest}) if(m != null) Destroy(m);
            foreach(var mesh in new[]{backMesh,cushionMesh,headMesh,armMesh}) if(mesh!=null)Destroy(mesh);
        }
    }
}
