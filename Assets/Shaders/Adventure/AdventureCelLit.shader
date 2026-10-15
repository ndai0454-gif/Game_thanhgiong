Shader "ThanhGiong/Adventure/Cel Rim Lit"
{
    Properties
    {
        _BaseMap("Authored albedo", 2D) = "white" {}
        _BaseColor("Authored tint", Color) = (1,1,1,1)
        _BumpMap("Normal", 2D) = "bump" {}
        _BumpScale("Normal strength", Float) = 1
        _MetallicGlossMap("Metallic", 2D) = "white" {}
        _Metallic("Metallic strength", Range(0,1)) = 0
        _Smoothness("Smoothness", Range(0,1)) = .3
        _EmissionMap("Emission", 2D) = "white" {}
        [HDR] _EmissionColor("Emission color", Color) = (0,0,0,1)
        _UseVertexColors("Use vertex colors", Float) = 0
        _ShadowTint("Shadow band", Color) = (.49,.54,.60,1)
        _BandThreshold("Light band threshold", Range(0,1)) = .48
        _BandSoftness("Band softness", Range(.01,.3)) = .08
        [HDR] _RimColor("Warm silhouette", Color) = (1,.77,.4,1)
        _RimIntensity("Rim strength", Range(0,2)) = .24
        _RimPower("Rim power", Float) = 3.5
        _Cutoff("Alpha cutoff", Range(0,1)) = .5
        [Toggle(_ALPHATEST_ON)] _AlphaClip("Cutout", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Cull [_Cull]
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
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma shader_feature_local _ALPHATEST_ON
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
            TEXTURE2D(_MetallicGlossMap); SAMPLER(sampler_MetallicGlossMap);
            TEXTURE2D(_EmissionMap); SAMPLER(sampler_EmissionMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST, _BaseColor, _EmissionColor, _ShadowTint, _RimColor;
                float _BumpScale, _Metallic, _Smoothness, _UseVertexColors;
                float _BandThreshold, _BandSoftness, _RimIntensity, _RimPower, _Cutoff;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float4 tangentOS:TANGENT; float2 uv:TEXCOORD0; float4 color:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float3 normalWS:TEXCOORD1; float4 tangentWS:TEXCOORD2; float2 uv:TEXCOORD3; half4 color:COLOR; half fog:TEXCOORD4; UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO };
            Varyings Vert(Attributes v)
            {
                Varyings o; UNITY_SETUP_INSTANCE_ID(v); UNITY_TRANSFER_INSTANCE_ID(v,o); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionWS=TransformObjectToWorld(v.positionOS.xyz); o.positionCS=TransformWorldToHClip(o.positionWS);
                o.normalWS=TransformObjectToWorldNormal(v.normalOS); o.tangentWS=float4(TransformObjectToWorldDir(v.tangentOS.xyz), v.tangentOS.w*GetOddNegativeScale());
                o.uv=TRANSFORM_TEX(v.uv,_BaseMap); o.color=lerp(1,v.color,_UseVertexColors); o.fog=ComputeFogFactor(o.positionCS.z); return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                half4 base=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv)*_BaseColor*i.color;
                #ifdef _ALPHATEST_ON
                clip(base.a-_Cutoff);
                #endif
                float3 n=normalize(i.normalWS);
                float3 tangent=normalize(i.tangentWS.xyz+float3(.00001,0,0));
                float3 bump=UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap,sampler_BumpMap,i.uv),_BumpScale);
                n=normalize(mul(bump,float3x3(tangent,cross(n,tangent)*i.tangentWS.w,n)));
                Light light=GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                float lambert=saturate(dot(n,light.direction));
                float band=smoothstep(_BandThreshold-_BandSoftness,_BandThreshold+_BandSoftness,lambert*light.shadowAttenuation);
                half3 ambient=max(SampleSH(n),half3(.18,.21,.24));
                half3 diffuse=base.rgb*(ambient*.68+lerp(_ShadowTint.rgb,light.color,band)*.88);
                #ifdef _ADDITIONAL_LIGHTS
                uint pixelLightCount = GetAdditionalLightsCount();
                for (uint lightIndex = 0u; lightIndex < pixelLightCount; ++lightIndex)
                {
                    Light addLight = GetAdditionalLight(lightIndex, i.positionWS);
                    float addNdotL = saturate(dot(n, addLight.direction) * 0.65 + 0.35);
                    diffuse += base.rgb * addLight.color * (addLight.distanceAttenuation * addLight.shadowAttenuation * addNdotL * 0.75);
                }
                #endif
                float3 view=SafeNormalize(GetCameraPositionWS()-i.positionWS);
                float rim=pow(saturate(1-dot(n,view)),_RimPower)*(.35+.65*lambert);
                float metal=SAMPLE_TEXTURE2D(_MetallicGlossMap,sampler_MetallicGlossMap,i.uv).r*_Metallic;
                float spec=pow(saturate(dot(n,SafeNormalize(light.direction+view))),lerp(16,96,_Smoothness))*light.shadowAttenuation;
                half3 color=diffuse+lerp(.035,base.rgb,metal)*spec*.65+_RimColor.rgb*rim*_RimIntensity;
                color+=SAMPLE_TEXTURE2D(_EmissionMap,sampler_EmissionMap,i.uv).rgb*_EmissionColor.rgb;
                return half4(MixFog(color,i.fog),base.a);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }
}
