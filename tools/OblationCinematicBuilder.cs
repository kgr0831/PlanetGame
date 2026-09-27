using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// Run after OblationExperienceBuilder when re-authoring the scene.
public static class OblationCinematicBuilder
{
    static SerializedObject game;
    static readonly Color Cyan = new Color(.2f,.87f,1);
    static readonly Color White = new Color(.94f,.97f,1);
    public static string Build()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play mode first.");
        var scene=EditorSceneManager.GetActiveScene();
        if(scene.path!="Assets/Scenes/SampleScene.unity")throw new InvalidOperationException("Open SampleScene first.");
        Transform root=GameObject.Find("Root").transform;
        game=new SerializedObject(root.Find("Util/Runtime/GameManager").GetComponent<OblationGame>());
        Transform hud=root.Find("HudUI/HudCanvas/HudScreen");
        CompactHud(hud);
        CinematicOverlay(hud);
        OperationTabs(root.Find("GameUI/GameCanvas/OperationsModal/Card"));
        Transform research=root.Find("GameUI/GameCanvas/DoctrineModal/Card");
        SetArray("researchChoiceCards",new UnityEngine.Object[]{research.Find("Viral"),research.Find("Foundry"),research.Find("Orbital")});
        root.Find("Util/World/Orbits").gameObject.SetActive(false);
        game.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(game.targetObject);
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        return "Saved compact HUD, contextual panels, operation tabs and interactive cinematic overlay.";
    }

    static void CompactHud(Transform hud)
    {
        Transform bar=hud.Find("TopBar");((RectTransform)bar).sizeDelta=new Vector2(0,76);
        string[] labels={"생체","광물","신경","공물","병력"};
        for(int i=0;i<5;i++)
        {
            RectTransform card=(RectTransform)bar.Find("ResourceCard"+i);
            card.anchorMin=new Vector2(i*.104f,0);card.anchorMax=new Vector2((i+1)*.104f,1);
            card.offsetMin=new Vector2(8,7);card.offsetMax=new Vector2(-4,-7);
            Label(card,"Name",labels[i],12,-24,62,25,19,White);
            Text value=Label(card,"Value","0",0,0,76,42,30,White);
            Place(value.rectTransform,Vector2.one,Vector2.one,new Vector2(-10,-16),new Vector2(76,42));
            value.alignment=TextAnchor.MiddleRight;
            Text rate=Label(card,"Rate","",12,-78,160,22,17,new Color(.7f,.8f,.86f));rate.gameObject.SetActive(false);
            Place((RectTransform)card.Find("Accent"),new Vector2(0,1),new Vector2(0,1),new Vector2(0,-23),new Vector2(2,25));
        }
        MoveButton(bar.Find("PauseButton"),Vector2.one,new Vector2(-18,-14),new Vector2(110,48),"메뉴");
        MoveButton(bar.Find("HelpButton"),Vector2.one,new Vector2(-136,-14),new Vector2(110,48),"도움말");
        MoveButton(bar.Find("SpeedButton"),Vector2.one,new Vector2(-254,-14),new Vector2(110,48),"×1");
        MoveButton(bar.Find("DoctrineButton"),Vector2.one,new Vector2(-372,-14),new Vector2(110,48),"연구");
        Bind("historyButton",Button(bar,"HistoryButton","기록",Vector2.one,new Vector2(-490,-14),new Vector2(110,48)));
        Bind("resourceDetailsButton",Button(bar,"ResourceDetailsButton","수입",Vector2.one,new Vector2(-608,-14),new Vector2(110,48)));
        Transform quick=hud.Find("QuickProduction");
        Label(quick,"Status","",18,-8,384,28,20,White);
        MoveButton(quick.Find("Produce"),new Vector2(0,1),new Vector2(18,-43),new Vector2(384,46),"정찰체 생산 · 생체 40");
        Place((RectTransform)quick.Find("Progress"),new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(0,5),new Vector2(384,3));
        Place((RectTransform)hud.Find("ResearchStatus"),new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(0,76),new Vector2(480,30));
        hud.Find("ResearchStatus").GetComponent<Text>().fontSize=20;
        Place((RectTransform)hud.Find("ResearchProgress"),new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(0,74),new Vector2(480,2));
        Place((RectTransform)hud.Find("ActionFeedback"),new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(0,26),new Vector2(760,42));
        Place((RectTransform)hud.Find("Chronicle"),Vector2.zero,Vector2.zero,new Vector2(22,138),new Vector2(440,220));
        Place((RectTransform)hud.Find("Ownership"),Vector2.zero,Vector2.zero,new Vector2(22,372),new Vector2(440,30));
        hud.Find("Chronicle").gameObject.SetActive(false);hud.Find("Ownership").gameObject.SetActive(false);
        Transform details=hud.Find("PlanetDetails");
        Label(details,"PlanetName","",20,-16,270,38,28,Cyan);
        Label(details,"PlanetMeta","",20,-58,380,28,19,White);
        Label(details,"PlanetTrait","",20,-220,380,80,20,White);
        Label(details,"Production","",20,-320,380,88,20,White);
        Label(details,"Hint","",20,-432,380,90,19,White);
        Place((RectTransform)details.Find("InteriorPreview"),new Vector2(0,1),new Vector2(0,1),new Vector2(310,-78),new Vector2(86,60));
        RectTransform interior=(RectTransform)details.Find("InteriorPreview/Image");interior.sizeDelta=new Vector2(82,56);
        Place((RectTransform)details.Find("BattleProgress"),new Vector2(.5f,1),new Vector2(.5f,1),new Vector2(0,-128),new Vector2(380,3));
        Bind("planetDetailsButton",Button(details,"DetailsButton","상세",Vector2.one,new Vector2(-16,-16),new Vector2(90,34)));
        MoveButton(details.Find("SurpriseButton"),new Vector2(0,1),new Vector2(20,-552),new Vector2(120,50),"역병");
        MoveButton(details.Find("OrbitalButton"),new Vector2(0,1),new Vector2(280,-552),new Vector2(120,50),"궤도");
        MoveButton(details.Find("OperationsButton"),new Vector2(0,1),new Vector2(20,-148),new Vector2(244,50),"행성 운영");
        details.gameObject.SetActive(false);
    }

    static void CinematicOverlay(Transform hud)
    {
        RectTransform overlay=Rect(hud,"CinematicOverlay",new Vector2(.5f,.5f),new Vector2(.5f,.5f),Vector2.zero,Vector2.zero);
        overlay.anchorMin=Vector2.zero;overlay.anchorMax=Vector2.one;overlay.offsetMin=overlay.offsetMax=Vector2.zero;
        Canvas canvas=Ensure<Canvas>(overlay.gameObject);canvas.overrideSorting=true;canvas.sortingOrder=30;
        Ensure<GraphicRaycaster>(overlay.gameObject);
        CanvasGroup group=Ensure<CanvasGroup>(overlay.gameObject);group.alpha=0;
        Bind("cinematicCanvas",canvas);Bind("cinematicGroup",group);
        RectTransform top=Rect(overlay,"LetterboxTop",new Vector2(.5f,1),new Vector2(.5f,1),Vector2.zero,new Vector2(0,58));
        top.anchorMin=new Vector2(0,1);top.anchorMax=Vector2.one;Paint(top,new Color(0,0,0,.92f));Bind("cinematicTopBar",top);
        RectTransform bottom=Rect(overlay,"LetterboxBottom",new Vector2(.5f,0),new Vector2(.5f,0),Vector2.zero,new Vector2(0,146));
        bottom.anchorMin=Vector2.zero;bottom.anchorMax=new Vector2(1,0);Paint(bottom,new Color(0,0,0,.96f));Bind("cinematicBottomBar",bottom);
        GameObject caption=(GameObject)game.FindProperty("tutorialRoot").objectReferenceValue;
        caption.transform.SetParent(overlay,false);
        Place((RectTransform)caption.transform,new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(0,18),new Vector2(1100,112));
        caption.GetComponent<Image>().color=Color.clear;caption.GetComponent<Image>().raycastTarget=false;
        if(caption.transform.Find("Accent")!=null)caption.transform.Find("Accent").gameObject.SetActive(false);
        CanvasGroup captionGroup=Ensure<CanvasGroup>(caption);Bind("cinematicCaption",captionGroup);
        Text title=Label(caption.transform,"Title","",0,-31,1100,44,34,White);title.alignment=TextAnchor.MiddleCenter;
        Text body=Label(caption.transform,"Body","",0,-78,1100,30,23,new Color(.75f,.86f,.91f));body.alignment=TextAnchor.MiddleCenter;
        Text chapter=Label(caption.transform,"Chapter","",0,-5,1100,22,16,Cyan);chapter.alignment=TextAnchor.MiddleCenter;Bind("cinematicChapter",chapter);
        RectTransform track=Rect(caption.transform,"BeatProgress",new Vector2(.5f,1),new Vector2(.5f,1),new Vector2(0,3),new Vector2(280,2));Paint(track,new Color(.1f,.16f,.2f));
        RectTransform fill=Rect(track,"Fill",Vector2.zero,Vector2.zero,Vector2.zero,Vector2.zero);fill.anchorMax=Vector2.one;fill.offsetMin=fill.offsetMax=Vector2.zero;Bind("cinematicProgress",Paint(fill,Cyan));
        Button skip=(Button)game.FindProperty("tutorialSkip").objectReferenceValue;skip.transform.SetParent(overlay,false);
        MoveButton(skip.transform,Vector2.one,new Vector2(-22,-9),new Vector2(200,40),"연출 건너뛰기 · Esc");
        ((Button)game.FindProperty("tutorialAction").objectReferenceValue).gameObject.SetActive(false);
        RectTransform reticle=Rect(overlay,"InteractionReticle",new Vector2(.5f,.5f),new Vector2(.5f,.5f),Vector2.zero,new Vector2(160,160));
        Image targetImage=Paint(reticle,Color.clear);targetImage.raycastTarget=true;
        Button target=Ensure<Button>(reticle.gameObject);target.targetGraphic=targetImage;target.navigation=new Navigation{mode=Navigation.Mode.Automatic};
        Bind("cinematicTargetButton",target);Bind("cinematicReticle",reticle);Bind("reticleGroup",Ensure<CanvasGroup>(reticle.gameObject));
        Vector2[] corners={Vector2.zero,new Vector2(1,0),Vector2.one,new Vector2(0,1)};
        for(int i=0;i<4;i++)
        {
            Vector2 corner=corners[i];
            Paint(Rect(reticle,"CornerH"+i,corner,corner,Vector2.zero,new Vector2(25,3)),Cyan);
            Paint(Rect(reticle,"CornerV"+i,corner,corner,Vector2.zero,new Vector2(3,25)),Cyan);
        }
        RectTransform scan=Rect(reticle,"Scan",new Vector2(.5f,.5f),new Vector2(.5f,.5f),Vector2.zero,new Vector2(-28,2));
        scan.anchorMin=new Vector2(0,.5f);scan.anchorMax=new Vector2(1,.5f);Paint(scan,new Color(.2f,.87f,1,.2f));Bind("cinematicScanLine",scan);
        Text targetLabel=Label(reticle,"TargetLabel","행성 선택",0,0,300,30,22,Cyan);
        Place(targetLabel.rectTransform,new Vector2(.5f,0),new Vector2(.5f,1),new Vector2(0,-12),new Vector2(300,30));targetLabel.alignment=TextAnchor.MiddleCenter;Bind("cinematicTargetLabel",targetLabel);
        reticle.gameObject.SetActive(false);overlay.gameObject.SetActive(false);
    }

    static void OperationTabs(Transform card)
    {
        ((RectTransform)card).sizeDelta=new Vector2(1180,654);
        string[] labels={"병력 생산","행성 개발","강화 · 절멸"};Button[] tabs=new Button[3];
        for(int i=0;i<3;i++)tabs[i]=Button(card,"OperationTab"+i,labels[i],new Vector2(0,1),new Vector2(30+i*373,-142),new Vector2(350,48));
        SetArray("operationTabs",tabs);
        string[] headings={"UnitsHeading","UpgradeHeading","ExterminatusHeading"};Text[] headingRefs=new Text[3];
        for(int i=0;i<3;i++){headingRefs[i]=card.Find(headings[i]).GetComponent<Text>();headingRefs[i].rectTransform.anchoredPosition=new Vector2(30,i==2?-408:-218);}
        SetArray("operationHeadings",headingRefs);
        for(int i=0;i<8;i++)Place((RectTransform)card.Find("Unit"+i),new Vector2(0,1),new Vector2(0,1),new Vector2(30+i%4*280,-262-i/4*82),new Vector2(260,70));
        Place((RectTransform)card.Find("AntennaButton"),new Vector2(0,1),new Vector2(0,1),new Vector2(30,-260),new Vector2(530,64));
        for(int i=0;i<3;i++)Place((RectTransform)card.Find("Trait"+i),new Vector2(0,1),new Vector2(0,1),new Vector2(30+i*373,-350),new Vector2(350,64));
        for(int i=0;i<6;i++)Place((RectTransform)card.Find("Upgrade"+i),new Vector2(0,1),new Vector2(0,1),new Vector2(30+i%3*373,-264-i/3*66),new Vector2(350,52));
        for(int i=0;i<3;i++)Place((RectTransform)card.Find("Exterminatus"+i),new Vector2(0,1),new Vector2(0,1),new Vector2(30+i*373,-451),new Vector2(350,64));
        Place((RectTransform)card.Find("CloseButton"),new Vector2(0,1),new Vector2(0,1),new Vector2(430,-580),new Vector2(320,50));
    }
    static T Ensure<T>(GameObject go)where T:Component{T component=go.GetComponent<T>();if(component==null)component=go.AddComponent<T>();return component;}
    static RectTransform Rect(Transform parent,string name,Vector2 anchor,Vector2 pivot,Vector2 position,Vector2 size)
    {var rect=parent.Find(name)as RectTransform;if(rect==null){rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();rect.SetParent(parent,false);}Place(rect,anchor,pivot,position,size);return rect;}
    static void Place(RectTransform rect,Vector2 anchor,Vector2 pivot,Vector2 position,Vector2 size)
    {rect.anchorMin=rect.anchorMax=anchor;rect.pivot=pivot;rect.anchoredPosition=position;rect.sizeDelta=size;rect.localScale=Vector3.one;}
    static Image Paint(RectTransform rect,Color color){Image image=Ensure<Image>(rect.gameObject);image.color=color;image.raycastTarget=false;return image;}
    static Text Label(Transform parent,string name,string text,float x,float y,float w,float h,int fontSize,Color color)
    {var rect=Rect(parent,name,new Vector2(0,1),new Vector2(0,1),new Vector2(x,y),new Vector2(w,h));Text label=Ensure<Text>(rect.gameObject);label.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");label.text=text;label.fontSize=fontSize;label.color=color;label.raycastTarget=false;label.horizontalOverflow=HorizontalWrapMode.Wrap;label.verticalOverflow=VerticalWrapMode.Truncate;return label;}
    static Button Button(Transform parent,string name,string text,Vector2 anchor,Vector2 position,Vector2 size)
    {var rect=Rect(parent,name,anchor,anchor,position,size);var image=Paint(rect,new Color(.04f,.18f,.25f,.95f));image.raycastTarget=true;var button=Ensure<Button>(rect.gameObject);button.targetGraphic=image;button.navigation=new Navigation{mode=Navigation.Mode.Automatic};Text label=Label(rect,"Label",text,0,0,size.x,size.y,20,White);label.alignment=TextAnchor.MiddleCenter;return button;}
    static void MoveButton(Transform transform,Vector2 anchor,Vector2 position,Vector2 size,string text)
    {Place((RectTransform)transform,anchor,anchor,position,size);Text label=transform.GetComponentInChildren<Text>(true);label.text=text;label.fontSize=20;}
    static void Bind(string name,UnityEngine.Object value)=>game.FindProperty(name).objectReferenceValue=value;
    static void SetArray(string name,UnityEngine.Object[] values){var property=game.FindProperty(name);property.arraySize=values.Length;for(int i=0;i<values.Length;i++)property.GetArrayElementAtIndex(i).objectReferenceValue=values[i];}
}
