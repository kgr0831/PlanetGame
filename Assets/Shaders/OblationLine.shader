Shader "Oblation/Galaxy Line"
{
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha One
        ZWrite Off
        Cull Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionHCS:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
            float _OblationMotion;
            Varyings vert(Attributes i){Varyings o;o.positionHCS=TransformObjectToHClip(i.positionOS.xyz);o.color=i.color;o.uv=i.uv;return o;}
            half4 frag(Varyings i):SV_Target
            {
                float core=pow(saturate(1-abs(i.uv.y-.5)*2),2);
                float pulse=.9+.1*sin(i.uv.x*24-_Time.y*3*_OblationMotion);
                return half4(i.color.rgb*(1.2+core),i.color.a*core*pulse);
            }
            ENDHLSL
        }
    }
}
