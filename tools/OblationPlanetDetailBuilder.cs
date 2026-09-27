using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class OblationPlanetDetailBuilder
{
    public static string Build()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play mode first.");
        var scene=EditorSceneManager.GetActiveScene();
        if(scene.path!="Assets/Scenes/SampleScene.unity")throw new InvalidOperationException("Open SampleScene.");
        const string path="Assets/Materials/Oblation/PlanetClouds.mat";
        var clouds=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(clouds==null){clouds=new Material(Shader.Find("Oblation/Planet Clouds"));AssetDatabase.CreateAsset(clouds,path);}
        var atmosphere=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Oblation/Atmosphere.mat");
        atmosphere.SetFloat("_Intensity",1.8f);atmosphere.SetFloat("_Opacity",.3f);atmosphere.SetFloat("_RimPower",4.2f);EditorUtility.SetDirty(atmosphere);
        var root=GameObject.Find("Root");int count=0;
        foreach(var view in root.GetComponentsInChildren<OblationPlanetView>(true))
        {
            var material=view.bodyRenderer.sharedMaterial;
            material.SetFloat("_Relief",.45f);material.SetFloat("_CloudCoverage",.565f+(view.index%3)*.01f);
            EditorUtility.SetDirty(material);
            view.atmosphereRenderer.transform.localScale=Vector3.one*1.055f;
            view.haloRenderer.transform.localScale=Vector3.one*1.085f;
            view.haloRenderer.sharedMaterial.SetFloat("_Opacity",.28f);EditorUtility.SetDirty(view.haloRenderer.sharedMaterial);
            Transform shell=view.bodyRenderer.transform.Find("Clouds");
            if(shell==null){shell=new GameObject("Clouds",typeof(MeshFilter),typeof(MeshRenderer)).transform;shell.SetParent(view.bodyRenderer.transform,false);}
            shell.localScale=Vector3.one*1.014f;
            shell.GetComponent<MeshFilter>().sharedMesh=view.bodyRenderer.GetComponent<MeshFilter>().sharedMesh;
            var renderer=shell.GetComponent<MeshRenderer>();renderer.sharedMaterial=clouds;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            view.cloudRenderer=renderer;EditorUtility.SetDirty(view);count++;
        }
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        return "Saved detailed terrain and independently lit cloud shells for "+count+" planets.";
    }
}
