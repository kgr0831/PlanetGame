#ifndef OBLATION_PLANET_STYLES_INCLUDED
#define OBLATION_PLANET_STYLES_INCLUDED

struct PlanetSurface
{
    float3 albedo;
    float3 emission;
    float height;
    float specular;
    float gloss;
};

// Irregular, overlapping impact basins with varying radii; no cellular tiling.
float2 NaturalCraters(float3 p,float seed,float footprint)
{
    float relief=0,dust=0;
    [unroll]for(int i=0;i<14;i++)
    {
        float3 key=float3(i*7.13,seed,i*.371);
        float3 center=normalize(float3(PlanetHash(key),PlanetHash(key+19.1),PlanetHash(key+41.7))*2-1);
        float radius=lerp(.045,.22,pow(PlanetHash(key+9.7),2));
        float d=length(p-center)/radius;
        float aa=max(.055,footprint/radius);
        float bowl=1-smoothstep(.3,.92+aa,d);
        float rim=exp(-pow((d-1)/(.13+aa),2));
        float influence=1-smoothstep(1.25,1.6,d);
        relief+=(rim*.28-bowl*.42)*radius*.045;
        dust+=(rim*.1-bowl*.12)*influence;
    }
    return float2(relief,dust);
}

PlanetSurface AlienSurface(float3 p,float footprint,float seedValue,float clock,int type,float variant,
    float3 dark,float3 light,float3 feature)
{
    PlanetSurface s=(PlanetSurface)0;s.gloss=38;s.specular=.018;
    float3 seed=PlanetSeed(seedValue);
    float warpA=PlanetFbm(p*2.4+seed,footprint*2.4,4)-.5;
    float warpB=PlanetFbm(p*2.4-seed,footprint*2.4,4)-.5;
    float3 q=p*lerp(2.4,3.8,variant*.5)+float3(warpA,warpB,warpA-warpB)*1.2;
    float geology=PlanetFbm(q+seed,footprint*4,7);
    float erosion=PlanetFbm(p*18+seed+warpA,footprint*18,6);
    float fine=PlanetFbm(p*105-seed,footprint*105,5);
    float weathering=lerp(.86,1.12,fine);
    float highland=smoothstep(.35,.67,geology);
    float2 craters=float2(0,0);
    s.albedo=lerp(dark,light,highland)*weathering;
    s.height=(geology-.5)*.018+(erosion-.5)*.004;

    [branch]if(type==0) // Exotic terrestrial rock: broad basins, dust and subtle mineral pigments.
    {
        float lowland=1-smoothstep(.32,.49,geology);
        float pigment=smoothstep(.43,.64,PlanetFbm(p*7-seed,footprint*7,5));
        s.albedo=lerp(s.albedo,feature*lerp(.42,.65,fine),pigment*lowland*.42);
        s.height+=(fine-.5)*.0008;
    }
    else if(type==1) // Volcanic terrain: dark lava plains, sparse warm vents and cooled flows.
    {
        float flows=PlanetFbm(q*1.7-seed+erosion*.35,footprint*7,6);
        float cooled=smoothstep(.36,.62,flows);
        float molten=smoothstep(.66,.73,flows)*(1-smoothstep(.47,.61,geology));
        s.albedo=lerp(dark*.55,light,cooled*.7)*weathering;
        s.albedo=lerp(s.albedo,feature*.22,molten*.7);
        s.emission=feature*molten*.8;
        craters=NaturalCraters(p,seedValue,footprint);
        s.height+=craters.x*.45;
        s.albedo*=1+craters.y*.5;
    }
    else if(type==2) // Desert: wind-eroded deposits and dusty valleys, without etched stripes.
    {
        float sediment=PlanetFbm(p*9+seed+warpB*1.8,footprint*9,6);
        float basin=1-smoothstep(.36,.53,sediment);
        s.albedo=lerp(dark,light,.2+highland*.62)*lerp(.86,1.1,erosion);
        s.albedo=lerp(s.albedo,feature*.65,basin*.26);
        s.height=(sediment-.5)*.027+(erosion-.5)*.003;
    }
    else if(type==3) // Frozen rock: broad frost fields and dusty exposed terrain.
    {
        float frost=smoothstep(.37,.6,geology+abs(p.y)*.07);
        s.albedo=lerp(dark,light,frost)*lerp(.9,1.06,erosion);
        s.albedo=lerp(s.albedo,feature*.65,(1-frost)*.16);
        s.height=(geology-.5)*.01+(fine-.5)*.0006;
        s.specular=.055;s.gloss=85;
    }
    else if(type==4) // Gas: uneven cloud belts and a broad, softly rotating storm.
    {
        float latitude=p.y*(6+variant*2)+warpA*.85;
        float belts=PlanetFbm(float3(latitude,seed.x,seed.z),footprint*7,5);
        float flow=PlanetFbm(p*11+seed+warpA*.5,footprint*11,5);
        float3 center=normalize(float3(.4,.16,-1));
        float3 delta=p-center;float radius=length(delta);
        float vortex=exp(-radius*radius*15);
        float curl=sin(atan2(delta.y,delta.x)+radius*16-clock*.025)*vortex*.07;
        float density=saturate((belts-.28)*1.8+(flow-.5)*.23+curl);
        s.albedo=lerp(dark,light,density);
        s.albedo=lerp(s.albedo,feature*.7,vortex*.16);
        s.height=0;s.specular=0;
    }
    else if(type==5) // Airless rocky moon with irregular overlapping impacts.
    {
        craters=NaturalCraters(p,seedValue,footprint);
        s.albedo=lerp(dark,light,.22+highland*.5)*weathering*(1+craters.y);
        s.height=(geology-.5)*.008+(erosion-.5)*.0015+craters.x;
        s.specular=0;
    }
    else if(type==6) // Iron-rich rock: oxidized highlands and dark basalt basins.
    {
        float oxidation=smoothstep(.38,.62,PlanetFbm(p*6-seed,footprint*6,6));
        float3 oxide=lerp(light,float3(.32,.15,.095),.38);
        s.albedo=lerp(dark,oxide,highland)*weathering;
        s.albedo=lerp(s.albedo,light*.65,oxidation*.22);
        craters=NaturalCraters(p,seedValue+8,footprint);
        s.height+=craters.x*.7;s.albedo*=1+craters.y*.5;
    }
    else // Mineral-rich rock: smooth geological variation rather than faceted gemstones.
    {
        float deposits=PlanetFbm(p*6+seed+warpA*2,footprint*6,6);
        float pale=smoothstep(.47,.68,deposits);
        s.albedo=lerp(dark,light,highland)*weathering;
        s.albedo=lerp(s.albedo,feature*.6,pale*.32);
        s.height+=(deposits-.5)*.007;s.specular=.028;
    }
    float resolved=1-smoothstep(.018,.075,footprint);
    s.albedo=lerp(lerp(dark,light,.45),s.albedo,resolved);
    s.emission*=resolved;
    return s;
}
#endif
