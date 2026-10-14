Shader "ThanhGiong/StylizedFoliageWind"
{
    Properties
    {
        [Header(Base Texture and Tint)]
        _BaseMap("Texture Atlas / Base Map", 2D) = "white" {}
        _BaseColor("Base Tint", Color) = (1, 1, 1, 1)

        [Header(Painterly Gradients)]
        _CanopyColor("Sunlit Crown Tint (Top)", Color) = (0.92, 0.98, 0.72, 1)
        _UndergrowthColor("Undergrowth / Root Tint (Bottom)", Color) = (0.28, 0.40, 0.22, 1)
        _HeightGradientScale("Gradient Height Scale", Float) = 4.0
        _HeightGradientOffset("Gradient Height Offset", Float) = 0.0

        [Header(Soft Lighting and Transmission)]
        _WrapLighting("Diffuse Light Wrap (0=Hard, 1=Soft)", Range(0, 1)) = 0.55
        _TransmissionColor("Subsurface Backlight Tint", Color) = (0.78, 0.88, 0.35, 1)
        _TransmissionIntensity("Subsurface Glow Intensity", Range(0, 2)) = 0.65
        _AmbientBoost("Ambient Forest Glow", Range(0, 1)) = 0.35

        [Header(Velvet Rim Light)]
        _RimColor("Rim Edge Tint", Color) = (0.95, 0.98, 0.75, 1)
        _RimPower("Rim Sharpness", Range(1, 8)) = 3.5
        _RimIntensity("Rim Intensity", Range(0, 2)) = 0.45

        [Header(Wind Motion Animation)]
        _WindSpeed("Wind Speed", Range(0, 5)) = 1.5
        _WindTurbulence("Turbulence / Gust Frequency", Range(0, 5)) = 2.2
        _TrunkSway("Trunk Sway Amplitude", Range(0, 0.5)) = 0.075
        _LeafFlutter("Leaf / Branch Flutter Amplitude", Range(0, 0.2)) = 0.035
        _WindDirection("Wind Direction (XZ)", Vector) = (0.85, 0, 0.52, 0)
        _WindHeightThreshold("Min Height for Trunk Sway", Float) = 0.2
        _MotionTime("Pause Aware Clock Override", Float) = 0

        [Header(Rendering)]
        [Toggle(_ALPHATEST_ON)] _AlphaClip("Alpha Clip (Cutout)", Float) = 0
        _Cutoff("Alpha Cutoff Threshold", Range(0, 1)) = 0.5
        _Visibility("Foreground dither visibility", Range(0, 1)) = 1
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull Mode", Float) = 0
    }

    SubShader
    {
        Tags 
        { 
            "RenderPipeline" = "UniversalPipeline" 
            "RenderType" = "Opaque" 
            "Queue" = "Geometry" 
        }

        Cull [_Cull]

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float4 _CanopyColor;
                float4 _UndergrowthColor;
                float4 _TransmissionColor;
                float4 _RimColor;
                float4 _WindDirection;
                float _HeightGradientScale;
                float _HeightGradientOffset;
                float _WrapLighting;
                float _TransmissionIntensity;
                float _AmbientBoost;
                float _RimPower;
                float _RimIntensity;
                float _WindSpeed;
                float _WindTurbulence;
                float _TrunkSway;
                float _LeafFlutter;
                float _WindHeightThreshold;
                float _MotionTime;
                float _Cutoff;
                float _Visibility;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float4 vertexColor : COLOR;
                float heightGradient : TEXCOORD3;
                float fogFactor : TEXCOORD4;
            };

            // Organic Wind Displacement
            float3 ApplyWind(float3 posOS, float3 normOS, float3 posWS, out float heightFactor)
            {
                float h = max(0.0, posOS.y - _WindHeightThreshold);
                heightFactor = saturate((posOS.y - _HeightGradientOffset) / max(_HeightGradientScale, 0.1));

                float2 windDir = normalize(_WindDirection.xz + float2(0.001, 0.001));
                float rawTime = (_MotionTime > 0.0001) ? _MotionTime : _Time.y;
                float windTime = rawTime * _WindSpeed;

                // Traveling spatial wave across the forest floor
                float wavePhase = dot(posWS.xz, windDir) * 0.35;
                
                // Multi-frequency wind gust wave
                float gust = sin(windTime * 1.1 + wavePhase) * 0.65
                           + sin(windTime * 2.3 - wavePhase * 0.6) * 0.35;
                
                // Trunk bend (anchored at root, flexing with height squared)
                float trunkBend = (h * h) * 0.04 * _TrunkSway * gust;
                posOS.xz += windDir * trunkBend;

                // Micro leaf & branch flutter
                float flutterPhase = dot(posOS, float3(9.1, 5.7, 11.3)) + wavePhase * 2.0;
                float flutter = sin(windTime * _WindTurbulence * 2.8 + flutterPhase)
                              * cos(windTime * _WindTurbulence * 1.6 + posOS.y * 3.5);
                
                float leafMotion = flutter * _LeafFlutter * saturate(h * 0.5);
                posOS += normOS * leafMotion;

                return posOS;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                
                float3 initialWS = TransformObjectToWorld(input.positionOS.xyz);
                float heightFactor = 0;
                float3 displacedOS = ApplyWind(input.positionOS.xyz, input.normalOS, initialWS, heightFactor);

                output.positionWS = TransformObjectToWorld(displacedOS);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.vertexColor = input.color;
                output.heightGradient = heightFactor;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                clip(_Visibility - frac(sin(dot(floor(input.positionCS.xy), float2(127.1, 311.7))) * 43758.5453));
                half4 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;

                #if defined(_ALPHATEST_ON)
                    clip(texColor.a - _Cutoff);
                #endif

                // Height gradient blend: undergrowth moss at base -> sun-kissed golden green at top
                half3 gradientTint = lerp(_UndergrowthColor.rgb, _CanopyColor.rgb, input.heightGradient);
                texColor.rgb *= gradientTint;

                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = normalize(GetCameraPositionWS() - input.positionWS);

                // Main directional sunlight with soft shadow calculation
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                // 1. Soft Half-Lambert Diffuse Wrap (removes harsh faceted plastic look)
                float NdotL = dot(normalWS, mainLight.direction);
                float wrappedNdotL = saturate((NdotL + _WrapLighting) / (1.0 + _WrapLighting));
                float shadowAtten = mainLight.shadowAttenuation;
                float directDiffuse = wrappedNdotL * shadowAtten;

                // 2. Subsurface Scattering / Leaf Backlight Transmission
                // When looking towards light source through foliage, leaves glow warmly
                float backNdotL = saturate(dot(-normalWS, mainLight.direction) + 0.35);
                float viewDotLight = saturate(dot(-viewDirWS, mainLight.direction));
                float sssFactor = backNdotL * pow(viewDotLight, 2.0) * _TransmissionIntensity * shadowAtten;
                half3 transmission = _TransmissionColor.rgb * sssFactor * mainLight.color;

                // 3. Velvet Micro-Fiber Rim Lighting (replaces cheap plastic specular glints)
                float NdotV = saturate(dot(normalWS, viewDirWS));
                float fresnel = 1.0 - NdotV;
                float rim = pow(fresnel, _RimPower) * _RimIntensity;
                half3 velvetRim = _RimColor.rgb * rim * (wrappedNdotL * 0.4 + 0.6);

                // 4. Ambient Forest Light with spherical harmonics
                half3 ambientSH = SampleSH(normalWS);
                half3 forestAmbient = ambientSH + half3(0.18, 0.24, 0.16) * _AmbientBoost;

                // Combine light stages
                half3 directLight = mainLight.color * directDiffuse;
                half3 totalLight = directLight + forestAmbient;

                half3 finalColor = texColor.rgb * totalLight + transmission + velvetRim;

                // Fog integration
                finalColor = MixFog(finalColor, input.fogFactor);

                return half4(finalColor, texColor.a);
            }
            ENDHLSL
        }

        // Shadow Caster Pass (Displaces shadow geometry with wind so ground shadows sway too!)
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex vertShadow
            #pragma fragment fragShadow
            #pragma shader_feature_local _ALPHATEST_ON

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float4 _CanopyColor;
                float4 _UndergrowthColor;
                float4 _TransmissionColor;
                float4 _RimColor;
                float4 _WindDirection;
                float _HeightGradientScale;
                float _HeightGradientOffset;
                float _WrapLighting;
                float _TransmissionIntensity;
                float _AmbientBoost;
                float _RimPower;
                float _RimIntensity;
                float _WindSpeed;
                float _WindTurbulence;
                float _TrunkSway;
                float _LeafFlutter;
                float _WindHeightThreshold;
                float _MotionTime;
                float _Cutoff;
                float _Visibility;
            CBUFFER_END

            struct AttributesShadow
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct VaryingsShadow
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            float3 ApplyWindShadow(float3 posOS, float3 normOS, float3 posWS)
            {
                float h = max(0.0, posOS.y - _WindHeightThreshold);
                float2 windDir = normalize(_WindDirection.xz + float2(0.001, 0.001));
                float rawTime = (_MotionTime > 0.0001) ? _MotionTime : _Time.y;
                float windTime = rawTime * _WindSpeed;
                float wavePhase = dot(posWS.xz, windDir) * 0.35;
                float gust = sin(windTime * 1.1 + wavePhase) * 0.65 + sin(windTime * 2.3 - wavePhase * 0.6) * 0.35;
                float trunkBend = (h * h) * 0.04 * _TrunkSway * gust;
                posOS.xz += windDir * trunkBend;
                float flutterPhase = dot(posOS, float3(9.1, 5.7, 11.3)) + wavePhase * 2.0;
                float flutter = sin(windTime * _WindTurbulence * 2.8 + flutterPhase) * cos(windTime * _WindTurbulence * 1.6 + posOS.y * 3.5);
                posOS += normOS * (flutter * _LeafFlutter * saturate(h * 0.5));
                return posOS;
            }

            VaryingsShadow vertShadow(AttributesShadow input)
            {
                VaryingsShadow output;
                float3 initialWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 displacedOS = ApplyWindShadow(input.positionOS.xyz, input.normalOS, initialWS);
                float3 positionWS = TransformObjectToWorld(displacedOS);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _MainLightPosition.xyz));
                #if UNITY_REVERSED_Z
                    output.positionCS.z = min(output.positionCS.z, output.positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #else
                    output.positionCS.z = max(output.positionCS.z, output.positionCS.w * UNITY_NEAR_CLIP_VALUE);
                #endif
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 fragShadow(VaryingsShadow input) : SV_Target
            {
                #if defined(_ALPHATEST_ON)
                    half4 texColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                    clip(texColor.a - _Cutoff);
                #endif
                return 0;
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
