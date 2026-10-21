Shader "ThanhGiong/Adventure/ZeldaStylizedWater"
{
    Properties
    {
        _ShallowColor("Shallow Jade Color", Color) = (0.16, 0.72, 0.68, 1)
        _DeepColor("Deep Cerulean Color", Color) = (0.07, 0.38, 0.56, 1)
        _FoamColor("Shoreline Foam Color", Color) = (0.94, 0.98, 1.0, 1)
        _CausticColor("Sunlit Caustic Color", Color) = (0.48, 0.92, 0.85, 1)
        _RippleSpeed("Ripple Speed", Float) = 0.85
        _RippleScale("Ripple Scale", Float) = 0.42
        _FoamWidth("Bank Foam Width", Range(0.02, 0.35)) = 0.14
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry+10" }
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
                float4 _ShallowColor, _DeepColor, _FoamColor, _CausticColor;
                float _RippleSpeed, _RippleScale, _FoamWidth;
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
                float3 localPos : TEXCOORD3;
                half fog : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float2 Hash22(float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
                return frac(sin(p) * 43758.5453);
            }

            float Voronoi(float2 x)
            {
                float2 n = floor(x);
                float2 f = frac(x);
                float md = 8.0;
                for (int j = -1; j <= 1; j++)
                {
                    for (int i = -1; i <= 1; i++)
                    {
                        float2 g = float2((float)i, (float)j);
                        float2 o = Hash22(n + g);
                        o = 0.5 + 0.45 * sin(_Time.y * _RippleSpeed * 1.6 + 6.2831 * o);
                        float2 r = g + o - f;
                        float d = dot(r, r);
                        md = min(md, d);
                    }
                }
                return sqrt(md);
            }

            Varyings Vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                float3 posWS = TransformObjectToWorld(v.positionOS.xyz);
                float wave = sin(posWS.x * 0.9 + _Time.y * 1.8) * cos(posWS.z * 1.1 - _Time.y * 1.4) * 0.018;
                posWS.y += wave;
                o.positionWS = posWS;
                o.positionCS = TransformWorldToHClip(posWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = v.uv;
                o.localPos = v.positionOS.xyz;
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float2 p = i.positionWS.xz * _RippleScale;
                float t = _Time.y * _RippleSpeed;

                float v1 = Voronoi(p + float2(t * 0.35, t * 0.22));
                float v2 = Voronoi(p * 1.45 - float2(t * 0.28, -t * 0.31));
                float caustic = smoothstep(0.42, 0.62, v1) * 0.65 + smoothstep(0.48, 0.68, v2) * 0.45;

                // Bank edge factor from local cube/cylinder coordinates so riverbanks and pond rims get natural Zelda shoreline foam
                float bankZ = abs(i.localPos.z) * 2.0;
                float bankRadial = length(i.localPos.xz) * 2.0;
                float edgeMetric = max(bankZ, saturate(bankRadial - 0.35));
                float waveShift = sin(i.positionWS.x * 1.3 + t * 2.4) * 0.035 + cos(i.positionWS.z * 1.5 - t * 2.1) * 0.035;
                float shallowFactor = smoothstep(0.35, 0.96, edgeMetric + waveShift);

                half3 waterCol = lerp(_DeepColor.rgb, _ShallowColor.rgb, shallowFactor);
                waterCol = lerp(waterCol, _CausticColor.rgb, saturate(caustic) * (0.28 + 0.22 * shallowFactor));

                // Crisp stylized shoreline foam band
                float foamLine = smoothstep(1.0 - _FoamWidth, 0.99, edgeMetric + waveShift * 1.4);
                float rippleCrest = smoothstep(0.66, 0.74, v1 * v2 * 1.65);
                float foam = saturate(foamLine * 0.85 + rippleCrest * 0.38);

                // Perturbed normal for sunlight reflection
                float3 n = normalize(float3(
                    sin(i.positionWS.x * 2.2 + t * 2.1) * 0.08,
                    1.0,
                    cos(i.positionWS.z * 2.5 - t * 1.9) * 0.08));

                Light light = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                float3 viewDir = SafeNormalize(GetCameraPositionWS() - i.positionWS);
                float fresnel = pow(1.0 - saturate(dot(n, viewDir)), 3.2);
                float3 halfVec = SafeNormalize(light.direction + viewDir);
                float spec = smoothstep(0.965, 0.992, dot(n, halfVec)) * light.shadowAttenuation;

                half3 skyReflect = half3(0.62, 0.84, 0.96);
                waterCol = lerp(waterCol, skyReflect, fresnel * 0.38);
                waterCol = lerp(waterCol, _FoamColor.rgb, foam);
                waterCol *= (0.72 + 0.28 * light.shadowAttenuation) * lerp(half3(1, 1, 1), light.color, 0.35);
                waterCol += light.color * spec * 0.85;

                return half4(MixFog(waterCol, i.fog), 1.0);
            }
            ENDHLSL
        }
    }
}
