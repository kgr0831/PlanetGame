using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class OblationUiReadabilityBuilder
{
    public static string Build()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play mode first.");
        var scene=EditorSceneManager.GetActiveScene();if(scene.path!="Assets/Scenes/SampleScene.unity")throw new Exception("Open SampleScene.");
        Transform root=GameObject.Find("Root").transform;var game=root.Find("Util/Runtime/GameManager").GetComponent<OblationGame>();
        var refs=new SerializedObject(game);Font silver=(Font)refs.FindProperty("interfaceFont").objectReferenceValue;
        silver.RequestCharactersInTexture(OblationTerminalText.AlienSymbols,42,FontStyle.Normal);
        string supported="";foreach(char c in OblationTerminalText.AlienSymbols)if(silver.HasCharacter(c))supported+=c;
        foreach(Text text in root.GetComponentsInChildren<Text>(true))
        {
            int target=Mathf.Max(32,text.fontSize);
            Fit(text,target,Mathf.Min(28,Mathf.Max(22,Mathf.FloorToInt(text.rectTransform.rect.height/1.1f))));
            if(text.GetComponentInParent<Button>()!=null)
            {
                text.rectTransform.anchorMin=Vector2.zero;text.rectTransform.anchorMax=Vector2.one;
                text.rectTransform.offsetMin=new Vector2(8,0);text.rectTransform.offsetMax=new Vector2(-8,0);
                Fit(text,Mathf.Max(42,target),28);
            }
            EditorUtility.SetDirty(text);
        }
        foreach(var effect in root.GetComponentsInChildren<OblationTerminalText>(true)){effect.scrambleDuration=.6f;EditorUtility.SetDirty(effect);}
        foreach(var panel in root.GetComponentsInChildren<OblationHologram>(true))if(panel.revealGroup!=null){panel.revealGroup.alpha=1;EditorUtility.SetDirty(panel.revealGroup);}
        RectTransform caption=((GameObject)refs.FindProperty("tutorialRoot").objectReferenceValue).GetComponent<RectTransform>();
        caption.sizeDelta=new Vector2(1200,150);
        Text title=(Text)refs.FindProperty("tutorialTitle").objectReferenceValue;
        Text body=(Text)refs.FindProperty("tutorialBody").objectReferenceValue;
        Position(title,0,-32,1200,64);Position(body,0,-101,1200,50);Fixed(title,54);Fixed(body,42);
        Text chapter=(Text)refs.FindProperty("cinematicChapter").objectReferenceValue;Position(chapter,0,-4,1200,28);Fixed(chapter,24);
        ((CanvasGroup)refs.FindProperty("cinematicCaption").objectReferenceValue).alpha=1;
        ((Button)refs.FindProperty("tutorialSkip").objectReferenceValue).GetComponentInChildren<Text>().text="건너뛰기 · Esc";
        Transform bar=root.Find("HudUI/HudCanvas/HudScreen/TopBar");
        for(int i=0;i<5;i++)
        {
            Transform card=bar.Find("ResourceCard"+i);
            Text name=card.Find("Name").GetComponent<Text>();Position(name,12,-24,68,38);Fit(name,32,30);
            Text value=card.Find("Value").GetComponent<Text>();value.rectTransform.anchoredPosition=new Vector2(-10,-12);value.rectTransform.sizeDelta=new Vector2(94,56);Fit(value,50,34);
            Text rate=card.Find("Rate").GetComponent<Text>();Position(rate,12,-94,166,32);Fit(rate,28,26);
        }
        foreach(var button in bar.GetComponentsInChildren<Button>(true))
        {
            var rect=(RectTransform)button.transform;rect.sizeDelta=new Vector2(rect.sizeDelta.x,54);rect.anchoredPosition=new Vector2(rect.anchoredPosition.x,-18);
        }
        Transform quick=root.Find("HudUI/HudCanvas/HudScreen/QuickProduction");
        Position(quick.Find("Status").GetComponent<Text>(),18,-6,424,42);Fixed(quick.Find("Status").GetComponent<Text>(),34);
        var produce=(RectTransform)quick.Find("Produce");produce.anchoredPosition=new Vector2(18,-54);produce.sizeDelta=new Vector2(424,60);
        var progress=(RectTransform)quick.Find("Progress");progress.sizeDelta=new Vector2(424,3);
        Text feedback=(Text)refs.FindProperty("feedbackText").objectReferenceValue;feedback.rectTransform.sizeDelta=new Vector2(1000,56);Fit(feedback,40,34);
        Transform details=root.Find("HudUI/HudCanvas/HudScreen/PlanetDetails");
        Position(details.Find("PlanetName").GetComponent<Text>(),20,-10,270,50);Fit(details.Find("PlanetName").GetComponent<Text>(),42,36);
        Position(details.Find("PlanetMeta").GetComponent<Text>(),20,-58,380,38);Fit(details.Find("PlanetMeta").GetComponent<Text>(),32,28);
        Transform operations=root.Find("GameUI/GameCanvas/OperationsModal/Card");
        for(int i=0;i<8;i++)
        {
            var unit=(RectTransform)operations.Find("Unit"+i);unit.anchoredPosition=new Vector2(30+i%4*280,-254-i/4*104);unit.sizeDelta=new Vector2(260,92);
            Fit(unit.GetComponentInChildren<Text>(),40,32);
        }
        foreach(UnityEngine.Object item in new UnityEngine.Object[]{title,body,chapter,caption,game})EditorUtility.SetDirty(item);
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        return "Saved larger Silver text, full-height button labels, immediate panels, readable tutorial captions and alien glyph alphabet: "+supported;
    }
    static void Position(Text text,float x,float y,float width,float height)
    {text.rectTransform.anchoredPosition=new Vector2(x,y);text.rectTransform.sizeDelta=new Vector2(width,height);}
    static void Fit(Text text,int size,int minimum)
    {text.fontSize=size;text.resizeTextForBestFit=true;text.resizeTextMaxSize=size;text.resizeTextMinSize=minimum;text.verticalOverflow=VerticalWrapMode.Truncate;}
    static void Fixed(Text text,int size)
    {text.fontSize=size;text.resizeTextForBestFit=false;text.verticalOverflow=VerticalWrapMode.Overflow;}
}
