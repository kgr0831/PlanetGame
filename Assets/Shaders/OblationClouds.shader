Shader "Oblation/Planet Clouds"
{
    Properties
    {
        _Seed("Planet Seed",Float)=1
        _CloudCoverage("Cloud Threshold",Range(.35,.7))=.53
        _SceneFocus("Scene Focus",Range(0,1))=1
        _CloudTint("Cloud Tint",Color)=(.66,.78,.9,1)
        _CloudOpacity("Cloud Opacity",Range(0,1))=.3
    }
    SubShader
    {
        Tags{"RenderType"="Transparent" "Queue"="Transparent-10" "RenderPipeline"="UniversalPipeline"}
        Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Back
        Pass
        {
            Tags{"LightMode"="UniversalForward"}
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "OblationPlanetSurface.hlsl"
            struct A{float4 positionOS:POSITION;float3 normalOS:NORMAL;};
            struct V{float4 positionCS:SV_POSITION;float3 normalWS:TEXCOORD0;float3 objectPos:TEXCOORD1;};
            CBUFFER_START(UnityPerMaterial)
            float _Seed,_CloudCoverage,_SceneFocus,_CloudOpacity;
            float4 _CloudTint;
            CBUFFER_END
            float _OblationMotion;
            V vert(A i){V o;o.positionCS=TransformObjectToHClip(i.positionOS.xyz);o.normalWS=TransformObjectToWorldNormal(i.normalOS);o.objectPos=i.positionOS.xyz;return o;}
            half4 frag(V i):SV_Target
            {
                float3 p=normalize(i.objectPos),n=normalize(i.normalWS);
                float footprint=max(length(ddx(p)),length(ddy(p)));
                float density=PlanetClouds(p,_Seed,footprint,_Time.y*.008*_OblationMotion,_CloudCoverage);
                Light sun=GetMainLight();float light=saturate(dot(n,sun.direction));
                float luminance=dot(sun.color,float3(.2126,.7152,.0722));
                float3 sunlight=lerp(sun.color,luminance.xxx,.7);
                float3 color=_CloudTint.rgb*(SampleSH(n)*.55+sunlight*(.05+light*.9));
                return half4(color*_SceneFocus,density*_CloudOpacity);
            }
            ENDHLSL
        }
    }
}
