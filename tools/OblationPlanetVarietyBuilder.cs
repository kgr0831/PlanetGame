using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class OblationPlanetVarietyBuilder
{
    static readonly int[] Types={0,6,1,2,3,4,0,5,4,7,6,4};
    static readonly string[] Dark={"39313E","322925","282324","614135","4C6170","464155","303832","302C2C","244453","4F3D4C","373432","625043"};
    static readonly string[] Light={"887E91","988172","6B5043","BD9871","C8D8D9","A297AC","758578","8D8176","8DAEAC","A98B96","9C907E","CDB692"};
    static readonly string[] Glow={"647487","B59474","EA5B26","D8BB96","769CAF","DDD0BD","A4B296","A09285","C4D4C8","DAC4B6","B9A993","EEE1C7"};
    static readonly float[] Clouds={.24f,.06f,.09f,.3f,.16f,0,.28f,0,0,.12f,.04f,0};
    static readonly float[] Atmospheres={.32f,.12f,.16f,.22f,.22f,.38f,.38f,0,.42f,.2f,.08f,.3f};
    static Color ColorOf(string hex){ColorUtility.TryParseHtmlString("#"+hex,out Color value);return value;}
    public static string Build()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play mode first.");
        var scene=EditorSceneManager.GetActiveScene();if(scene.path!="Assets/Scenes/SampleScene.unity")throw new Exception("Open SampleScene.");
        var root=GameObject.Find("Root");Mesh ringMesh=RingMesh();
        foreach(var view in root.GetComponentsInChildren<OblationPlanetView>(true))
        {
            int i=view.index%12;var material=view.bodyRenderer.sharedMaterial;
            material.SetFloat("_SurfaceType",Types[i]);material.SetFloat("_StyleVariant",i==6?1:i==8?1.5f:i==11?.5f:0);
            material.SetColor("_BaseColor",ColorOf(Dark[i]));material.SetColor("_AccentColor",ColorOf(Light[i]));material.SetColor("_FeatureColor",ColorOf(Glow[i]));
            material.SetFloat("_Emission",Types[i]==1?.5f:0);material.SetFloat("_CloudOpacity",Clouds[i]);
            material.SetFloat("_CloudCoverage",.55f);material.SetFloat("_Relief",Types[i]==4?0:Types[i]==5?.7f:.38f);
            view.atmosphereTint=ColorOf(Glow[i]);view.cloudTint=Color.Lerp(ColorOf(Light[i]),Color.white,.25f);view.atmosphereStrength=Atmospheres[i];
            EditorUtility.SetDirty(material);EditorUtility.SetDirty(view);
            view.cloudRenderer.enabled=Clouds[i]>0;
            view.atmosphereRenderer.transform.localScale=Vector3.one*1.025f;
            bool ringed=i==5||i==11;
            Transform ring=view.bodyRenderer.transform.Find("OrbitalRing");
            if(ringed)
            {
                if(ring==null){ring=new GameObject("OrbitalRing",typeof(MeshFilter),typeof(MeshRenderer)).transform;ring.SetParent(view.bodyRenderer.transform,false);}
                ring.localRotation=Quaternion.Euler(i==5?24:12,0,i==5?-17:28);
                ring.GetComponent<MeshFilter>().sharedMesh=ringMesh;
                string path="Assets/Materials/Oblation/"+view.name+"Ring.mat";
                var ringMaterial=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(ringMaterial==null){ringMaterial=new Material(Shader.Find("Oblation/Planet Rings"));AssetDatabase.CreateAsset(ringMaterial,path);}
                ringMaterial.SetColor("_Color",Color.Lerp(ColorOf(Light[i]),Color.white,.2f));ringMaterial.SetFloat("_Seed",i+1);EditorUtility.SetDirty(ringMaterial);
                var renderer=ring.GetComponent<MeshRenderer>();renderer.sharedMaterial=ringMaterial;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                view.ringRenderer=renderer;
            }
            if(ring!=null)ring.gameObject.SetActive(ringed);
        }
        foreach(var light in root.GetComponentsInChildren<Light>(true))
            if(light.type==LightType.Directional){light.transform.rotation=Quaternion.Euler(24,42,0);light.color=new Color(.95f,.97f,1);EditorUtility.SetDirty(light);}
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        return "Saved natural extraterrestrial geology, restrained atmospheres and side lighting for 12 planets.";
    }
    static Mesh RingMesh()
    {
        const string path="Assets/Materials/Oblation/PlanetRingMesh.asset";
        var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(mesh!=null)return mesh;
        const int segments=192;var vertices=new Vector3[(segments+1)*2];var uv=new Vector2[vertices.Length];var triangles=new int[segments*6];
        for(int i=0;i<=segments;i++)
        {
            float angle=i*Mathf.PI*2/segments;Vector3 axis=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
            vertices[i*2]=axis*.68f;vertices[i*2+1]=axis*1.08f;uv[i*2]=new Vector2(0,i/(float)segments);uv[i*2+1]=new Vector2(1,i/(float)segments);
            if(i==segments)continue;int t=i*6,v=i*2;
            triangles[t]=v;triangles[t+1]=v+2;triangles[t+2]=v+1;triangles[t+3]=v+1;triangles[t+4]=v+2;triangles[t+5]=v+3;
        }
        mesh=new Mesh{name="Oblation Planet Rings"};mesh.vertices=vertices;mesh.uv=uv;mesh.triangles=triangles;mesh.RecalculateNormals();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,path);return mesh;
    }
}
