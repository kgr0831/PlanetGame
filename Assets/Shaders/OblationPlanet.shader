Shader "Oblation/Procedural Planet"
{
    Properties
    {
        _BaseColor("Ocean", Color) = (0.04,0.12,0.25,1)
        _AccentColor("Continents", Color) = (0.35,0.72,0.8,1)
        _OwnerColor("Owner", Color) = (0.5,0.5,0.5,1)
        _Seed("Seed", Float) = 1
        _Selected("Selected", Range(0,1)) = 0
        _Emission("Emission", Range(0,5)) = 0.4
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionHCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 positionWS : TEXCOORD1; float3 objectPos : TEXCOORD2; };

            CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor, _AccentColor, _OwnerColor;
            float _Seed, _Selected, _Emission;
            CBUFFER_END

            float hash31(float3 p) { return frac(sin(dot(p, float3(127.1,311.7,74.7))) * 43758.5453); }
            float noise(float3 p)
            {
                float3 i=floor(p), f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(lerp(hash31(i),hash31(i+float3(1,0,0)),f.x),lerp(hash31(i+float3(0,1,0)),hash31(i+float3(1,1,0)),f.x),f.y),lerp(lerp(hash31(i+float3(0,0,1)),hash31(i+float3(1,0,1)),f.x),lerp(hash31(i+float3(0,1,1)),hash31(i+1),f.x),f.y),f.z);
            }
            float fbm(float3 p) { float n=0,a=.55; [unroll] for(int i=0;i<4;i++){n+=noise(p)*a;p=p*2.03+17.1;a*=.48;} return n; }

            Varyings vert(Attributes input)
            {
                Varyings o;
                VertexPositionInputs pos=GetVertexPositionInputs(input.positionOS.xyz);
                o.positionHCS=pos.positionCS; o.positionWS=pos.positionWS;
                o.normalWS=TransformObjectToWorldNormal(input.normalOS); o.objectPos=input.positionOS.xyz;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 n=normalize(i.normalWS), v=normalize(_WorldSpaceCameraPos-i.positionWS);
                float terrain=fbm(i.objectPos*3.2+_Seed);
                float ridges=fbm(i.objectPos*8.0-_Seed*.31);
                float land=smoothstep(.48,.58,terrain+ridges*.15);
                float3 color=lerp(_BaseColor.rgb,_AccentColor.rgb,land);
                float sunlight=.22+.78*saturate(dot(n,normalize(float3(-.4,.8,-.3))));
                float rim=pow(1-saturate(dot(n,v)),3.2);
                float latitude=abs(sin((i.objectPos.y+terrain*.08)*38+_Time.y*.12));
                color*=sunlight; color+=_AccentColor.rgb*latitude*.055;
                color+=_OwnerColor.rgb*(rim*(.34+_Selected*.75)+_Emission*.08);
                color+=_OwnerColor.rgb*_Selected*(.1+.08*sin(_Time.y*6));
                return half4(color,1);
            }
            ENDHLSL
        }
    }
}
