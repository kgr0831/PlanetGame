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
            float _OblationMotion;
            float hash(float3 p){return frac(sin(dot(p,float3(17.17,71.53,43.21)))*43758.5453);}
            float noise(float3 p)
            {
                float3 a=floor(p),f=frac(p);f=f*f*(3-2*f);
                return lerp(lerp(lerp(hash(a),hash(a+float3(1,0,0)),f.x),lerp(hash(a+float3(0,1,0)),hash(a+float3(1,1,0)),f.x),f.y),lerp(lerp(hash(a+float3(0,0,1)),hash(a+float3(1,0,1)),f.x),lerp(hash(a+float3(0,1,1)),hash(a+1),f.x),f.y),f.z);
            }
            float fog(float3 p){float r=0,a=.55;[unroll] for(int k=0;k<4;k++){r+=noise(p)*a;p=p*2.02+8.7;a*=.5;}return r;}
            float stars(float3 d,float scale,float seed)
            {
                float3 p=d*scale,cell=floor(p),f=frac(p)-.5;
                float h=hash(cell+seed);
                float radius=lerp(.06,.16,h);
                float dotStar=exp(-dot(f,f)/(radius*radius));
                return dotStar*step(.982,h)*lerp(.55,4,frac(h*72))*(.92+.08*sin(_Time.y*.5*_OblationMotion+h*77));
            }
            Varyings vert(Attributes i){Varyings o;o.positionHCS=TransformObjectToHClip(i.positionOS.xyz);o.dir=normalize(i.positionOS.xyz);return o;}
            half4 frag(Varyings i):SV_Target
            {
                float3 d=normalize(i.dir);
                float density=fog(d*4.5+float3(3,7,1));
                float dust=fog(d*14+density*3);
                float band=pow(saturate(1-abs(d.y+d.x*.38+.18)*1.9),3);
                float nebula=pow(saturate(density*1.65-.3),2)*band;
                float3 color=float3(.003,.006,.019);
                color+=lerp(float3(.16,.025,.34),float3(.015,.25,.32),dust)*nebula*2.4;
                color*=1-smoothstep(.52,.74,dust)*band*.65;
                color+=stars(d,155,2)*float3(.54,.77,1)+stars(d,290,19)*float3(1,.76,.55);
                return half4(color,1);
            }
            ENDHLSL
        }
    }
}
