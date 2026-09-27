Shader "Oblation/Action FX"
{
    Properties { [HDR] _Tint("Emission",Color)=(.2,2,4,1) _Age("Age",Range(0,1))=0 _Kind("Action",Float)=0 _Strength("Strength",Float)=1 }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+21" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha One ZWrite Off Cull Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A { float4 positionOS:POSITION;float2 uv:TEXCOORD0; };
            struct V { float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0; };
            CBUFFER_START(UnityPerMaterial)
            float4 _Tint;float _Age,_Kind,_Strength;
            CBUFFER_END
            V vert(A i){V o;o.positionCS=TransformObjectToHClip(i.positionOS.xyz);o.uv=i.uv;return o;}
            float ring(float r,float radius,float width){return exp(-abs(r-radius)*width);}
            half4 frag(V i):SV_Target
            {
                float2 p=i.uv*2-1;float r=length(p),a=atan2(p.y,p.x),t=_Age;
                float fade=sin(saturate(t)*3.14159),v=0;
                if(_Kind<.5) // kinetic impact: sharp directional debris
                    v=ring(r,.1+t*.8,80)*(1-t)+pow(saturate(sin(a*13+r*21)),28)*exp(-abs(r-t*.7)*10)+exp(-r*22)*(1-t);
                else if(_Kind<1.5) // plague: soft asymmetric curling cloud
                {
                    float lobes=.09*sin(a*5-t*9)+.045*sin(a*9+t*7);
                    v=exp(-pow((r-lobes-.25-t*.27)*5,2))*(.45+.35*sin(a*4+r*19-t*8));
                    v+=ring(r+lobes,.2+t*.45,25)*.4;
                }
                else if(_Kind<2.5) // orbital strike: vertical lance and lens cross
                    v=exp(-abs(p.x)*110)*exp(-abs(p.y)*1.7)+ring(r,.52-.35*t,90)*.7+exp(-abs(p.y)*90)*exp(-abs(p.x)*4)*.7;
                else if(_Kind<3.5) // antenna: three discrete propagating waves
                {
                    for(int j=0;j<3;j++){float phase=frac(t*1.7-j*.25);v+=ring(r,.08+phase*.84,100)*(1-phase);}
                    v*=.4+.6*step(-.35,sin(a*3));v+=exp(-r*35)*.5;
                }
                else if(_Kind<4.5) // production: rising chevrons
                {
                    for(int j=0;j<3;j++){float y=frac(t+j*.27)*1.6-.8;v+=exp(-abs(p.y-y+abs(p.x)*.55)*65)*saturate(1-abs(p.x)*2);}
                }
                else if(_Kind<5.5) // research: crossed orbits and bright nodes
                {
                    float c=cos(t*2),s=sin(t*2);float2 q=float2(c*p.x-s*p.y,s*p.x+c*p.y);
                    v=ring(length(q*float2(1,2.7)),.65,70)+ring(length(q*float2(2.7,1)),.65,70);
                    v+=exp(-length(p-float2(c,s)*.65)*45)*2;
                }
                else // conquest: expanding aureole and rising crown
                {
                    v=ring(r,.3+.5*t,75)+ring(r,.25+.35*t,65)*.4;
                    v+=pow(saturate(cos(a*8)),20)*ring(r,.45+.35*t,18)*.7;
                }
                return half4(_Tint.rgb,saturate(v*fade*_Strength)*saturate((1-r)*10));
            }
            ENDHLSL
        }
    }
}
