#ifndef OBLATION_PLANET_SURFACE_INCLUDED
#define OBLATION_PLANET_SURFACE_INCLUDED

float PlanetHash(float3 p)
{
    p=frac(p*.1031);p+=dot(p,p.yzx+33.33);
    return frac((p.x+p.y)*p.z);
}
float PlanetNoise(float3 p)
{
    float3 c=floor(p),f=frac(p);
    f=f*f*f*(f*(f*6-15)+10);
    float a=lerp(PlanetHash(c),PlanetHash(c+float3(1,0,0)),f.x);
    float b=lerp(PlanetHash(c+float3(0,1,0)),PlanetHash(c+float3(1,1,0)),f.x);
    float d=lerp(PlanetHash(c+float3(0,0,1)),PlanetHash(c+float3(1,0,1)),f.x);
    float e=lerp(PlanetHash(c+float3(0,1,1)),PlanetHash(c+1),f.x);
    return lerp(lerp(a,b,f.y),lerp(d,e,f.y),f.z);
}
// Fade subpixel octaves instead of letting detail shimmer when viewed from the galaxy map.
float PlanetFbm(float3 p,float footprint,int octaves)
{
    float result=0,weight=.5,total=0;
    [unroll] for(int k=0;k<7;k++)
    {
        if(k>=octaves)break;
        float resolved=1-smoothstep(.3,.85,footprint);
        result+=lerp(.5,PlanetNoise(p),resolved)*weight;total+=weight;
        p=p*2.03+float3(17.13,9.21,31.7);footprint*=2.03;weight*=.49;
    }
    return result/max(total,.001);
}
float3 PlanetSeed(float seed){return float3(seed*.731+7.1,seed*.397+13.2,seed*.163+3.8);}
float3 PlanetCloudPoint(float3 p,float clock)
{
    float s=sin(clock),c=cos(clock);
    return float3(c*p.x-s*p.z,p.y,s*p.x+c*p.z);
}
float PlanetClouds(float3 p,float seed,float footprint,float clock,float coverage)
{
    p=PlanetCloudPoint(p,clock);
    float3 offset=PlanetSeed(seed+48.3);
    float weather=PlanetFbm(p*3.8+offset,footprint*3.8,4);
    float warp=PlanetFbm(p*7.5-offset,footprint*7.5,3)-.5;
    float wisps=PlanetFbm(p*28+offset+warp*3,footprint*28,6);
    float density=weather*.7+wisps*.3;
    return smoothstep(coverage,coverage+.115,density)*lerp(.55,1, wisps);
}
#endif
