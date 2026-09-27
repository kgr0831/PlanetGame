Shader "Oblation/Procedural Planet"
{
    Properties
    {
        _BaseColor("Surface Shadows", Color) = (0.04,0.12,0.25,1)
        _AccentColor("Surface Highlights", Color) = (0.35,0.72,0.8,1)
        [Enum(OblationPlanetSurfaceType)] _SurfaceType("Surface Type", Float) = 0
        [HDR] _FeatureColor("Luminous Features", Color) = (0.3,1,0.65,1)
        _StyleVariant("Style Variant", Range(0,2)) = 0
        _CloudOpacity("Cloud Opacity", Range(0,1)) = 0.3
        _OwnerColor("Owner", Color) = (0.5,0.5,0.5,1)
        _Seed("Seed", Float) = 1
        _Selected("Selected", Range(0,1)) = 0
        _Emission("Emission", Range(0,5)) = 1.2
        _HitFlash("Impact", Range(0,1)) = 0
        _SceneFocus("Scene Focus", Range(0,1)) = 1
        _Relief("Terrain Relief", Range(0,1)) = .38
        _CloudCoverage("Cloud Threshold", Range(.35,.7)) = .53
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "OblationPlanetSurface.hlsl"
            #include "OblationPlanetStyles.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionHCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 positionWS : TEXCOORD1; float3 objectPos : TEXCOORD2; };

            CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor, _AccentColor, _OwnerColor, _FeatureColor;
            float _Seed, _Selected, _Emission, _HitFlash, _SceneFocus, _Relief, _CloudCoverage;
            float _SurfaceType, _StyleVariant, _CloudOpacity;
            CBUFFER_END
            float _OblationMotion, _OblationEffects;

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
                float3 geometricNormal=normalize(i.normalWS),v=normalize(_WorldSpaceCameraPos-i.positionWS);
                float3 p=normalize(i.objectPos),seed=PlanetSeed(_Seed);
                float footprint=max(length(ddx(p)),length(ddy(p)));
                PlanetSurface surface=AlienSurface(p,footprint,_Seed,_Time.y*_OblationMotion,(int)_SurfaceType,_StyleVariant,
                    _BaseColor.rgb,_AccentColor.rgb,_FeatureColor.rgb);
                float3 albedo=surface.albedo;
                float height=surface.height;
                float3 dx=ddx(i.positionWS),dy=ddy(i.positionWS);
                float3 r1=cross(dy,geometricNormal),r2=cross(geometricNormal,dx);
                float determinant=dot(dx,r1);
                float3 gradient=sign(determinant)*(ddx(height)*r1+ddy(height)*r2)/max(abs(determinant),1e-8);
                gradient*=min(1,.75/max(length(gradient),.001));
                float3 n=normalize(geometricNormal-gradient*_Relief);
                Light key=GetMainLight();
                float ndl=saturate(dot(n,key.direction));
                float cloudShadow=PlanetClouds(p+TransformWorldToObjectDir(key.direction)*.022,_Seed,footprint,_Time.y*.008*_OblationMotion,_CloudCoverage)*_CloudOpacity;
                float3 color=albedo*(SampleSH(n)*.65+float3(.013,.019,.03)+key.color*ndl*(1-cloudShadow*.42));
                color+=albedo*.025;
                InputData inputData=(InputData)0;
                inputData.positionWS=i.positionWS;
                inputData.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.positionHCS);
                uint lightCount=GetAdditionalLightsCount();
                #if USE_CLUSTER_LIGHT_LOOP
                UNITY_LOOP for(uint lightIndex=0;lightIndex<min(URP_FP_DIRECTIONAL_LIGHTS_COUNT,MAX_VISIBLE_LIGHTS);lightIndex++)
                {
                    Light fill=GetAdditionalLight(lightIndex,i.positionWS);
                    color+=albedo*fill.color*saturate(dot(n,fill.direction))*.3;
                }
                #endif
                LIGHT_LOOP_BEGIN(lightCount)
                    Light glow=GetAdditionalLight(lightIndex,i.positionWS);
                    color+=albedo*glow.color*saturate(dot(n,glow.direction))*glow.distanceAttenuation*.38;
                LIGHT_LOOP_END
                float spec=pow(saturate(dot(n,normalize(key.direction+v))),surface.gloss);
                color+=lerp(key.color,float3(1,1,1),.45)*spec*surface.specular*(1-cloudShadow);
                color+=surface.emission*_Emission*_OblationEffects;
                float rim=pow(1-saturate(dot(geometricNormal,v)),5);
                color+=_AccentColor.rgb*rim*(.05+_Selected*.08)*_OblationEffects;
                color+=float3(1,.58,.22)*_HitFlash*3;
                return half4(color*_SceneFocus,1);
            }
            ENDHLSL
        }
    }
}
