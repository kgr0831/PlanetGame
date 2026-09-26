Shader "Oblation/Conquest Halo"
{
    Properties
    {
        _OwnerColor("Owner", Color) = (0.2,0.8,1,1)
        _Progress("Conquest", Range(0,1)) = 1
        _ContenderColor("Contender", Color) = (1,0.2,0.35,1)
        _ContestedProgress("Contender conquest", Range(0,1)) = 0
        _Seed("Seed", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha One
        ZWrite Off
        Cull Back
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
            struct Varyings { float4 positionHCS:SV_POSITION; float3 normalWS:TEXCOORD0; float3 positionWS:TEXCOORD1; float3 objectPos:TEXCOORD2; };
            CBUFFER_START(UnityPerMaterial)
            float4 _OwnerColor, _ContenderColor; float _Progress, _ContestedProgress, _Seed;
            CBUFFER_END
            Varyings vert(Attributes i){Varyings o;VertexPositionInputs p=GetVertexPositionInputs(i.positionOS.xyz);o.positionHCS=p.positionCS;o.positionWS=p.positionWS;o.normalWS=TransformObjectToWorldNormal(i.normalOS);o.objectPos=i.positionOS.xyz;return o;}
            half4 frag(Varyings i):SV_Target
            {
                float3 n=normalize(i.normalWS),v=normalize(_WorldSpaceCameraPos-i.positionWS);
                float rim=pow(1-saturate(dot(n,v)),2.1);
                float angle=frac(atan2(i.objectPos.z,i.objectPos.x)/6.2831853+.5);
                float ownerFill=(1-smoothstep(_Progress,_Progress+.025,angle))*step(.0001,_Progress);
                float contenderFill=smoothstep(1-_ContestedProgress-.025,1-_ContestedProgress,angle)*step(.0001,_ContestedProgress);
                float fill=max(ownerFill,contenderFill);
                float scan=.7+.3*sin(angle*90-_Time.y*7+_Seed);
                float pulse=.72+.28*sin(_Time.y*3+_Seed);
                float alpha=rim*fill*scan*pulse;
                clip(alpha-.025);
                float3 claimColor=lerp(_OwnerColor.rgb,_ContenderColor.rgb,contenderFill);
                return half4(claimColor*(1.2+rim),alpha*.78);
            }
            ENDHLSL
        }
    }
}
