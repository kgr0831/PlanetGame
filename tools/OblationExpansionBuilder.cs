using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class OblationExpansionBuilder
{
    static Font font;
    static OblationUiAudio audio;
    static SerializedObject refs;
    public static string Build()
    {
        if(EditorApplication.isPlaying)throw new Exception("Stop Play mode first.");
        var scene=EditorSceneManager.GetActiveScene();if(scene.path!="Assets/Scenes/SampleScene.unity")throw new Exception("Open SampleScene.");
        Transform root=GameObject.Find("Root").transform;
        var game=root.Find("Util/Runtime/GameManager").GetComponent<OblationGame>();refs=new SerializedObject(game);
        font=(Font)refs.FindProperty("interfaceFont").objectReferenceValue;audio=game.GetComponent<OblationUiAudio>();
        var catalog=(OblationCatalogSO)refs.FindProperty("catalog").objectReferenceValue;
        Transform planetRoot=root.Find("Util/World/Planets"),routeRoot=root.Find("Util/World/Routes");
        Transform labels=root.Find("HudUI/HudCanvas/HudScreen/PlanetLabels");
        var oldViews=refs.FindProperty("planetViews");var oldLabels=refs.FindProperty("planetLabels");
        var bases=new OblationPlanetView[12];for(int i=0;i<12;i++)bases[i]=(OblationPlanetView)oldViews.GetArrayElementAtIndex(i).objectReferenceValue;
        var labelTemplate=(Text)oldLabels.GetArrayElementAtIndex(0).objectReferenceValue;
        var worlds=new PlanetDefinitionSO[OblationGalaxyLayout.Names.Length];Array.Copy(catalog.planets,worlds,Mathf.Min(worlds.Length,catalog.planets.Length));
        var views=new OblationPlanetView[worlds.Length];var texts=new Text[worlds.Length];
        for(int i=0;i<worlds.Length;i++)
        {
            string name=OblationGalaxyLayout.Names[i];Transform existing=planetRoot.Find(name);
            var view=existing!=null?existing.GetComponent<OblationPlanetView>():UnityEngine.Object.Instantiate(bases[i%12],planetRoot);
            view.name=name;view.index=i;view.transform.localPosition=OblationGalaxyLayout.Positions[i];
            foreach(var marker in view.GetComponentsInChildren<PlanetMarker>(true))marker.index=i;
            if(i>=12)
            {
                string path="Assets/Materials/Oblation/"+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(material==null){material=new Material(bases[i%12].bodyRenderer.sharedMaterial);material.name=name;AssetDatabase.CreateAsset(material,path);}
                material.SetFloat("_Seed",21+i*7.31f);material.SetFloat("_StyleVariant",(i%7)*.27f);view.bodyRenderer.sharedMaterial=material;
                EditorUtility.SetDirty(material);
                if(worlds[i]==null)
                {
                    worlds[i]=UnityEngine.Object.Instantiate(catalog.planets[i%12]);worlds[i].name=name;worlds[i].id=name;worlds[i].displayName=name;
                    worlds[i].maxPopulation*=1+(i/12)*.12f;worlds[i].defense+=i/12*3;
                    worlds[i].traitDescription=(i<24?"중간 성역 · ":"외곽 성역 · ")+worlds[i].traitDescription;
                    AssetDatabase.AddObjectToAsset(worlds[i],catalog);
                }
            }
            Transform label=labels.Find(name+" Label");
            if(i<oldLabels.arraySize)texts[i]=(Text)oldLabels.GetArrayElementAtIndex(i).objectReferenceValue;
            else texts[i]=label!=null?label.GetComponent<Text>():UnityEngine.Object.Instantiate(labelTemplate,labels);
            texts[i].name=name+" Label";texts[i].text=name;texts[i].raycastTarget=false;
            views[i]=view;EditorUtility.SetDirty(view);EditorUtility.SetDirty(worlds[i]);
        }
        catalog.planets=worlds;EditorUtility.SetDirty(catalog);
        var oldRoutes=refs.FindProperty("routeViews");var template=(LineRenderer)oldRoutes.GetArrayElementAtIndex(0).objectReferenceValue;
        var routes=new LineRenderer[OblationGalaxyLayout.Edges.GetLength(0)];
        for(int i=0;i<routes.Length;i++)
        {
            Transform existing=routeRoot.Find("Route "+i);
            routes[i]=i<oldRoutes.arraySize?(LineRenderer)oldRoutes.GetArrayElementAtIndex(i).objectReferenceValue:existing!=null?existing.GetComponent<LineRenderer>():UnityEngine.Object.Instantiate(template,routeRoot);
            routes[i].name="Route "+i;routes[i].positionCount=2;routes[i].useWorldSpace=true;
            routes[i].SetPosition(0,views[OblationGalaxyLayout.Edges[i,0]].transform.position);routes[i].SetPosition(1,views[OblationGalaxyLayout.Edges[i,1]].transform.position);
            routes[i].startWidth=routes[i].endWidth=.045f;EditorUtility.SetDirty(routes[i]);
        }
        ArrayRef("planetViews",views);ArrayRef("routeViews",routes);ArrayRef("planetLabels",texts);
        var markers=new Text[worlds.Length];
        for(int i=0;i<markers.Length;i++)markers[i]=Text(labels,"MapMarker "+i,"◇",new Vector2(.5f,.5f),Vector2.zero,new Vector2(32,36),28);
        ArrayRef("planetMapMarkers",markers);
        root.Find("Util/World/Background/Starfield").localScale=Vector3.one*1400;
        ((Camera)refs.FindProperty("gameCamera").objectReferenceValue).farClipPlane=1200;
        var director=(OblationVisualDirector)refs.FindProperty("visualDirector").objectReferenceValue;
        const string fxPath="Assets/Materials/Oblation/ActionFx.mat";var fx=AssetDatabase.LoadAssetAtPath<Material>(fxPath);
        if(fx==null){fx=new Material(Shader.Find("Oblation/Action FX"));AssetDatabase.CreateAsset(fx,fxPath);}director.actionMaterial=fx;EditorUtility.SetDirty(director);
        BuildStory(root.Find("GameUI"));
        Transform hud=root.Find("HudUI/HudCanvas/HudScreen");
        var overview=Button(hud,"MapOverview","전체 지도 · M",new Vector2(0,1),new Vector2(135,-120),new Vector2(230,52));Ref("mapOverviewButton",overview);
        var hint=Text(hud,"NavigationHint","휠 확대/축소 · 우클릭 드래그 · 행성 클릭 포커스",new Vector2(.5f,0),new Vector2(0,28),new Vector2(740,42),28);Ref("navigationHint",hint);
        refs.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(game);
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        return "Saved 36 planets, 69 routes, extended starfield, story overlay, navigation controls and seven action FX variants.";
    }
    static void BuildStory(Transform parent)
    {
        var overlay=Rect(parent,"StoryOverlay",new Vector2(.5f,.5f),Vector2.zero,new Vector2(1920,1080));
        var canvas=Ensure<Canvas>(overlay.gameObject);canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=60;
        var scaler=Ensure<CanvasScaler>(overlay.gameObject);scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
        Ensure<GraphicRaycaster>(overlay.gameObject);var blocker=Ensure<Image>(overlay.gameObject);blocker.color=Color.clear;blocker.raycastTarget=true;
        var top=Rect(overlay,"Top",new Vector2(.5f,1),new Vector2(0,-36),new Vector2(1920,72));Ensure<Image>(top.gameObject).color=new Color(0,0,0,.88f);
        var bottom=Rect(overlay,"Bottom",new Vector2(.5f,0),new Vector2(0,160),new Vector2(1920,320));Ensure<Image>(bottom.gameObject).color=new Color(.005f,.012f,.024f,.94f);
        Ref("storyCanvas",canvas);
        Ref("storyChapterLabel",Text(overlay,"Chapter","기억의 신호",new Vector2(.5f,0),new Vector2(0,288),new Vector2(1500,34),26));
        var title=Text(overlay,"Title","긴 침묵",new Vector2(.5f,0),new Vector2(0,232),new Vector2(1600,70),56);Ref("storyTitle",title);
        var body=Text(overlay,"Body","",new Vector2(.5f,0),new Vector2(0,139),new Vector2(1600,118),40);Ref("storyBody",body);
        foreach(var label in new[]{title,body}){var effect=Ensure<OblationTerminalText>(label.gameObject);effect.scrambleDuration=.6f;effect.audioFeedback=audio;}
        Ref("storyNextButton",Button(overlay,"Next","다음 장면 · Space",new Vector2(1,0),new Vector2(-200,38),new Vector2(330,54)));
        Ref("storySkipButton",Button(overlay,"Skip","장면 건너뛰기 · Esc",new Vector2(1,1),new Vector2(-205,-35),new Vector2(360,50)));
        var track=Rect(overlay,"Progress",new Vector2(.5f,0),new Vector2(0,310),new Vector2(1600,2));Ensure<Image>(track.gameObject).color=new Color(.1f,.2f,.26f);
        var fill=Rect(track,"Fill",Vector2.zero,Vector2.zero,Vector2.zero);fill.anchorMax=Vector2.one;fill.pivot=Vector2.zero;fill.offsetMin=fill.offsetMax=Vector2.zero;
        var image=Ensure<Image>(fill.gameObject);image.color=Color.cyan;image.raycastTarget=false;Ref("storyProgress",image);
        overlay.gameObject.SetActive(false);
    }
    static RectTransform Rect(Transform parent,string name,Vector2 anchor,Vector2 position,Vector2 size)
    {
        var found=parent.Find(name);var rect=found!=null?(RectTransform)found:new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent,false);rect.anchorMin=rect.anchorMax=anchor;rect.pivot=new Vector2(.5f,.5f);rect.anchoredPosition=position;rect.sizeDelta=size;return rect;
    }
    static Text Text(Transform parent,string name,string value,Vector2 anchor,Vector2 position,Vector2 size,int fontSize)
    {
        var rect=Rect(parent,name,anchor,position,size);var text=Ensure<Text>(rect.gameObject);text.font=font;text.text=value;text.fontSize=fontSize;text.color=new Color(.82f,.94f,1);
        text.alignment=TextAnchor.MiddleCenter;text.raycastTarget=false;text.horizontalOverflow=HorizontalWrapMode.Wrap;text.verticalOverflow=VerticalWrapMode.Overflow;return text;
    }
    static Button Button(Transform parent,string name,string value,Vector2 anchor,Vector2 position,Vector2 size)
    {
        var rect=Rect(parent,name,anchor,position,size);var image=Ensure<Image>(rect.gameObject);image.color=new Color(.025f,.12f,.2f,.95f);image.raycastTarget=true;
        image.material=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Oblation/HolographicUI.mat");var button=Ensure<Button>(rect.gameObject);button.targetGraphic=image;
        var label=Text(rect,"Label",value,new Vector2(.5f,.5f),Vector2.zero,size-new Vector2(12,0),36);label.resizeTextForBestFit=true;label.resizeTextMinSize=28;label.resizeTextMaxSize=36;
        Ensure<OblationHologram>(rect.gameObject).audioFeedback=audio;Ensure<OblationButtonFeedback>(rect.gameObject).audioFeedback=audio;return button;
    }
    static T Ensure<T>(GameObject go) where T:Component
    {T component=go.GetComponent<T>();return component!=null?component:go.AddComponent<T>();}
    static void Ref(string name,UnityEngine.Object value)=>refs.FindProperty(name).objectReferenceValue=value;
    static void ArrayRef(string name,UnityEngine.Object[] values){var prop=refs.FindProperty(name);prop.arraySize=values.Length;for(int i=0;i<values.Length;i++)prop.GetArrayElementAtIndex(i).objectReferenceValue=values[i];}
}
