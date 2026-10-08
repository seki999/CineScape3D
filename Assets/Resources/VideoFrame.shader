Shader "Hidden/CineScape/VideoFrame"
{
    Properties { _MainTex ("Frame", 2D) = "black" {} }
    SubShader
    {
        Pass
        {
            ZTest Always Cull Off ZWrite Off
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float2 _Fit;
            fixed4 frag(v2f_img i) : SV_Target
            {
                float2 uv=(i.uv-0.5)/_Fit+0.5;
                if(any(uv<0)||any(uv>1)) return fixed4(0,0,0,1);
                uv.y=1-uv.y;
                return fixed4(tex2D(_MainTex,uv).rgb,1);
            }
            ENDCG
        }
    }
}
