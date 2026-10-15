Shader "ThanhGiong/Adventure/ZeldaRoadPath"
{
    Properties
    {
        _BaseColor("Packed Earth Color", Color) = (0.64, 0.53, 0.36, 1)
        _StoneColor("Cobblestone Accent Color", Color) = (0.56, 0.54, 0.48, 1)
        _EdgeGrassColor("Verge Blend Color", Color) = (0.42, 0.54, 0.28, 1)
        _ShadowTint("Shadow Tint", Color) = (0.54, 0.58, 0.66, 1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="TransparentCutout" "Queue"="AlphaTest+15" }
        Cull Off
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor, _StoneColor, _EdgeGrassColor, _ShadowTint;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                half fog : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float Hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            float Noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(
                    lerp(Hash(i), Hash(i + float2(1.0, 0.0)), f.x),
                    lerp(Hash(i + float2(0.0, 1.0)), Hash(i + float2(1.0, 1.0)), f.x),
                    f.y);
            }

            Varyings Vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = v.uv;
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float2 w = i.positionWS.xz;
                float n1 = Noise(w * 0.25);
                float n2 = Noise(w * 0.85);
                float n3 = Noise(w * 2.8);

                // Distance from road centerline (0 at center, 1 at outer edge)
                float across = abs(i.uv.x * 2.0 - 1.0);
                float organicEdge = across + (n1 - 0.5) * 0.28 + (n2 - 0.5) * 0.14;
                clip(0.92 - organicEdge);

                // Worn cart/horse tracks & scattered stones
                float rut = exp(-pow((across - 0.34) * 5.5, 2.0));
                float stones = smoothstep(0.68, 0.82, n3) * (1.0 - across * 0.65);
                float verge = smoothstep(0.58, 0.90, organicEdge);

                half3 col = _BaseColor.rgb * (0.90 + 0.18 * n1 + 0.08 * n2);
                col *= lerp(1.0, 0.86, rut * 0.55);
                col = lerp(col, _StoneColor.rgb, stones * 0.55);
                col = lerp(col, _EdgeGrassColor.rgb, verge * 0.78);

                Light light = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                float3 n = normalize(i.normalWS);
                float ndotl = saturate(dot(n, light.direction));
                float cel = smoothstep(0.32, 0.48, ndotl * light.shadowAttenuation);
                half3 lit = col * (SampleSH(n) * 0.55 + lerp(_ShadowTint.rgb, light.color, cel) * 0.82);

                return half4(MixFog(lit, i.fog), 1.0);
            }
            ENDHLSL
        }
    }
}
