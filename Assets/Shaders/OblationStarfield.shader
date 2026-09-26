Shader "Oblation/Starfield"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Background" "RenderPipeline"="UniversalPipeline" }
        Cull Front
        ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS:POSITION; };
            struct Varyings { float4 positionHCS:SV_POSITION; float3 dir:TEXCOORD0; };
            float hash(float3 p){return frac(sin(dot(p,float3(17.17,71.53,43.21)))*43758.5453);}
            Varyings vert(Attributes i){Varyings o;o.positionHCS=TransformObjectToHClip(i.positionOS.xyz);o.dir=normalize(i.positionOS.xyz);return o;}
            half4 frag(Varyings i):SV_Target
            {
                float3 d=normalize(i.dir);
                float3 cell=floor(d*420);
                float h=hash(cell);
                float star=smoothstep(.994,1,h)*pow(saturate(sin(h*994+_Time.y*h*.12)*.5+.5),6)*5;
                float nebula=pow(saturate(sin(d.x*5+d.z*3+sin(d.y*7))*0.5+0.5),5)*.08;
                float horizon=pow(1-abs(d.y),10)*.055;
                float3 color=float3(.003,.006,.018)+float3(.04,.11,.23)*nebula+float3(.22,.07,.3)*horizon;
                color+=star*lerp(float3(.45,.72,1),float3(1,.65,.35),h);
                return half4(color,1);
            }
            ENDHLSL
        }
    }
}
