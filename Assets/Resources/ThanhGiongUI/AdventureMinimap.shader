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
                // Enhance brightness and contrast so terrain features, roads, and water are crisp and easily visible
                fixed3 enhanced = saturate(pow(terrain.rgb, 0.90) * 1.28);
                float vignette = 1.0 - smoothstep(0.40, 0.495, distanceFromCenter) * 0.35;
                return fixed4(enhanced * vignette, mask);
            }
            ENDCG
        }
    }
    Fallback Off
}
