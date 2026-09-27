using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

// Close-up of the saved scene, with the same camera/body pose for visual comparison.
public static class OblationPlanetReview
{
    public static Task<string> Before()=>Capture("planet_before",0);
    public static Task<string> After()=>Capture("planet_after",0);
    public static Task<string> Alternate()=>Capture("planet_alternate",2);
    public static string VerifyVariation()
    {
        if(!EditorApplication.isPlaying)throw new InvalidOperationException("Enter Play mode first.");
        var root=GameObject.Find("Root");var game=root.GetComponentInChildren<OblationGame>();
        var view=Array.Find(root.GetComponentsInChildren<OblationPlanetView>(true),p=>p.index==0);
        var authored=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Oblation/VESPER.mat");
        float assetSeed=authored.GetFloat("_Seed");Color assetColor=authored.GetColor("_BaseColor");
        int previousSeed=view.VisualSeed;Quaternion rotation=view.bodyRenderer.transform.localRotation;
        var gameplayRandom=UnityEngine.Random.state;
        try
        {
            view.ApplyVisualSeed(314159);var material=view.bodyRenderer.sharedMaterial;
            float seed=material.GetFloat("_Seed");Color color=material.GetColor("_BaseColor");Quaternion pose=view.bodyRenderer.transform.localRotation;
            view.ApplyVisualSeed(314159);
            if(material.GetFloat("_Seed")!=seed||material.GetColor("_BaseColor")!=color||view.bodyRenderer.transform.localRotation!=pose)throw new Exception("A visual seed must reproduce the same appearance.");
            view.ApplyVisualSeed(271828);
            if(material.GetFloat("_Seed")==seed)throw new Exception("Different visual seeds produced the same geography.");
            if(!UnityEngine.Random.state.Equals(gameplayRandom))throw new Exception("Visual randomness changed the gameplay random stream.");
            if(authored.GetFloat("_Seed")!=assetSeed||authored.GetColor("_BaseColor")!=assetColor)throw new Exception("Appearance variation modified the authored material.");
        }
        finally{view.ApplyVisualSeed(previousSeed);view.bodyRenderer.transform.localRotation=rotation;}
        int campaignSeed=game.AppearanceSeed;
        typeof(OblationGame).GetMethod("ResetCampaign",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(game,new object[]{false});
        if(game.AppearanceSeed==campaignSeed)throw new Exception("New campaign did not create a fresh appearance seed.");
        return "Same seed reproduces appearance; new seed changes geography; authored materials and gameplay random stream stay intact; each campaign receives a new appearance seed.";
    }
    public static async Task<string> Gallery()
    {
        var views=GameObject.Find("Root").GetComponentsInChildren<OblationPlanetView>(true);
        Array.Sort(views,(a,b)=>a.index.CompareTo(b.index));
        int rows=Mathf.CeilToInt(views.Length/4f);
        var atlas=new Texture2D(1920,rows*270,TextureFormat.RGB24,false);
        var target=RenderTexture.GetTemporary(480,270,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
        RenderTexture previous=RenderTexture.active;
        try
        {
            for(int i=0;i<views.Length;i++)
            {
                string path=await Capture("planet_"+views[i].name,views[i].index);
                var source=new Texture2D(2,2);source.LoadImage(System.IO.File.ReadAllBytes(path));
                Graphics.Blit(source,target);RenderTexture.active=target;
                atlas.ReadPixels(new Rect(0,0,480,270),(i%4)*480,(rows-1-i/4)*270,false);UnityEngine.Object.Destroy(source);
            }
            atlas.Apply();string output="Temp/planet_variety.png";System.IO.File.WriteAllBytes(output,atlas.EncodeToPNG());return System.IO.Path.GetFullPath(output);
        }
        finally{RenderTexture.active=previous;RenderTexture.ReleaseTemporary(target);UnityEngine.Object.Destroy(atlas);}
    }
    static async Task<string> Capture(string name,int index)
    {
        if(!EditorApplication.isPlaying)throw new InvalidOperationException("Enter Play mode first.");
        Transform root=GameObject.Find("Root").transform;
        var game=root.GetComponentInChildren<OblationGame>();
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
        Camera camera=(Camera)typeof(OblationGame).GetField("gameCamera",flags).GetValue(game);
        var views=root.GetComponentsInChildren<OblationPlanetView>(true);
        var planet=Array.Find(views,p=>p.index==index);
        var activePlanets=new List<GameObject>();
        var routes=root.Find("Util/World/Routes").gameObject;bool routesActive=routes.activeSelf;
        Vector3 position=camera.transform.position;Quaternion rotation=camera.transform.rotation;float fov=camera.fieldOfView;
        Quaternion bodyRotation=planet.bodyRenderer.transform.rotation;
        var canvases=root.GetComponentsInChildren<Canvas>(true);var enabledCanvases=new List<Canvas>();
        bool wasEnabled=game.enabled;float motion=Shader.GetGlobalFloat("_OblationMotion");
        try
        {
            game.enabled=false;Shader.SetGlobalFloat("_OblationMotion",0);
            foreach(var canvas in canvases)if(canvas.enabled){enabledCanvases.Add(canvas);canvas.enabled=false;}
            foreach(var view in views)if(view!=planet&&view.gameObject.activeSelf){activePlanets.Add(view.gameObject);view.gameObject.SetActive(false);}
            routes.SetActive(false);
            planet.bodyRenderer.transform.rotation=Quaternion.Euler(12,35,0);
            camera.transform.position=planet.transform.position+new Vector3(1.15f,.55f,-2.25f).normalized*planet.bodyRenderer.transform.lossyScale.x*(planet.ringRenderer!=null?3.6f:2.2f);
            camera.transform.LookAt(planet.transform.position);camera.fieldOfView=34;
            await Task.Delay(300);
            string path="Temp/"+name+".png";ScreenCapture.CaptureScreenshot(path);
            await Task.Delay(250);return System.IO.Path.GetFullPath(path);
        }
        finally
        {
            camera.transform.SetPositionAndRotation(position,rotation);camera.fieldOfView=fov;
            planet.bodyRenderer.transform.rotation=bodyRotation;
            foreach(var canvas in enabledCanvases)canvas.enabled=true;
            foreach(var active in activePlanets)active.SetActive(true);routes.SetActive(routesActive);
            Shader.SetGlobalFloat("_OblationMotion",motion);game.enabled=wasEnabled;
        }
    }
}
