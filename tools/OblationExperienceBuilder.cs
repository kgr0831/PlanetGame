using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

// Authors persistent UI and URP settings. Run outside Play mode with the Unity CLI.
public static class OblationExperienceBuilder
{
    static readonly Color Panel = new Color(.025f, .052f, .085f, .98f);
    static readonly Color Cyan = new Color(.2f, .87f, 1f);
    static readonly Color White = new Color(.94f, .97f, 1f);
    static SerializedObject game;

    public static string Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode first.");
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/SampleScene.unity") throw new InvalidOperationException("Open SampleScene first.");
        Transform root = GameObject.Find("Root").transform;
        game = new SerializedObject(root.Find("Util/Runtime/GameManager").GetComponent<OblationGame>());
        Transform hud = root.Find("HudUI/HudCanvas/HudScreen");
        Transform canvas = root.Find("GameUI/GameCanvas");
        BuildResourceHud(hud);
        BuildTutorial(hud);
        ReflowDetails(hud);
        ReflowMenus(canvas);
        ConfigureUrp(root);
        foreach (Button button in root.GetComponentsInChildren<Button>(true))
        {
            button.navigation = new Navigation { mode = Navigation.Mode.Automatic };
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(.72f, 1, 1);
            colors.selectedColor = new Color(.65f, 1, 1);
            colors.pressedColor = new Color(.45f, .8f, .95f);
            colors.disabledColor = new Color(.5f, .57f, .64f, 1);
            colors.fadeDuration = .08f;
            button.colors = colors;
            if (button.GetComponent<OblationButtonFeedback>() == null) button.gameObject.AddComponent<OblationButtonFeedback>();
            Text label = button.GetComponentInChildren<Text>(true);
            if (label != null) { label.fontSize = Mathf.Max(19, label.fontSize); label.verticalOverflow = VerticalWrapMode.Truncate; }
        }
        foreach (Slider slider in root.GetComponentsInChildren<Slider>(true))
            slider.navigation = new Navigation { mode = Navigation.Mode.Automatic };
        game.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(game.targetObject);
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(scene);
        return "Saved readable resource HUD, guided tutorial, accessibility controls and URP lighting/post-processing.";
    }

    static void BuildResourceHud(Transform hud)
    {
        RectTransform bar = hud.Find("TopBar") as RectTransform;
        bar.sizeDelta = new Vector2(0, 132);
        bar.GetComponent<Image>().color = Panel;
        foreach (string name in new[] { "Brand", "Resources", "Controls" }) bar.Find(name).gameObject.SetActive(false);
        Canvas overlay = Ensure<Canvas>(bar.gameObject);
        overlay.overrideSorting = true; overlay.sortingOrder = 20;
        if (bar.GetComponent<GraphicRaycaster>() == null) bar.gameObject.AddComponent<GraphicRaycaster>();
        overlay.overrideSorting = true; overlay.sortingOrder = 20;
        Bind("resourceCanvas",overlay);
        EditorUtility.SetDirty(overlay);
        string[] names = { "생체 물질", "광물 잔해", "신경 에너지", "공물", "전투 병력" };
        Color[] accents = { new Color(.5f,1,.65f), new Color(1,.78f,.4f), new Color(.66f,.69f,1), new Color(1,.56f,.75f), Cyan };
        Text[] values = new Text[5], rates = new Text[5];
        Image[] cards = new Image[5];
        for (int i = 0; i < 5; i++)
        {
            RectTransform card = Rect(bar, "ResourceCard" + i, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            card.anchorMin = new Vector2(i * .148f, 0); card.anchorMax = new Vector2((i + 1) * .148f, 1);
            card.offsetMin = new Vector2(12, 12); card.offsetMax = new Vector2(-2, -12);
            cards[i] = Paint(card, Panel, false);
            Paint(Rect(card, "Accent", new Vector2(0,1), new Vector2(0,1), Vector2.zero, new Vector2(4,108)), accents[i], false);
            Label(card, "Name", names[i], 14, -9, 250, 26, 21, accents[i]);
            values[i] = Label(card, "Value", "0", 14, -33, 245, 43, 36, White);
            rates[i] = Label(card, "Rate", "+0/분", 14, -79, 245, 24, 18, White);
            foreach (Text label in card.GetComponentsInChildren<Text>())
            {
                label.rectTransform.anchorMax = new Vector2(1,1);
                label.rectTransform.sizeDelta = new Vector2(-28,label.rectTransform.sizeDelta.y);
            }
        }
        Array("resourceValues", values); Array("resourceRates", rates); Array("resourceCards", cards);
        MoveButton(bar.Find("DoctrineButton"), new Vector2(-150,-12), new Vector2(124,48), "연구 [F]");
        MoveButton(bar.Find("PauseButton"), new Vector2(-16,-12), new Vector2(124,48), "일시정지 [P]");
        Bind("speedButton", Button(bar, "SpeedButton", "진행 ×1", -150,-73,124,48, Vector2.one));
        Bind("helpButton", Button(bar, "HelpButton", "도움말 [H]", -16,-73,124,48, Vector2.one));
        Text ownership = (bar.Find("Ownership") ?? hud.Find("Ownership")).GetComponent<Text>();
        ownership.transform.SetParent(hud, false);
        Place(ownership.rectTransform, new Vector2(0,1), new Vector2(0,1), new Vector2(22,-146), new Vector2(530,30));
        ownership.fontSize = 20;
        RectTransform research = Rect(hud, "ResearchStatus", new Vector2(.5f,1), new Vector2(.5f,1), new Vector2(0,-144), new Vector2(700,36));
        Text researchText = TextComponent(research, "연구 대기", 22, White, TextAnchor.MiddleCenter);
        Bind("researchStatus", researchText);
        Bind("researchProgress", Progress(hud, "ResearchProgress", new Vector2(.5f,1), new Vector2(0,-183), new Vector2(700,5), Cyan));
        RectTransform quick = Rect(hud,"QuickProduction",new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(0,88),new Vector2(530,122));
        Paint(quick,Panel);
        Bind("productionStatus",Label(quick,"Status","빠른 병력 보충",18,-12,494,30,22,White));
        Bind("quickProduceButton",Button(quick,"Produce","정찰체 +1 · 생체 40 [1]",18,-50,494,50));
        Bind("productionProgress",Progress(quick,"Progress",new Vector2(.5f,0),new Vector2(0,9),new Vector2(494,5),new Color(.5f,1,.65f)));
        RectTransform toast = Rect(hud,"ActionFeedback",new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(0,226),new Vector2(790,60));
        Text feedback = TextComponent(toast,"",23,White,TextAnchor.MiddleCenter);
        Outline outline = Ensure<Outline>(toast.gameObject);
        outline.effectColor = Color.black; outline.effectDistance = new Vector2(1,-1);
        Bind("feedbackText",feedback);
        Text status = hud.Find("StatusBar/Status").GetComponent<Text>();
        status.fontSize = 21; status.rectTransform.sizeDelta = new Vector2(1830,32);
        RectTransform chronicle = hud.Find("Chronicle") as RectTransform;
        Place(chronicle,Vector2.zero,Vector2.zero,new Vector2(22,88),new Vector2(430,210));
        Label(chronicle,"Heading","최근 사건",18,-14,394,30,23,Cyan);
        Label(chronicle,"EventLog","",18,-53,394,144,19,White);
    }

    static void BuildTutorial(Transform hud)
    {
        RectTransform tutorial = Rect(hud,"Tutorial",new Vector2(0,1),new Vector2(0,1),new Vector2(22,-198),new Vector2(470,330));
        Paint(tutorial,Panel);
        Paint(Rect(tutorial,"Accent",new Vector2(0,1),new Vector2(0,1),Vector2.zero,new Vector2(470,4)),Cyan,false);
        Bind("tutorialRoot",tutorial.gameObject);
        Bind("tutorialTitle",Label(tutorial,"Title","01  병력 생산",22,-22,426,38,28,Cyan));
        Bind("tutorialBody",Label(tutorial,"Body","",22,-76,426,164,22,White));
        Bind("tutorialAction",Button(tutorial,"Action","다음 행동",22,-258,300,52));
        Bind("tutorialSkip",Button(tutorial,"Skip","건너뛰기",332,-258,116,52));
        tutorial.gameObject.SetActive(false);
    }

    static void ReflowDetails(Transform hud)
    {
        RectTransform details = hud.Find("PlanetDetails") as RectTransform;
        Place(details,Vector2.one,Vector2.one,new Vector2(-22,-198),new Vector2(450,640));
        Label(details,"PlanetName","",22,-20,278,42,30,Cyan);
        Label(details,"PlanetMeta","",22,-76,280,36,22,White);
        Label(details,"PlanetStats","",22,-128,406,66,23,White);
        Bind("battleProgress",Progress(details,"BattleProgress",new Vector2(.5f,1),new Vector2(0,-199),new Vector2(406,6),Cyan));
        Label(details,"PlanetTrait","",22,-210,406,80,20,White);
        Label(details,"Production","",22,-302,406,98,21,White);
        Place(details.Find("InteriorPreview") as RectTransform,new Vector2(0,1),new Vector2(0,1),new Vector2(322,-18),new Vector2(106,82));
        MoveButton(details.Find("SourceButton"),new Vector2(22,-418),new Vector2(196,52),"출발지 지정",false);
        MoveButton(details.Find("ZoomButton"),new Vector2(232,-418),new Vector2(196,52),"행성 집중",false);
        string[] buttons = { "SurpriseButton", "AssaultButton", "OrbitalButton" };
        string[] captions = { "역병", "강습 [2]", "궤도 타격" };
        for (int i=0;i<3;i++) MoveButton(details.Find(buttons[i]),new Vector2(22+i*138,-418),new Vector2(130,52),captions[i],false);
        Label(details,"Hint","",22,-486,406,90,19,White);
        MoveButton(details.Find("OperationsButton"),new Vector2(22,-582),new Vector2(406,48),"행성 운영 · 생산 / 강화",false);
        RectTransform defense = hud.Find("DefenseAlert") as RectTransform;
        Place(defense,new Vector2(.5f,1),new Vector2(.5f,1),new Vector2(0,-198),new Vector2(650,88));
        Label(defense,"DefenseText","",18,-20,430,48,23,new Color(1,.65f,.3f));
        MoveButton(defense.Find("DefendButton"),new Vector2(466,-18),new Vector2(166,52),"방어 · 광물 15",false);
        RectTransform focus = hud.Find("PlanetFocus") as RectTransform;
        focus.anchoredPosition = new Vector2(0,-170);
    }

    static void ReflowMenus(Transform canvas)
    {
        foreach (string modalName in new[] { "OperationsModal", "DoctrineModal", "AssignmentModal", "PauseModal", "EndingModal", "OptionsModal" })
        {
            Transform modal = canvas.Find(modalName);
            modal.GetComponent<Image>().color = new Color(0,0,0,.82f);
            modal.Find("Card").GetComponent<Image>().color = new Color(.018f,.04f,.065f,1);
        }
        Transform options = canvas.Find("OptionsModal/Card");
        ((RectTransform)options).sizeDelta = new Vector2(840,760);
        Label(options,"Heading","설정 · 접근성",36,-26,768,54,34,Cyan);
        string[] labels = { "MusicLabel", "SfxLabel", "ScaleLabel", "CameraLabel" };
        string[] captions = { "배경음", "효과음", "UI 크기", "카메라 속도" };
        string[] sliders = { "MusicSlider", "SfxSlider", "ScaleSlider", "CameraSlider" };
        for (int i=0;i<4;i++)
        {
            Label(options,labels[i],captions[i],40,-106-i*66,200,34,24,White);
            Place(options.Find(sliders[i]) as RectTransform,new Vector2(0,1),new Vector2(0,1),new Vector2(264,-116-i*66),new Vector2(526,24));
        }
        Bind("reducedMotionButton",Button(options,"ReducedMotion","움직임 줄이기",40,-394,760,52));
        Bind("reducedEffectsButton",Button(options,"ReducedEffects","강한 효과 줄이기",40,-458,760,52));
        Bind("contrastButton",Button(options,"HighContrast","높은 대비",40,-522,760,52));
        Label(options,"KeyboardHelp","Tab 행성 선택 · 1 병력 생산 · 2 강습 · F 연구 · P 일시정지",40,-594,760,48,20,White);
        MoveButton(options.Find("SaveButton"),new Vector2(240,-670),new Vector2(360,56),"저장하고 닫기",false);
        Transform operations = canvas.Find("OperationsModal/Card");
        Label(operations,"Summary","",30,-74,1100,70,18,White);
        MoveButton(operations.Find("AntennaButton"),new Vector2(30,-150),new Vector2(250,46),"안테나 설치 · 광물 80",false);
        for(int i=0;i<3;i++) ((RectTransform)operations.Find("Trait"+i)).anchoredPosition=new Vector2(300+i*280,-150);
        foreach(Text text in operations.GetComponentsInChildren<Text>(true)) if(text.name!="Summary")text.fontSize=Mathf.Max(19,text.fontSize);
        Transform research = canvas.Find("DoctrineModal/Card");
        ((RectTransform)research).sizeDelta = new Vector2(1120,650);
        string[] cards={"Viral","Foundry","Orbital"};
        for(int i=0;i<3;i++)
        {
            Transform card=research.Find(cards[i]);
            Place(card as RectTransform,new Vector2(0,1),new Vector2(0,1),new Vector2(30+i*364,-150),new Vector2(332,380));
            Label(card,"Title","",20,-20,292,42,27,Cyan);
            Label(card,"Description","",20,-80,292,138,22,White);
            Label(card,"Cost","",20,-225,292,76,21,new Color(1,.82f,.52f));
            MoveButton(card.Find("ResearchButton"),new Vector2(20,-316),new Vector2(292,48),"연구 시작",false);
        }
        MoveButton(research.Find("CloseButton"),new Vector2(400,-574),new Vector2(320,50),"은하로 돌아가기",false);
        Transform title=canvas.Find("TitleScreen");
        title.Find("Prototype").gameObject.SetActive(false);
        Label(title,"Description","병력을 생산하고, 행성을 정복하고, 은하를 연결하세요.\n처음 시작하면 행동별 안내가 함께합니다.",178,-318,740,100,26,White);
        MoveButton(title.Find("StartButton"),new Vector2(178,-580),new Vector2(380,66),"새 작전 시작",false);
    }

    static void ConfigureUrp(Transform root)
    {
        Camera camera=root.GetComponentInChildren<Camera>(true);
        camera.allowHDR=true;
        UniversalAdditionalCameraData data=camera.GetUniversalAdditionalCameraData();
        data.renderPostProcessing=true;
        data.antialiasing=AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        data.antialiasingQuality=AntialiasingQuality.High;
        const string path="Assets/Settings/OblationGameplayProfile.asset";
        VolumeProfile profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
        if(profile==null) { profile=ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(profile,path); }
        Bloom bloom=VolumeComponent<Bloom>(profile); bloom.intensity.Override(.65f); bloom.threshold.Override(1.1f); bloom.scatter.Override(.68f);
        bloom.highQualityFiltering.Override(true);
        Tonemapping tone=VolumeComponent<Tonemapping>(profile); tone.mode.Override(TonemappingMode.ACES);
        ColorAdjustments color=VolumeComponent<ColorAdjustments>(profile); color.postExposure.Override(.1f); color.contrast.Override(12); color.saturation.Override(8);
        Vignette vignette=VolumeComponent<Vignette>(profile); vignette.intensity.Override(.16f); vignette.smoothness.Override(.5f);
        Volume volume=root.GetComponentInChildren<Volume>(true);
        volume.enabled=true; volume.isGlobal=true; volume.weight=1; volume.priority=10; volume.sharedProfile=profile;
        Bind("gameplayVolume",volume);
        Light key=null;
        foreach(Light candidate in root.GetComponentsInChildren<Light>(true)) if(candidate.type==LightType.Directional) { key=candidate; break; }
        if(key!=null) { key.enabled=true; key.color=new Color(1,.91f,.78f); key.intensity=1.45f; key.transform.rotation=Quaternion.Euler(38,-28,0); key.shadows=LightShadows.Soft; }
        RenderSettings.ambientMode=AmbientMode.Trilight;
        RenderSettings.ambientSkyColor=new Color(.12f,.19f,.3f);
        RenderSettings.ambientEquatorColor=new Color(.08f,.12f,.21f);
        RenderSettings.ambientGroundColor=new Color(.025f,.04f,.09f);
        Shader.SetGlobalFloat("_OblationMotion",1);
        const string materialPath="Assets/Materials/Oblation/ImpactParticle.mat";
        Material material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if(material==null) { material=new Material(Shader.Find("Oblation/Impact Particle")); AssetDatabase.CreateAsset(material,materialPath); }
        Bind("impactMaterial",material);
        EditorUtility.SetDirty(profile); EditorUtility.SetDirty(data); EditorUtility.SetDirty(volume);
    }

    static T VolumeComponent<T>(VolumeProfile profile) where T:VolumeComponent
    {
        if(!profile.TryGet(out T component)) { component=profile.Add<T>(true); AssetDatabase.AddObjectToAsset(component,profile); }
        component.active=true; EditorUtility.SetDirty(component); return component;
    }
    static RectTransform Rect(Transform parent,string name,Vector2 anchor,Vector2 pivot,Vector2 pos,Vector2 size)
    {
        RectTransform rect=parent.Find(name) as RectTransform;
        if(rect==null) { var go=new GameObject(name,typeof(RectTransform)); rect=go.GetComponent<RectTransform>(); rect.SetParent(parent,false); }
        Place(rect,anchor,pivot,pos,size); return rect;
    }
    static void Place(RectTransform rect,Vector2 anchor,Vector2 pivot,Vector2 pos,Vector2 size)
    { rect.anchorMin=rect.anchorMax=anchor; rect.pivot=pivot; rect.anchoredPosition=pos; rect.sizeDelta=size; rect.localScale=Vector3.one; }
    static Image Paint(RectTransform rect,Color color,bool block=true)
    { Image image=Ensure<Image>(rect.gameObject); image.color=color; image.raycastTarget=block; return image; }
    static Text Label(Transform parent,string name,string value,float x,float y,float width,float height,int size,Color color)
    { return TextComponent(Rect(parent,name,new Vector2(0,1),new Vector2(0,1),new Vector2(x,y),new Vector2(width,height)),value,size,color,TextAnchor.UpperLeft); }
    static Text TextComponent(RectTransform rect,string value,int size,Color color,TextAnchor alignment)
    {
        Text text=Ensure<Text>(rect.gameObject); text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text=value; text.fontSize=size; text.color=color; text.alignment=alignment; text.raycastTarget=false;
        text.horizontalOverflow=HorizontalWrapMode.Wrap; text.verticalOverflow=VerticalWrapMode.Truncate; return text;
    }
    static Button Button(Transform parent,string name,string caption,float x,float y,float width,float height,Vector2? anchor=null)
    {
        Vector2 at=anchor??new Vector2(0,1);
        RectTransform rect=Rect(parent,name,at,at,new Vector2(x,y),new Vector2(width,height));
        Image image=Paint(rect,new Color(.055f,.3f,.42f,1));
        Button button=Ensure<Button>(rect.gameObject); button.targetGraphic=image;
        RectTransform label=Rect(rect,"Label",Vector2.zero,Vector2.zero,Vector2.zero,Vector2.zero);
        label.anchorMin=Vector2.zero;label.anchorMax=Vector2.one;label.offsetMin=new Vector2(8,4);label.offsetMax=new Vector2(-8,-4);
        TextComponent(label,caption,21,White,TextAnchor.MiddleCenter);return button;
    }
    static void MoveButton(Transform transform,Vector2 position,Vector2 size,string caption,bool right=true)
    {
        Vector2 anchor=right?Vector2.one:new Vector2(0,1);
        Place((RectTransform)transform,anchor,anchor,position,size);
        Text text=transform.GetComponentInChildren<Text>();text.text=caption;text.fontSize=21;
    }
    static Image Progress(Transform parent,string name,Vector2 anchor,Vector2 position,Vector2 size,Color color)
    {
        RectTransform track=Rect(parent,name,anchor,new Vector2(.5f,anchor.y),position,size);Paint(track,new Color(.11f,.17f,.22f),false);
        RectTransform fill=Rect(track,"Fill",Vector2.zero,Vector2.zero,Vector2.zero,Vector2.zero);
        fill.anchorMax=Vector2.one;fill.offsetMin=fill.offsetMax=Vector2.zero;return Paint(fill,color,false);
    }
    static void Bind(string name,UnityEngine.Object value)=>game.FindProperty(name).objectReferenceValue=value;
    static T Ensure<T>(GameObject go) where T:Component { T component=go.GetComponent<T>();if(component==null)component=go.AddComponent<T>();return component; }
    static void Array(string name,UnityEngine.Object[] values)
    { SerializedProperty field=game.FindProperty(name);field.arraySize=values.Length;for(int i=0;i<values.Length;i++)field.GetArrayElementAtIndex(i).objectReferenceValue=values[i]; }
}
