using UnityEngine;

namespace CineScape
{
    public class CameraDistanceController : MonoBehaviour
    {
        public float Value { get; private set; }
        public float Keystone { get; private set; } = 1;
        UnityEngine.Camera view;
        Vector3 screenCenter;
        float near, far, velocity, eyeHeight;
        public void Initialize(Vector3 center, float roomDepth, float screenWidth, float value, float seatedEyeHeight = 2.3f)
        {
            screenCenter = center;
            eyeHeight = Mathf.Clamp(seatedEyeHeight, 1, center.y);
            near = center.z + Mathf.Max(3f, screenWidth * .28f);
            far = center.z + roomDepth - 2f;
            far = Mathf.Max(near + 1, far);
            view=GetComponent<UnityEngine.Camera>();view.orthographic = false;
            Set(value);
            float z=Mathf.Lerp(far,near,Value);
            transform.position = new Vector3(0, eyeHeight+CinemaSceneController.SeatingHeight(z),z);
            ApplyProjection();
        }
        public void Set(float value) { Value = float.IsNaN(value) || float.IsInfinity(value) ? .5f : Mathf.Clamp01(value); }
        public void SetKeystone(float value)
        {
            Keystone=float.IsNaN(value)||float.IsInfinity(value)?1:Mathf.Clamp01(value);
            if(view!=null) ApplyProjection();
        }
        void ApplyProjection()
        {
            // 用偏轴透视保持坐姿视点：校正时让成像平面平行银幕，再平移光学中心。
            // 不改变视频纹理或银幕几何；100% 时四角是矩形，0% 保留原透视。
            var toCenter=screenCenter-transform.position;
            transform.rotation=Quaternion.Slerp(Quaternion.LookRotation(toCenter,Vector3.up),Quaternion.LookRotation(Vector3.back,Vector3.up),Keystone);
            var centerInView=view.worldToCameraMatrix.MultiplyPoint(screenCenter);
            float depth=Mathf.Max(.01f,-centerInView.z);
            var projection=Matrix4x4.Perspective(view.fieldOfView,view.aspect,view.nearClipPlane,view.farClipPlane);
            float scale=depth/Mathf.Max(.01f,toCenter.magnitude);
            projection.m00*=scale;projection.m11*=scale;
            projection.m02=projection.m00*centerInView.x/depth;
            projection.m12=projection.m11*centerInView.y/depth;
            view.projectionMatrix=projection;
        }
        void LateUpdate()
        {
            float z = Mathf.SmoothDamp(transform.position.z, Mathf.Lerp(far, near, Value), ref velocity, .09f);
            z=Mathf.Clamp(z,near,far);
            transform.position = new Vector3(0, eyeHeight+CinemaSceneController.SeatingHeight(z),z);
            ApplyProjection();
        }
        void OnDisable(){if(view!=null)view.ResetProjectionMatrix();}
    }
}
