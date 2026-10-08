using UnityEngine;

namespace CineScape
{
    public class VideoScreenRenderer : MonoBehaviour
    {
        Material material;
        Material frameMaterial;
        public RenderTexture Texture { get; private set; }
        public void Initialize(Transform mesh)
        {
            material = mesh.GetComponent<Renderer>().sharedMaterial;
            // 固定 1080p 解码目标，FitInside 负责实际画面留黑；避免播放时反复分配。
            Texture = new RenderTexture(1920,1080,0,RenderTextureFormat.ARGB32);
            Texture.name = "Movie 1080p"; Texture.Create();
            material.SetTexture("_BaseMap", Texture);
            material.SetColor("_BaseColor", Color.white);
            Clear();
        }
        public void Clear()
        {
            var previous = RenderTexture.active;
            RenderTexture.active = Texture; GL.Clear(true,true,Color.black); RenderTexture.active = previous;
        }
        public void Present(Texture2D frame,float aspect)
        {
            if(frameMaterial==null) frameMaterial=new Material(Resources.Load<Shader>("VideoFrame"));
            float target=Texture.width/(float)Texture.height;
            frameMaterial.SetVector("_Fit",aspect>=target ? new Vector4(1,target/aspect,0,0) : new Vector4(aspect/target,1,0,0));
            Graphics.Blit(frame,Texture,frameMaterial);
        }
        void OnDestroy()
        {
            if(Texture != null) { Texture.Release(); Destroy(Texture); }
            if(material != null) Destroy(material);
            if(frameMaterial != null) Destroy(frameMaterial);
        }
    }
}
