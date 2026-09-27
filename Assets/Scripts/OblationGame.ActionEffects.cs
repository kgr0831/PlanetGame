using System.Collections.Generic;
using UnityEngine;

public sealed partial class OblationGame
{
    readonly Dictionary<int,OblationSignalVisual> antennaEffects = new Dictionary<int,OblationSignalVisual>();
    static OblationEffectKind AttackEffect(AttackMode mode)=>mode==AttackMode.Surprise?OblationEffectKind.Plague:mode==AttackMode.Orbital?OblationEffectKind.Orbital:OblationEffectKind.Assault;
    void ResetActionEffects()
    {
        foreach(var effect in antennaEffects.Values)if(effect!=null)Destroy(effect.gameObject);
        antennaEffects.Clear();
    }
    void UpdateActionEffects()
    {
        if(state!=ScreenState.Playing)return;
        for(int i=0;i<planets.Count;i++)
        {
            Planet p=planets[i];antennaEffects.TryGetValue(i,out var effect);
            if(p.antennaRemaining<=0||p.destroyed)
            {if(effect!=null)Destroy(effect.gameObject);antennaEffects.Remove(i);continue;}
            if(effect==null)
            {
                int source=p.owner==Allegiance.Enemy?1:0;
                foreach(int neighbor in p.links)if(planets[neighbor].antennaActive&&planets[neighbor].owner==p.owner){source=neighbor;break;}
                var go=new GameObject("안테나 구축 · "+p.name);go.transform.SetParent(effectsRoot,false);go.AddComponent<LineRenderer>();
                effect=go.AddComponent<OblationSignalVisual>();effect.Initialize(planets[source].position,p.position,OwnerColor(p.owner),lineMaterial,reducedEffects);
                antennaEffects[i]=effect;
            }
            effect.SetProgress(1-p.antennaRemaining/catalog.antenna.installationSeconds);
        }
    }
}
