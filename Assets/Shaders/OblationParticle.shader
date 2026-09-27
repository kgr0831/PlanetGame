Shader "Oblation/Impact Particle"
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
            struct Varyings { float4 positionCS:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
            Varyings vert(Attributes i) { Varyings o; o.positionCS=TransformObjectToHClip(i.positionOS.xyz); o.color=i.color; o.uv=i.uv; return o; }
            half4 frag(Varyings i):SV_Target
            {
                float glow=pow(saturate(1-length(i.uv*2-1)),2);
                return half4(i.color.rgb*(1.5+glow*2),i.color.a*glow);
            }
            ENDHLSL
        }
    }
}
