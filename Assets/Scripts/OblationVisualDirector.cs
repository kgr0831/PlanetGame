using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public sealed class OblationVisualDirector : MonoBehaviour
{
    public Volume volume;
    public Camera sceneCamera;
    public Transform effectsRoot;
    public Material pulseMaterial;
    public Material actionMaterial;
    Bloom bloom;
    ColorAdjustments color;
    ChromaticAberration chromatic;
    bool reduced;
    bool contrast;
    float impulse;
    void Start()
    {
        volume.profile.TryGet(out bloom);
        volume.profile.TryGet(out color);
        volume.profile.TryGet(out chromatic);
    }
    public void Configure(bool reduceEffects,bool highContrast){reduced=reduceEffects;contrast=highContrast;}
    public void Cue(Vector3 position,Color tint,float size,bool major=false)
    {
        OblationEnergyPulse.Spawn(effectsRoot,position,tint,size,pulseMaterial,sceneCamera,reduced||OblationGame.ReducedMotion);
        if(!reduced)impulse=Mathf.Max(impulse,major?.75f:.25f);
    }
    public void Event(Vector3 position,Color tint,OblationEffectKind kind,float size)
    {
        if(actionMaterial!=null)OblationActionFx.Spawn(effectsRoot,position,tint,size,actionMaterial,sceneCamera,kind,reduced||OblationGame.ReducedMotion);
        if(!reduced)impulse=Mathf.Max(impulse,kind==OblationEffectKind.Orbital?.6f:kind==OblationEffectKind.Capture?.4f:.12f);
    }
    void LateUpdate()
    {
        Shader.SetGlobalFloat("_OblationTime",Time.unscaledTime);
        impulse=Mathf.MoveTowards(impulse,0,Time.unscaledDeltaTime*1.7f);
        if(bloom!=null)bloom.intensity.Override(reduced?.2f:1.05f+impulse*.32f);
        if(color!=null)color.postExposure.Override(.22f+(reduced?0:impulse*.1f));
        if(chromatic!=null)chromatic.intensity.Override(reduced||contrast||OblationGame.ReducedMotion?0:.015f+impulse*.045f);
    }
}
