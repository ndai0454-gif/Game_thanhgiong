Shader "Hidden/ThanhGiong/AdventureMinimap"
{
    Properties { _MainTex ("Terrain", 2D) = "white" {} }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Cull Off ZWrite Off ZTest Always
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            fixed4 frag(v2f_img input) : SV_Target
            {
                fixed4 terrain=tex2D(_MainTex,input.uv);
                float distanceFromCenter=length(input.uv-.5);
                float mask=1-smoothstep(.487,.5,distanceFromCenter);
                return fixed4(lerp(terrain.rgb,float3(.08,.17,.12),.13),mask);
            }
            ENDCG
        }
    }
    Fallback Off
}
