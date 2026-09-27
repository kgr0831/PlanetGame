using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class OblationSilverUiBuilder
{
    public static string Build()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play mode.");
        var scene=EditorSceneManager.GetActiveScene();if(scene.path!="Assets/Scenes/SampleScene.unity")throw new Exception("Open SampleScene.");
        Font silver=AssetDatabase.LoadAssetAtPath<Font>("Assets/Silver.ttf");if(silver==null)throw new Exception("Silver font asset is missing.");
        silver.RequestCharactersInTexture("행성생체광물신경연구설정접속완료OBLATION0123456789·×",32,FontStyle.Normal);
        foreach(char c in "행성생체광물신경연구설정접속완료")if(!silver.HasCharacter(c))throw new Exception("Missing Korean glyph in Silver: "+c);
        Transform root=GameObject.Find("Root").transform;
        var game=root.Find("Util/Runtime/GameManager").GetComponent<OblationGame>();var serialized=new SerializedObject(game);
        serialized.FindProperty("interfaceFont").objectReferenceValue=silver;
        var sound=Ensure<OblationUiAudio>(game.gameObject);sound.game=game;sound.source=(AudioSource)serialized.FindProperty("sfxSource").objectReferenceValue;
        int labels=0,terminals=0;
        foreach(Text text in root.GetComponentsInChildren<Text>(true))
        {
            if(text.font!=silver)
            {
                text.fontSize=Mathf.Max(20,Mathf.RoundToInt(text.fontSize*1.16f/2)*2);
                if(text.resizeTextForBestFit){text.resizeTextMaxSize=text.fontSize;text.resizeTextMinSize=Mathf.Max(18,text.fontSize-6);}
            }
            text.font=silver;text.fontStyle=FontStyle.Normal;text.lineSpacing=1;
            float height=text.rectTransform.rect.height;
            if(height>0&&height<text.fontSize*1.2f)
            {
                text.resizeTextForBestFit=true;text.resizeTextMaxSize=text.fontSize;
                text.resizeTextMinSize=Mathf.Max(14,Mathf.Min(text.fontSize,Mathf.FloorToInt(height*.85f)));
            }
            if(text.GetComponentInParent<Button>()!=null)
            {
                text.resizeTextForBestFit=true;text.resizeTextMinSize=Mathf.Min(28,text.fontSize);
                text.fontSize=Mathf.Max(36,text.fontSize);text.resizeTextMaxSize=text.fontSize;
            }
            if(text.name=="Title"||text.name=="Heading")
            {
                var terminal=Ensure<OblationTerminalText>(text.gameObject);terminal.audioFeedback=sound;terminal.scrambleDuration=.6f;terminals++;
            }
            EditorUtility.SetDirty(text);labels++;
        }
        foreach(string field in new[]{"tutorialTitle","tutorialBody","feedbackText"})
        {
            var text=(Text)serialized.FindProperty(field).objectReferenceValue;
            if(field=="tutorialTitle"){text.fontSize=44;text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,54);text.resizeTextForBestFit=false;}
            if(field=="tutorialBody"){text.fontSize=34;text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,40);text.resizeTextForBestFit=false;}
            var terminal=Ensure<OblationTerminalText>(text.gameObject);terminal.audioFeedback=sound;
            terminal.scrambleDuration=.6f;terminals++;EditorUtility.SetDirty(terminal);
        }
        foreach(var button in root.GetComponentsInChildren<OblationButtonFeedback>(true)){button.audioFeedback=sound;EditorUtility.SetDirty(button);}
        foreach(var panel in root.GetComponentsInChildren<OblationHologram>(true))
        {
            panel.audioFeedback=sound;panel.panelFrame=panel.GetComponent<Button>()==null;EditorUtility.SetDirty(panel);
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(game);EditorUtility.SetDirty(sound);
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        return "Applied Silver to "+labels+" labels; connected "+terminals+" terminal text effects, holographic panel animation and UI audio. Korean glyphs verified.";
    }
    static T Ensure<T>(GameObject go) where T:Component{var item=go.GetComponent<T>();return item!=null?item:go.AddComponent<T>();}
}
