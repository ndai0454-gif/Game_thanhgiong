Shader "ThanhGiong/Adventure/Interactive Foliage"
{
    Properties
    {
        _BaseMap("Authored texture", 2D) = "white" {}
        _BaseColor("Authored tint", Color) = (1,1,1,1)
        _UseVertexColors("Vertex colors", Float) = 0
        _RootColor("Ground blending", Color) = (.65,.77,.55,1)
        _TipColor("Sunlit tips", Color) = (1.06,1.08,.86,1)
        _UseUVHeight("Blade tip UV weight", Float) = 0
        _RootHeight("Object root", Float) = 0
        _BladeHeight("Flexible height", Float) = 1
        _WindStrength("Sway", Float) = .10
        _TrampleStrength("Trampling", Float) = .6
        _Visibility("Foreground visibility", Range(0,1)) = 1
        _MotionTime("Pause aware clock", Float) = 0
        [Toggle(_ALPHATEST_ON)] _AlphaClip("Cutout", Float) = 0
        _Cutoff("Alpha cutoff", Float) = .5
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Cull [_Cull]
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST, _BaseColor, _RootColor, _TipColor;
            float _UseVertexColors, _UseUVHeight, _RootHeight, _BladeHeight, _WindStrength, _TrampleStrength, _Visibility, _MotionTime, _Cutoff;
        CBUFFER_END
        float4 _AdventurePlayerPosition; // w: interaction radius; negative disables
        float4 _AdventureHorsePosition;
        float3 PushAway(float3 p,float4 actor,float weight)
        {
            float2 delta=p.xz-actor.xz; float dist=length(delta);
            float influence=saturate(1-dist/max(actor.w,.001))*step(.01,actor.w)*saturate(1-abs(p.y-actor.y)*.35);
            float push=influence*weight*_TrampleStrength;
            p.xz+=delta/max(dist,.04)*push; p.y-=push*.28; return p;
        }
        struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; float4 color:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
        struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float3 normalWS:TEXCOORD1; float2 uv:TEXCOORD2; float weight:TEXCOORD3; half4 color:COLOR; float fog:TEXCOORD4; UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO };
        Varyings Vert(Attributes v)
        {
            Varyings o; UNITY_SETUP_INSTANCE_ID(v); UNITY_TRANSFER_INSTANCE_ID(v,o); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
            float w=lerp(saturate((v.positionOS.y-_RootHeight)/max(_BladeHeight,.01)),saturate(v.uv.y),_UseUVHeight);
            float3 p=TransformObjectToWorld(v.positionOS.xyz);
            float t=_MotionTime;
            float phase=dot(p.xz,float2(.35,.21));
            float wave=sin(t*1.4+phase)*.7+sin(t*2.4-phase*.4)*.3;
            p.xz+=float2(.85,.52)*wave*w*w*_WindStrength;
            p=PushAway(p,_AdventurePlayerPosition,w); p=PushAway(p,_AdventureHorsePosition,w);
            o.positionWS=p; o.positionCS=TransformWorldToHClip(p); o.normalWS=TransformObjectToWorldNormal(v.normalOS);
            o.uv=TRANSFORM_TEX(v.uv,_BaseMap); o.weight=w; o.color=lerp(1,v.color,_UseVertexColors); o.fog=ComputeFogFactor(o.positionCS.z); return o;
        }
        half4 SampleBase(Varyings i)
        {
            half4 col=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv)*_BaseColor*i.color;
            #ifdef _ALPHATEST_ON
            clip(col.a-_Cutoff);
            #endif
            clip(_Visibility-frac(sin(dot(floor(i.positionCS.xy),float2(127.1,311.7)))*43758.5453));
            return col;
        }
        ENDHLSL
        Pass
        {
            Name "ForwardLit" Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma shader_feature_local _ALPHATEST_ON
            half4 Frag(Varyings i):SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                half4 c=SampleBase(i); float3 n=normalize(i.normalWS);
                Light light=GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                float diffuse=saturate(dot(n,light.direction)*.5+.5)*light.shadowAttenuation;
                half3 illumination=max(SampleSH(n)*.65,half3(.17,.19,.14))+light.color*(.38+.42*diffuse);
                half3 color=c.rgb*lerp(_RootColor.rgb,_TipColor.rgb,i.weight)*illumination;
                return half4(MixFog(color,i.fog),c.a);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster" Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing
            #pragma shader_feature_local _ALPHATEST_ON
            half4 DepthFrag(Varyings i):SV_Target { SampleBase(i); return 0; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly" Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing
            #pragma shader_feature_local _ALPHATEST_ON
            half4 DepthFrag(Varyings i):SV_Target { SampleBase(i); return 0; }
            ENDHLSL
        }
    }
}
