using System;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class OblationTerminalReview
{
    const BindingFlags Fields=BindingFlags.Instance|BindingFlags.NonPublic;
    const string Preference="Oblation.CinematicTutorialComplete";
    static object Get(object obj,string field)=>obj.GetType().GetField(field,Fields).GetValue(obj);
    static int Visible(Text text)
    {int count=0;foreach(Color32 color in text.canvasRenderer.GetMesh().colors32)if(color.a>0)count++;return count;}
    static bool SameUvs(Vector2[] first,Vector2[] second)
    {if(first.Length!=second.Length)return false;for(int i=0;i<first.Length;i++)if((first[i]-second[i]).sqrMagnitude>.0000001f)return false;return true;}
    public static string StartWithCompletedTutorial()
    {
        if(!EditorApplication.isPlaying)throw new InvalidOperationException("Enter Play mode first.");
        if(!SessionState.GetBool("Oblation.CinematicReview",false))
        {SessionState.SetInt("Oblation.CinematicReview.Preference",PlayerPrefs.GetInt(Preference,0));SessionState.SetBool("Oblation.CinematicReview",true);}
        PlayerPrefs.SetInt(Preference,1);
        var game=GameObject.Find("Root").GetComponentInChildren<OblationGame>();
        typeof(OblationGame).GetMethod("ResetCampaign",Fields).Invoke(game,new object[]{true});
        typeof(OblationGame).GetMethod("ResetStory",Fields).Invoke(game,new object[]{false});
        typeof(OblationGame).GetMethod("EnterBeat",Fields).Invoke(game,new object[]{Enum.Parse(typeof(OblationGame).GetNestedType("TutorialBeat",BindingFlags.NonPublic),"SelectHome")});
        typeof(OblationGame).GetMethod("RefreshUi",Fields).Invoke(game,null);
        if(!(bool)Get(game,"tutorialActive")||!((GameObject)Get(game,"tutorialRoot")).activeInHierarchy||(float)Get(game,"fade")!=0)
            throw new Exception("New campaign did not immediately restore the tutorial after a prior completion.");
        return "New campaign shows tutorial immediately even with the old completion flag set; no black startup fade.";
    }
    public static async Task<string> Verify()
    {
        StartWithCompletedTutorial();var root=GameObject.Find("Root");var game=root.GetComponentInChildren<OblationGame>();
        await Task.Delay(1600);
        var label=(Text)Get(game,"tutorialBody");var terminal=label.GetComponent<OblationTerminalText>();
        foreach(Text text in root.GetComponentsInChildren<Text>(true))if(text.font==null||text.font.name!="Silver")throw new Exception("Runtime font override: "+text.name);
        string full=label.text;bool originalMotion=OblationGame.ReducedMotion;float originalVolume=game.UiSfxVolume;
        var motion=typeof(OblationGame).GetProperty("ReducedMotion",BindingFlags.Public|BindingFlags.Static);
        try
        {
            motion.SetValue(null,false);terminal.enabled=false;terminal.enabled=true;Canvas.ForceUpdateCanvases();
            int initial=Visible(label);Vector2[] alienUvs=label.canvasRenderer.GetMesh().uv;
            ScreenCapture.CaptureScreenshot("Temp/ui_decode_start.png");
            await Task.Delay(200);Canvas.ForceUpdateCanvases();
            if(terminal.DecodeProgress<=0||terminal.DecodeProgress>=.9f||Visible(label)!=initial)
                throw new Exception("Decoding should still be visible and in progress after 0.2 seconds.");
            await Task.Delay(550);Canvas.ForceUpdateCanvases();int complete=Visible(label);Vector2[] koreanUvs=label.canvasRenderer.GetMesh().uv;
            ScreenCapture.CaptureScreenshot("Temp/ui_decode_done.png");
            if(label.text!=full||initial<=0||initial!=complete||SameUvs(alienUvs,koreanUvs)||terminal.DecodeProgress<1)
                throw new Exception("Decoding must keep all glyphs visible and replace alien UVs with Korean within 0.75 seconds: "+initial+"/"+complete);
            if(label.fontSize<42||label.rectTransform.rect.height<label.preferredHeight)
                throw new Exception("Tutorial text is too small or clipped.");
            if(((CanvasGroup)Get(game,"cinematicCaption")).alpha!=1||((CanvasGroup)Get(game,"cinematicGroup")).alpha!=1)
                throw new Exception("Tutorial caption is being faded out.");
            motion.SetValue(null,true);terminal.enabled=false;terminal.enabled=true;label.SetVerticesDirty();Canvas.ForceUpdateCanvases();
            if(!SameUvs(label.canvasRenderer.GetMesh().uv,koreanUvs))throw new Exception("Reduced motion did not show Korean immediately.");
            var sound=game.GetComponent<OblationUiAudio>();
            await Task.Delay(250);typeof(OblationGame).GetField("sfxVolume",Fields).SetValue(game,.5f);sound.Open();await Task.Delay(25);
            if(!sound.source.isPlaying)throw new Exception("Panel sound did not play.");
            await Task.Delay(250);typeof(OblationGame).GetField("sfxVolume",Fields).SetValue(game,0f);sound.Open();await Task.Delay(25);
            if(sound.source.isPlaying)throw new Exception("UI sound ignored SFX mute.");
            return "Tutorial restores after completion; text stays visible from frame one; decoding is still in progress at 0.2 s and complete at 0.75 s without changing source/layout; larger captions fit; reduced motion and SFX mute pass.";
        }
        finally{motion.SetValue(null,originalMotion);typeof(OblationGame).GetField("sfxVolume",Fields).SetValue(game,originalVolume);}
    }
}
