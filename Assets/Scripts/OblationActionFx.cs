using UnityEngine;
using UnityEngine.Rendering;

public enum OblationEffectKind { Assault, Plague, Orbital, Antenna, Production, Research, Capture }

public sealed class OblationActionFx : MonoBehaviour
{
    Material owned;
    Camera view;
    float age;
    float duration;
    public OblationEffectKind Kind { get; private set; }
    public static void Spawn(Transform parent,Vector3 position,Color color,float size,Material template,Camera camera,OblationEffectKind kind,bool reduced)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Quad);go.name="사건 효과 · "+kind;go.transform.SetParent(parent,false);
        Vector3 facing=(camera.transform.position-position).normalized;
        go.transform.position=position+facing*1.2f;go.transform.localScale=Vector3.one*size;
        var collider=go.GetComponent<Collider>();collider.enabled=false;Destroy(collider);
        var renderer=go.GetComponent<Renderer>();renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
        var fx=go.AddComponent<OblationActionFx>();fx.view=camera;fx.Kind=kind;fx.duration=reduced?1.2f:kind==OblationEffectKind.Antenna?3:2.2f;
        fx.owned=new Material(template);fx.owned.SetFloat("_Kind",(int)kind);fx.owned.SetFloat("_Strength",reduced?.22f:.9f);
        fx.owned.SetColor("_Tint",new Color(color.r*3,color.g*3,color.b*3,1));renderer.sharedMaterial=fx.owned;
        go.transform.rotation=Quaternion.LookRotation(-facing);
    }
    void Update()
    {
        age+=Time.unscaledDeltaTime;owned.SetFloat("_Age",Mathf.Clamp01(age/duration));
        if(view!=null)transform.rotation=Quaternion.LookRotation(transform.position-view.transform.position);
        if(age>=duration)Destroy(gameObject);
    }
    void OnDestroy(){if(owned!=null)Destroy(owned);}
}
