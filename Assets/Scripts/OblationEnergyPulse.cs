using UnityEngine;
using UnityEngine.Rendering;

public sealed class OblationEnergyPulse : MonoBehaviour
{
    Material material;
    float age;
    float lifetime;
    public static void Spawn(Transform parent, Vector3 position, Color color, float size, Material template, Camera camera, bool reduced)
    {
        var root=GameObject.CreatePrimitive(PrimitiveType.Quad);
        root.name="에너지 충격 링";
        root.transform.SetParent(parent,false);
        // Keep the flash on the visible surface instead of hiding it inside the planet.
        Vector3 facing=(camera.transform.position-position).normalized;
        root.transform.position=position+facing*.9f;
        root.transform.rotation=Quaternion.LookRotation(-facing);
        root.transform.localScale=Vector3.one*size;
        var collider=root.GetComponent<Collider>();collider.enabled=false;Destroy(collider);
        var renderer=root.GetComponent<Renderer>();
        renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
        var pulse=root.AddComponent<OblationEnergyPulse>();
        pulse.material=new Material(template);
        pulse.material.SetColor("_Tint",new Color(color.r*3.5f,color.g*3.5f,color.b*3.5f,1));
        pulse.material.SetFloat("_Strength",reduced?.12f:.8f);
        pulse.lifetime=reduced?.7f:1.7f;
        renderer.sharedMaterial=pulse.material;
    }
    void Update()
    {
        age+=Time.unscaledDeltaTime;
        material.SetFloat("_Age",Mathf.Clamp01(age/lifetime));
        if(age>=lifetime)Destroy(gameObject);
    }
    void OnDestroy(){if(material!=null)Destroy(material);}
}
