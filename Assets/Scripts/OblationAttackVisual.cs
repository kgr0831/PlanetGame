using UnityEngine;

public sealed class OblationAttackVisual : MonoBehaviour
{
    LineRenderer path;
    Transform head;
    Light glow;
    Vector3 from,to;
    Color color;
    bool reduced;
    Transform[] escorts;
    public OblationEffectKind Style { get; private set; }
    public void Initialize(Vector3 start,Vector3 target,Color tint,Material material,OblationEffectKind style,bool reduceEffects)
    {
        from=start;to=target;color=tint;Style=style;reduced=reduceEffects||OblationGame.ReducedMotion;
        path=GetComponent<LineRenderer>();path.positionCount=style==OblationEffectKind.Orbital?2:40;
        path.startWidth=style==OblationEffectKind.Plague?.12f:.06f;path.endWidth=.025f;
        path.startColor=new Color(tint.r,tint.g,tint.b,.16f);path.endColor=tint*2;
        head=Wake("Lead",material,style==OblationEffectKind.Plague?.3f:.15f,style==OblationEffectKind.Plague?1.2f:.5f);
        glow=head.gameObject.AddComponent<Light>();glow.type=LightType.Point;glow.color=tint;glow.range=4;glow.shadows=LightShadows.None;
        escorts=new Transform[reduced?0:style==OblationEffectKind.Assault?4:style==OblationEffectKind.Plague?6:2];
        for(int i=0;i<escorts.Length;i++)escorts[i]=Wake("Escort "+i,material,style==OblationEffectKind.Plague?.16f:.065f,.65f);
        if(style==OblationEffectKind.Assault)
        {
            Ship(head,material,.32f);
            foreach(var escort in escorts)Ship(escort,material,.19f);
        }
        SetProgress(0);
    }
    Transform Wake(string name,Material material,float width,float lifetime)
    {
        var go=new GameObject(name);go.transform.SetParent(transform,false);go.transform.position=from;
        var trail=go.AddComponent<TrailRenderer>();trail.sharedMaterial=material;trail.time=lifetime;trail.startWidth=width;trail.endWidth=0;
        trail.startColor=color*2.8f;trail.endColor=new Color(color.r,color.g,color.b,0);trail.minVertexDistance=.06f;trail.numCapVertices=3;
        return go.transform;
    }
    void Ship(Transform parent,Material material,float size)
    {
        var line=parent.gameObject.AddComponent<LineRenderer>();line.sharedMaterial=material;line.useWorldSpace=false;line.positionCount=4;line.loop=true;
        line.startWidth=line.endWidth=.055f;line.startColor=line.endColor=Color.Lerp(color,Color.white,.6f)*2;
        line.SetPositions(new[]{new Vector3(0,0,size*2),new Vector3(-size,0,-size),Vector3.zero,new Vector3(size,0,-size)});
    }
    public void SetProgress(float progress)
    {
        if(Style==OblationEffectKind.Orbital)
        {
            Vector3 origin=to+Vector3.up*9;
            float strike=Mathf.InverseLerp(.58f,.82f,progress);
            path.SetPosition(0,origin);path.SetPosition(1,Vector3.Lerp(origin,to,strike));
            path.startWidth=path.endWidth=progress<.58f?.035f:Mathf.Lerp(.5f,.09f,Mathf.InverseLerp(.82f,1,progress));
            path.startColor=path.endColor=color*(progress<.58f?.5f:4);
            head.position=Vector3.Lerp(origin,to,strike);
            for(int i=0;i<escorts.Length;i++){float a=progress*Mathf.PI*3+i*Mathf.PI;escorts[i].position=origin+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*1.6f;}
        }
        else
        {
            for(int i=0;i<path.positionCount;i++)path.SetPosition(i,Point(progress*i/(path.positionCount-1)));
            head.position=Point(progress);head.rotation=Quaternion.LookRotation(to-from);
            Vector3 side=Vector3.Cross((to-from).normalized,Vector3.up).normalized;
            for(int i=0;i<escorts.Length;i++)
            {
                float t=Mathf.Clamp01(progress-.018f*(i+1));float spread=Mathf.Sin(t*Mathf.PI);
                if(Style==OblationEffectKind.Plague)
                {float a=t*22+i*Mathf.PI/3;escorts[i].position=Point(t)+(side*Mathf.Cos(a)+Vector3.up*Mathf.Sin(a))*.5f*spread;}
                else {escorts[i].position=Point(t)+side*((i%2==0?-1:1)*(.28f+i*.18f)*spread);escorts[i].rotation=head.rotation;}
            }
        }
        glow.intensity=reduced?.2f:Style==OblationEffectKind.Orbital?3:Style==OblationEffectKind.Plague?.6f:1.4f;
    }
    Vector3 Point(float t)
    {
        Vector3 point=Vector3.Lerp(from,to,t)+Vector3.up*(Mathf.Sin(t*Mathf.PI)*(Style==OblationEffectKind.Plague?.6f:1.4f));
        if(Style==OblationEffectKind.Plague&&!reduced)point+=Vector3.Cross((to-from).normalized,Vector3.up)*Mathf.Sin(t*24)*Mathf.Sin(t*Mathf.PI)*.3f;
        return point;
    }
}
