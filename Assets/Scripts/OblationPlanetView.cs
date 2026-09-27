using UnityEngine;

public enum OblationPlanetSurfaceType { ExoticRock, Volcanic, Desert, Frozen, Gas, Barren, IronRich, MineralRich }

public sealed class OblationPlanetView : MonoBehaviour
{
    public int index;
    public Renderer bodyRenderer;
    public Renderer haloRenderer;
    public Renderer atmosphereRenderer;
    public Renderer cloudRenderer;
    public Renderer ringRenderer;
    public Color atmosphereTint=new Color(.15f,.6f,1);
    public Color cloudTint=new Color(.66f,.78f,.9f);
    [Range(0,1)] public float atmosphereStrength=1;
    MaterialPropertyBlock atmosphereProperties;
    MaterialPropertyBlock cloudProperties;
    MaterialPropertyBlock ringProperties;
    Material originalSurface;
    Material ownedSurface;
    Quaternion originalRingRotation;
    Color variedAtmosphere;
    Color variedClouds;
    Color variedRing;
    float atmosphereVariation=1;
    bool appearanceInitialized;
    public int VisualSeed { get; private set; }

    public void ApplyVisualSeed(int seed)
    {
        if(!appearanceInitialized)
        {
            originalSurface=bodyRenderer.sharedMaterial;
            ownedSurface=bodyRenderer.material;
            if(ringRenderer!=null)originalRingRotation=ringRenderer.transform.localRotation;
            appearanceInitialized=true;
        }
        VisualSeed=seed;var random=new System.Random(seed);
        float hue=Range(random,-.02f,.02f),saturation=Range(random,.88f,1.12f),brightness=Range(random,.9f,1.1f);
        ownedSurface.SetColor("_BaseColor",Vary(originalSurface.GetColor("_BaseColor"),hue,saturation,brightness));
        ownedSurface.SetColor("_AccentColor",Vary(originalSurface.GetColor("_AccentColor"),hue,saturation,brightness));
        ownedSurface.SetColor("_FeatureColor",Vary(originalSurface.GetColor("_FeatureColor"),hue,saturation,brightness));
        ownedSurface.SetFloat("_Seed",Range(random,1,240));
        ownedSurface.SetFloat("_StyleVariant",Range(random,0,2));
        ownedSurface.SetFloat("_CloudCoverage",Mathf.Clamp(originalSurface.GetFloat("_CloudCoverage")+Range(random,-.025f,.025f),.45f,.65f));
        ownedSurface.SetFloat("_CloudOpacity",Mathf.Clamp01(originalSurface.GetFloat("_CloudOpacity")*Range(random,.75f,1.25f)));
        bodyRenderer.transform.localRotation=Quaternion.Euler(Range(random,-12,12),Range(random,0,360),Range(random,-8,8));
        variedAtmosphere=Vary(atmosphereTint,hue,saturation,1);
        variedClouds=Vary(cloudTint,hue,saturation,1);
        atmosphereVariation=Range(random,.85f,1.15f);
        if(ringRenderer!=null)
        {
            ringRenderer.transform.localRotation=originalRingRotation*Quaternion.Euler(Range(random,-6,6),0,Range(random,-6,6));
            variedRing=Vary(ringRenderer.sharedMaterial.GetColor("_Color"),hue,saturation,brightness);
        }
    }
    static float Range(System.Random random,float min,float max)=>Mathf.Lerp(min,max,(float)random.NextDouble());
    static Color Vary(Color color,float hue,float saturation,float brightness)
    {
        Color.RGBToHSV(color,out float h,out float s,out float v);
        Color result=Color.HSVToRGB(Mathf.Repeat(h+hue,1),Mathf.Clamp01(s*saturation),Mathf.Clamp01(v*brightness));
        result.a=color.a;return result;
    }
    public void SetAtmosphere(Color tint,float strength,float sceneFocus=1)
    {
        if(cloudRenderer!=null)
        {
            if(cloudProperties==null)cloudProperties=new MaterialPropertyBlock();
            cloudProperties.SetFloat("_Seed",bodyRenderer.sharedMaterial.GetFloat("_Seed"));
            cloudProperties.SetFloat("_CloudCoverage",bodyRenderer.sharedMaterial.GetFloat("_CloudCoverage"));
            cloudProperties.SetFloat("_SceneFocus",sceneFocus);
            float opacity=bodyRenderer.sharedMaterial.GetFloat("_CloudOpacity");
            cloudRenderer.enabled=opacity>.001f;
            cloudProperties.SetFloat("_CloudOpacity",opacity);
            cloudProperties.SetColor("_CloudTint",appearanceInitialized?variedClouds:cloudTint);
            cloudRenderer.SetPropertyBlock(cloudProperties);
        }
        if(ringRenderer!=null)
        {
            if(ringProperties==null)ringProperties=new MaterialPropertyBlock();
            ringProperties.SetFloat("_SceneFocus",sceneFocus);
            if(appearanceInitialized){ringProperties.SetColor("_Color",variedRing);ringProperties.SetFloat("_Seed",VisualSeed%1000);}
            ringRenderer.SetPropertyBlock(ringProperties);
        }
        if(atmosphereRenderer==null)return;
        if(atmosphereProperties==null)atmosphereProperties=new MaterialPropertyBlock();
        atmosphereProperties.SetColor("_AtmosphereTint",Color.Lerp(appearanceInitialized?variedAtmosphere:atmosphereTint,tint,.06f));
        atmosphereProperties.SetFloat("_Intensity",1.8f*strength*atmosphereStrength*atmosphereVariation);
        atmosphereProperties.SetFloat("_Opacity",.3f);
        atmosphereRenderer.SetPropertyBlock(atmosphereProperties);
    }
    void OnDestroy(){if(ownedSurface!=null)Destroy(ownedSurface);}
}
