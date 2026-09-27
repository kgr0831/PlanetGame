using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.IO;
using UnityEngine;
using UnityEditor;

// Uses the installed Shader Graph 17 APIs so Unity owns serialization and node/slot IDs.
public static class OblationAtmosphereGraph
{
    const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
    static Type TypeOf(string name)=>AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType(name)).First(t=>t!=null);
    static object New(string name)=>Activator.CreateInstance(TypeOf(name),true);
    static object Get(object o,string name)=>o.GetType().GetProperty(name,Flags)?.GetValue(o)??o.GetType().GetField(name,Flags)?.GetValue(o);
    static void Set(object o,string name,object value)
    {
        var prop=o.GetType().GetProperty(name,Flags);
        if(prop!=null){if(prop.PropertyType.IsEnum)value=Enum.Parse(prop.PropertyType,value.ToString());prop.SetValue(o,value);return;}
        o.GetType().GetField(name,Flags).SetValue(o,value);
    }
    static object Call(object o,string method,params object[] args)
    {
        var m=o.GetType().GetMethods(Flags).First(x=>x.Name==method&&!x.IsGenericMethod&&x.GetParameters().Length==args.Length);
        try{return m.Invoke(o,args);}catch(TargetInvocationException e){throw new Exception(method+": "+e.InnerException,e);}
    }
    static object Node(object graph,string kind,int x,int y)
    {
        object node=New("UnityEditor.ShaderGraph."+kind);
        Call(graph,"AddNode",node,false);
        var draw=Get(node,"drawState");Set(draw,"position",new Rect(x,y,210,140));Set(node,"drawState",draw);
        return node;
    }
    static object Property(object graph,string kind,string name,string reference,object value,int x,int y)
    {
        object property=New("UnityEditor.ShaderGraph.Internal."+kind);
        Set(property,"displayName",name);Set(property,"overrideReferenceName",reference);Set(property,"value",value);
        if(kind=="ColorShaderProperty")Set(property,"colorMode","HDR");
        Call(graph,"AddGraphInput",property,-1);
        object node=Node(graph,"PropertyNode",x,y);Set(node,"property",property);return node;
    }
    static void Edge(object graph,object from,int output,object to,int input)=>Call(graph,"Connect",Call(from,"GetSlotReference",output),Call(to,"GetSlotReference",input));
    public static string Build()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play mode.");
        object graph=New("UnityEditor.ShaderGraph.GraphData");Set(graph,"path","Oblation");Call(graph,"AddContexts");
        object target=New("UnityEditor.Rendering.Universal.ShaderGraph.UniversalTarget");
        Call(target,"TrySetActiveSubTarget",TypeOf("UnityEditor.Rendering.Universal.ShaderGraph.UniversalUnlitSubTarget"));
        Set(target,"surfaceType","Transparent");Set(target,"alphaMode","Additive");Set(target,"renderFace","Front");
        Set(target,"zWriteControl","ForceDisabled");Set(target,"castShadows",false);
        Array targets=Array.CreateInstance(TypeOf("UnityEditor.ShaderGraph.Target"),1);targets.SetValue(target,0);
        Type descriptor=TypeOf("UnityEditor.ShaderGraph.BlockFieldDescriptor");
        Array blocks=Array.CreateInstance(descriptor,5);
        string[] names={"VertexDescription.Position","VertexDescription.Normal","VertexDescription.Tangent","SurfaceDescription.BaseColor","SurfaceDescription.Alpha"};
        for(int i=0;i<names.Length;i++)
        {
            string[] parts=names[i].Split('.');
            Type owner=TypeOf("UnityEditor.ShaderGraph.BlockFields+"+parts[0]);
            blocks.SetValue(owner.GetField(parts[1],Flags).GetValue(null),i);
        }
        Call(graph,"InitializeOutputs",targets,blocks);
        object tint=Property(graph,"ColorShaderProperty","Atmosphere Tint","_AtmosphereTint",new Color(.12f,.55f,1,1),-800,-300);
        object intensity=Property(graph,"Vector1ShaderProperty","HDR Intensity","_Intensity",3f,-800,-100);
        object power=Property(graph,"Vector1ShaderProperty","Rim Falloff","_RimPower",3.5f,-800,150);
        object opacity=Property(graph,"Vector1ShaderProperty","Atmosphere Opacity","_Opacity",.6f,-470,420);
        object fresnel=Node(graph,"FresnelNode",-480,160);
        object radiance=Node(graph,"MultiplyNode",-460,-240);
        object alpha=Node(graph,"MultiplyNode",-180,160);
        Edge(graph,power,0,fresnel,2);Edge(graph,tint,0,radiance,0);Edge(graph,intensity,0,radiance,1);
        Edge(graph,fresnel,3,alpha,0);Edge(graph,opacity,0,alpha,1);
        foreach(object entry in (IEnumerable)Get(Get(graph,"fragmentContext"),"blocks"))
        {
            object block=Get(entry,"value")??entry;
            string name=(string)Get(block,"name");
            if(name.EndsWith("BaseColor"))Edge(graph,radiance,2,block,0);
            if(name.EndsWith("Alpha"))Edge(graph,alpha,2,block,0);
        }
        Call(graph,"ValidateGraph");
        string json=(string)TypeOf("UnityEditor.ShaderGraph.Serialization.MultiJson").GetMethod("Serialize",Flags).Invoke(null,new[]{graph});
        const string path="Assets/Shaders/OblationAtmosphere.shadergraph";
        File.WriteAllText(path,json);AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
        if(AssetDatabase.LoadAssetAtPath<Shader>(path)==null)throw new Exception("Atmosphere graph did not import as a shader.");
        return "Saved URP additive atmosphere Shader Graph: Fresnel / HDR tint / intensity / opacity.";
    }
}
