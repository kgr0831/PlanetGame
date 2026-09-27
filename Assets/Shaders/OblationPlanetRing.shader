Shader "Oblation/Planet Rings"
{
    Properties
    {
        _Color("Dust Color",Color)=(.6,.4,.2,1)
        _Seed("Seed",Float)=1
        _SceneFocus("Scene Focus",Float)=1
    }
    SubShader
    {
        Tags{"RenderType"="Transparent" "Queue"="Transparent-20" "RenderPipeline"="UniversalPipeline"}
        Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off
        Pass
        {
            Tags{"LightMode"="UniversalForward"}
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            struct A{float4 positionOS:POSITION;float2 uv:TEXCOORD0;float3 normalOS:NORMAL;};
            struct V{float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;float3 normalWS:TEXCOORD1;};
            CBUFFER_START(UnityPerMaterial)
            float4 _Color;float _Seed,_SceneFocus;
            CBUFFER_END
            V vert(A i){V o;o.positionCS=TransformObjectToHClip(i.positionOS.xyz);o.uv=i.uv;o.normalWS=TransformObjectToWorldNormal(i.normalOS);return o;}
            half4 frag(V i):SV_Target
            {
                float r=i.uv.x;
                float bands=lerp(.5,.5+.5*sin(r*230+sin(r*63+_Seed)*3),1-smoothstep(.35,1.4,fwidth(r)*230));
                float gap=1-smoothstep(.02,.035,abs(r-.57));
                float edge=smoothstep(0,.06,r)*(1-smoothstep(.86,1,r));
                Light sun=GetMainLight();float light=.3+.7*abs(dot(normalize(i.normalWS),sun.direction));
                float3 color=_Color.rgb*(.55+bands*.65)*light*_SceneFocus;
                return half4(color,edge*(.26+bands*.42)*(1-gap)*_Color.a);
            }
            ENDHLSL
        }
    }
}
