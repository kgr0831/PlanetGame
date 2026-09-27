using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

public static class OblationVfxBuilder
{
    public static string Build()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play mode first.");
        var scene=EditorSceneManager.GetActiveScene();
        if(scene.path!="Assets/Scenes/SampleScene.unity")throw new InvalidOperationException("Open SampleScene first.");
        Transform root=GameObject.Find("Root").transform;
        Transform util=root.Find("Util");
        var game=util.Find("Runtime/GameManager").GetComponent<OblationGame>();
        var serialized=new SerializedObject(game);
        var camera=(Camera)serialized.FindProperty("gameCamera").objectReferenceValue;
        var volume=(Volume)serialized.FindProperty("gameplayVolume").objectReferenceValue;
        Material atmosphere=Material("Atmosphere",AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/OblationAtmosphere.shadergraph"));
        atmosphere.SetColor("_AtmosphereTint",new Color(.12f,.55f,1));atmosphere.SetFloat("_Intensity",1.8f);atmosphere.SetFloat("_RimPower",4.2f);atmosphere.SetFloat("_Opacity",.3f);
        Material hologram=Material("HolographicUI",Shader.Find("Oblation/Holographic UI"));
        Material pulse=Material("EnergyPulse",Shader.Find("Oblation/Energy Pulse"));
        Mesh sphere=Sphere();
        Color[] oceans={new Color(.012f,.045f,.09f),new Color(.09f,.014f,.04f),new Color(.018f,.035f,.095f),new Color(.028f,.012f,.07f)};
        Color[] lands={new Color(.1f,.48f,.42f),new Color(.56f,.1f,.18f),new Color(.27f,.38f,.64f),new Color(.46f,.22f,.59f)};
        foreach(var planet in root.GetComponentsInChildren<OblationPlanetView>(true))
        {
            planet.bodyRenderer.GetComponent<MeshFilter>().sharedMesh=sphere;
            planet.haloRenderer.GetComponent<MeshFilter>().sharedMesh=sphere;
            var bodyMaterial=planet.bodyRenderer.sharedMaterial;
            bodyMaterial.SetColor("_BaseColor",oceans[planet.index%4]);bodyMaterial.SetColor("_AccentColor",lands[planet.index%4]);
            bodyMaterial.SetFloat("_Emission",1.25f);EditorUtility.SetDirty(bodyMaterial);
            Transform shell=planet.bodyRenderer.transform.Find("Atmosphere");
            if(shell==null)
            {
                var go=new GameObject("Atmosphere",typeof(MeshFilter),typeof(MeshRenderer));
                shell=go.transform;shell.SetParent(planet.bodyRenderer.transform,false);
            }
            shell.localScale=Vector3.one*1.055f;
            shell.GetComponent<MeshFilter>().sharedMesh=sphere;
            var renderer=shell.GetComponent<MeshRenderer>();renderer.sharedMaterial=atmosphere;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            planet.atmosphereRenderer=renderer;EditorUtility.SetDirty(planet);
        }
        int surfaces=0;
        foreach(var button in root.GetComponentsInChildren<Button>(true))
        {
            if(button.name=="InteractionReticle")continue;
            var image=button.GetComponent<Image>();if(image==null)continue;
            image.material=hologram;
            Ensure<OblationHologram>(button.gameObject).accent=new Color(.13f,.73f,1);
            Ensure<OblationButtonFeedback>(button.gameObject);surfaces++;
        }
        string[] panels={"HudUI/HudCanvas/HudScreen/TopBar","HudUI/HudCanvas/HudScreen/PlanetDetails","HudUI/HudCanvas/HudScreen/QuickProduction","HudUI/HudCanvas/HudScreen/Chronicle"};
        foreach(string path in panels)
        {
            Transform panel=root.Find(path);if(panel==null)continue;Dress(panel,hologram,false);surfaces++;
        }
        foreach(var image in root.Find("GameUI").GetComponentsInChildren<Image>(true))
            if(image.name=="Card") { Dress(image.transform,hologram,true);surfaces++; }
        var profile=volume.sharedProfile;
        var bloom=Effect<Bloom>(profile);bloom.intensity.Override(1.05f);bloom.threshold.Override(.9f);bloom.scatter.Override(.76f);bloom.clamp.Override(12);bloom.highQualityFiltering.Override(true);
        var grade=Effect<ColorAdjustments>(profile);grade.postExposure.Override(.22f);grade.contrast.Override(16);grade.saturation.Override(10);
        Effect<Tonemapping>(profile).mode.Override(TonemappingMode.ACES);
        var split=Effect<SplitToning>(profile);split.shadows.Override(new Color(.43f,.48f,.55f));split.highlights.Override(new Color(.55f,.53f,.5f));split.balance.Override(10);
        Effect<ChromaticAberration>(profile).intensity.Override(.015f);
        var grain=Effect<FilmGrain>(profile);grain.intensity.Override(.045f);grain.response.Override(.8f);
        var vignette=Effect<Vignette>(profile);vignette.intensity.Override(.16f);vignette.smoothness.Override(.65f);
        camera.allowHDR=true;
        var data=camera.GetUniversalAdditionalCameraData();data.renderPostProcessing=true;
        data.antialiasing=AntialiasingMode.SubpixelMorphologicalAntiAliasing;data.antialiasingQuality=AntialiasingQuality.High;
        foreach(string path in new[]{"Assets/Settings/PC_RPAsset.asset","Assets/Settings/Mobile_RPAsset.asset"})
        {
            var pipeline=AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);if(pipeline==null)continue;
            pipeline.supportsHDR=true;
            var settings=new SerializedObject(pipeline);settings.FindProperty("m_ColorGradingMode").intValue=1;
            settings.FindProperty("m_AdditionalLightsPerObjectLimit").intValue=8;
            if(path.Contains("PC_"))settings.FindProperty("m_HDRColorBufferPrecision").intValue=1;
            settings.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(pipeline);
        }
        Transform rig=util.Find("Lighting");if(rig==null){rig=new GameObject("Lighting").transform;rig.SetParent(util,false);}
        LightAt(rig,"CyanFill",new Vector3(2,4,-5),new Color(.17f,.65f,1),3.5f,23);
        LightAt(rig,"VioletRim",new Vector3(-9,5,6),new Color(.5f,.16f,1),4.2f,26);
        foreach(var key in root.GetComponentsInChildren<Light>(true))if(key.type==LightType.Directional)
        {key.intensity=1.65f;key.color=new Color(1,.88f,.72f);key.transform.rotation=Quaternion.Euler(32,-35,0);EditorUtility.SetDirty(key);}
        var director=Ensure<OblationVisualDirector>(game.gameObject);
        director.volume=volume;director.sceneCamera=camera;director.pulseMaterial=pulse;
        director.effectsRoot=(Transform)serialized.FindProperty("effectsRoot").objectReferenceValue;
        serialized.FindProperty("visualDirector").objectReferenceValue=director;serialized.ApplyModifiedPropertiesWithoutUndo();
        Shader.SetGlobalFloat("_OblationEffects",1);Shader.SetGlobalFloat("_OblationMotion",1);
        foreach(UnityEngine.Object asset in new UnityEngine.Object[]{atmosphere,hologram,pulse,profile,director,data,game})EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        return "Saved 12 Shader Graph atmospheres, smooth planet meshes, HDR lighting/post effects and "+surfaces+" holographic UI surfaces.";
    }
    static void Dress(Transform transform,Material material,bool reveal)
    {
        var image=transform.GetComponent<Image>();if(image==null)return;
        image.material=material;var effect=Ensure<OblationHologram>(transform.gameObject);
        if(reveal)effect.revealGroup=Ensure<CanvasGroup>(transform.gameObject);
    }
    static void LightAt(Transform parent,string name,Vector3 position,Color tint,float intensity,float range)
    {
        Transform t=parent.Find(name);if(t==null){t=new GameObject(name,typeof(Light)).transform;t.SetParent(parent,false);}
        t.localPosition=position;Light light=t.GetComponent<Light>();light.type=LightType.Point;light.color=tint;light.intensity=intensity;light.range=range;light.shadows=LightShadows.None;
    }
    static T Ensure<T>(GameObject go) where T:Component {var component=go.GetComponent<T>();return component!=null?component:go.AddComponent<T>();}
    static Material Material(string name,Shader shader)
    {
        if(shader==null)throw new Exception("Missing shader for "+name);
        string path="Assets/Materials/Oblation/"+name+".mat";
        var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(mat==null){mat=new Material(shader);AssetDatabase.CreateAsset(mat,path);}else mat.shader=shader;
        return mat;
    }
    static T Effect<T>(VolumeProfile profile) where T:VolumeComponent
    {
        if(!profile.TryGet(out T value)){value=profile.Add<T>(true);AssetDatabase.AddObjectToAsset(value,profile);}
        value.active=true;EditorUtility.SetDirty(value);return value;
    }
    static Mesh Sphere()
    {
        const string path="Assets/Materials/Oblation/PlanetSphere.asset";
        Mesh mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(mesh!=null)return mesh;
        const int rows=48,columns=96;
        var vertices=new Vector3[(rows+1)*(columns+1)];var normals=new Vector3[vertices.Length];var uv=new Vector2[vertices.Length];
        var triangles=new int[rows*columns*6];int k=0;
        for(int y=0;y<=rows;y++)for(int x=0;x<=columns;x++)
        {
            float lat=y*Mathf.PI/rows,lon=x*2*Mathf.PI/columns;int i=y*(columns+1)+x;
            normals[i]=new Vector3(Mathf.Sin(lat)*Mathf.Cos(lon),Mathf.Cos(lat),Mathf.Sin(lat)*Mathf.Sin(lon));
            vertices[i]=normals[i]*.5f;uv[i]=new Vector2(x/(float)columns,y/(float)rows);
            if(y==rows||x==columns)continue;
            int n=i+columns+1;
            triangles[k++]=i;triangles[k++]=i+1;triangles[k++]=n;
            triangles[k++]=i+1;triangles[k++]=n+1;triangles[k++]=n;
        }
        mesh=new Mesh{name="Oblation Planet Sphere"};mesh.vertices=vertices;mesh.normals=normals;mesh.uv=uv;mesh.triangles=triangles;mesh.RecalculateBounds();mesh.RecalculateTangents();
        AssetDatabase.CreateAsset(mesh,path);return mesh;
    }
}
