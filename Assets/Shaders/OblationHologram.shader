Shader "Oblation/Holographic UI"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite",2D)="white"{}
        _Accent("Accent",Color)=(.16,.78,1,1)
        _Size("Size",Vector)=(200,60,0,0)
        _Hover("Hover",Float)=0
        _Press("Press",Float)=0
        _Reveal("Reveal",Float)=1
        _PanelMode("Panel Frame",Float)=0
        _Click("Click",Vector)=(.5,.5,0,0)
        _StencilComp("Stencil Comparison",Float)=8
        _Stencil("Stencil ID",Float)=0
        _StencilOp("Stencil Operation",Float)=0
        _StencilWriteMask("Stencil Write Mask",Float)=255
        _StencilReadMask("Stencil Read Mask",Float)=255
        _ColorMask("Color Mask",Float)=15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip("Use Alpha Clip",Float)=0
    }
    SubShader
    {
        Tags{"Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "CanUseSpriteAtlas"="True"}
        Stencil{Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask]}
        Cull Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A{float4 positionOS:POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;};
            struct V{float4 positionCS:SV_POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;float2 local:TEXCOORD1;};
            TEXTURE2D(_MainTex);SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
            float4 _Accent,_Size,_Click,_ClipRect;
            float _Hover,_Press,_Reveal,_PanelMode;
            CBUFFER_END
            float _OblationMotion,_OblationEffects,_OblationTime;
            V vert(A i){V o;o.positionCS=TransformObjectToHClip(i.positionOS.xyz);o.local=i.positionOS.xy;o.uv=i.uv;o.color=i.color;return o;}
            half4 frag(V i):SV_Target
            {
                float2 size=max(_Size.xy,1),p=i.uv*size;
                float cut=min(9,min(size.x,size.y)*.12);
                float corner=min(p.x+p.y,(size.x-p.x)+(size.y-p.y))-cut;
                float edge=min(min(p.x,size.x-p.x),min(p.y,size.y-p.y));
                edge=min(edge,corner*.707);
                float aa=max(fwidth(edge),.6);
                float mask=smoothstep(-aa,aa,edge);
                float border=1-smoothstep(1,1+aa,edge);
                float inner=exp(-max(edge,0)*.15);
                float sweep=pow(saturate(1-abs(i.uv.x-frac(_OblationTime*.22))*.9),8)*_Hover*_OblationMotion;
                float scan=.003*sin(p.y*.65-_OblationTime*_OblationMotion*1.5);
                float ring=exp(-pow((length((i.uv-_Click.xy)*size)-_Press*max(size.x,size.y))* .12,2))*step(.001,_Press)*(1-_Press);
                float3 base=i.color.rgb*lerp(.68,1.28,i.uv.y)+_Accent.rgb*(.018+scan)*_OblationEffects;
                float3 color=base+_Accent.rgb*(border*(.3+_Hover*.5)+inner*(.045+_Hover*.1)+sweep*.16+ring*.55)*_OblationEffects;
                float2 fromEdge=min(p,size-p);
                float corners=(1-step(36,fromEdge.x))*(1-smoothstep(2,3,fromEdge.y))+(1-step(22,fromEdge.y))*(1-smoothstep(2,3,fromEdge.x));
                float grid=(1-smoothstep(.35,1,abs(frac(p.x/36)-.5)*36))+(1-smoothstep(.35,1,abs(frac(p.y/36)-.5)*36));
                float ticks=step(.75,frac(p.x/12))*(1-smoothstep(6,8,fromEdge.y));
                float tracer=pow(saturate(.5+.5*sin((i.uv.x+i.uv.y)*5-_OblationTime*.65*_OblationMotion)),18);
                float projection=exp(-pow((i.uv.x-saturate(_Reveal))*24,2))*(1-saturate(_Reveal));
                color+=_Accent.rgb*(corners*.42+_PanelMode*(grid*.009+ticks*.06)+border*tracer*.3+projection*.32)*_OblationEffects;
                float alpha=i.color.a*mask*SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv).a;
                #ifdef UNITY_UI_CLIP_RECT
                float2 inside=step(_ClipRect.xy,i.local)*step(i.local,_ClipRect.zw);alpha*=inside.x*inside.y;
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(alpha-.001);
                #endif
                return half4(color,alpha);
            }
            ENDHLSL
        }
    }
}
