Shader "ThanhGiong/OpenWorldBiomeGround"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                half4  color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3  normalWS   : TEXCOORD1;
                half4  color      : COLOR;
                float  fogFactor  : TEXCOORD2;
            };

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = Hash21(i);
                float b = Hash21(i + float2(1.0, 0.0));
                float c = Hash21(i + float2(0.0, 1.0));
                float d = Hash21(i + float2(1.0, 1.0));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs posInputs = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = posInputs.positionCS;
                o.positionWS = posInputs.positionWS;
                o.normalWS   = TransformObjectToWorldNormal(v.normalOS);
                o.color      = v.color;
                o.fogFactor  = ComputeFogFactor(posInputs.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half3 N = normalize(i.normalWS);
                float2 wp = i.positionWS.xz;

                // Multi-octave painterly BotW/TotK meadow variation
                float macroPatch = ValueNoise(wp * 0.035);
                float midBrush   = ValueNoise(wp * 0.16 + float2(macroPatch * 1.7, -macroPatch * 1.3));
                float microBlade = ValueNoise(wp * 1.35);

                half3 baseAlbedo = i.color.rgb;
                half3 sunlitMeadow = baseAlbedo * half3(1.16h, 1.18h, 0.88h);
                half3 coolClover   = baseAlbedo * half3(0.82h, 0.96h, 0.90h);
                half3 meadowCol    = lerp(coolClover, sunlitMeadow, smoothstep(0.25, 0.78, macroPatch * 0.65 + midBrush * 0.35));
                meadowCol *= lerp(0.93h, 1.07h, (half)microBlade);

                // Subtle wildflower & golden clover speckles in lush grassland
                float flowerNoise = ValueNoise(wp * 0.85 + 19.3);
                half flowerMask = saturate((flowerNoise - 0.82) * 5.5) * saturate(N.y * 2.0 - 1.0);
                meadowCol = lerp(meadowCol, half3(0.95h, 0.88h, 0.38h), flowerMask * 0.32h);

                // Slope-based karst limestone cliff blending
                half slope = 1.0h - saturate(N.y);
                half cliffMask = smoothstep(0.16h, 0.42h, slope);
                float rockStrata = ValueNoise(float2(wp.x * 0.18 + i.positionWS.y * 0.55, wp.y * 0.18));
                half3 cliffRock = lerp(half3(0.42h, 0.44h, 0.40h), half3(0.58h, 0.55h, 0.47h), (half)rockStrata);
                half3 surfaceAlbedo = lerp(meadowCol, cliffRock, cliffMask);

                // Drifting Zelda cloud shadow dapple across the plains
                float2 cloudUV = wp * 0.0085 + float2(_Time.y * 0.06, _Time.y * 0.035);
                float cloudDapple = lerp(0.86, 1.04, smoothstep(0.30, 0.72, ValueNoise(cloudUV)));

                // Main directional light + real-time shadow cascades
                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half ndl = saturate(dot(N, mainLight.direction));
                half lit = ndl * mainLight.shadowAttenuation * (half)cloudDapple;

                // 3-band Zelda cel-shaded terrain lighting with warm golden sunlight & cool sky-blue shadows
                half band1 = smoothstep(0.18h, 0.36h, lit);
                half band2 = smoothstep(0.58h, 0.82h, lit);
                half celLight = band1 * 0.72h + band2 * 0.28h;

                half3 shadowColor = surfaceAlbedo * half3(0.52h, 0.62h, 0.72h);
                half3 litColor    = surfaceAlbedo * mainLight.color * 1.08h;
                half3 color       = lerp(shadowColor, litColor, celLight);

                // Soft horizon aerial perspective
                color = MixFog(color, i.fogFactor);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }
    }
}