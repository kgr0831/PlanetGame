Shader "Oblation/Energy Pulse"
{
    Properties
    {
        [HDR] _Tint("Emission",Color)=(.2,2,4,1)
        _Age("Lifetime",Range(0,1))=0
        _Strength("Strength",Float)=1
    }
    SubShader
    {
        Tags{"RenderType"="Transparent" "Queue"="Transparent+20" "RenderPipeline"="UniversalPipeline"}
        Blend SrcAlpha One ZWrite Off Cull Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A{float4 positionOS:POSITION;float2 uv:TEXCOORD0;};
            struct V{float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;};
            CBUFFER_START(UnityPerMaterial)
            float4 _Tint;float _Age,_Strength;
            CBUFFER_END
            V vert(A i){V o;o.positionCS=TransformObjectToHClip(i.positionOS.xyz);o.uv=i.uv;return o;}
            half4 frag(V i):SV_Target
            {
                float2 p=i.uv*2-1;float r=length(p),a=atan2(p.y,p.x);
                float radius=lerp(.06,.92,1-pow(1-_Age,3));
                float ring=exp(-abs(r-radius)*95)*(1-_Age);
                float echo=exp(-abs(r-radius*.73)*70)*.35*(1-_Age);
                float flare=exp(-r*10)*pow(1-_Age,5)*1.4;
                float rays=pow(saturate(sin(a*19+r*24-_Age*14)),12)*exp(-r*4)*pow(1-_Age,2);
                float streak=exp(-abs(p.y)*90)*exp(-abs(p.x)*3)*pow(1-_Age,4);
                float alpha=(ring+echo+flare+rays*.5+streak)*_Strength*saturate((1-r)*15);
                return half4(_Tint.rgb,saturate(alpha));
            }
            ENDHLSL
        }
    }
}
