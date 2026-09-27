using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

public static class OblationExpansionReview
{
    const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static object Get(object o,string name)=>o.GetType().GetField(name,Flags).GetValue(o);
    static void Set(object o,string name,object value)=>o.GetType().GetField(name,Flags).SetValue(o,value);
    static void Call(OblationGame game,string name,params object[] args)=>typeof(OblationGame).GetMethod(name,Flags).Invoke(game,args);
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    static OblationGame Game(){if(!EditorApplication.isPlaying)throw new Exception("Enter Play mode.");return GameObject.Find("Root").GetComponentInChildren<OblationGame>();}
    static async Task MouseAt(Vector2 p,ushort buttons=0)
    {InputSystem.QueueStateEvent(Mouse.current,new MouseState{position=p,buttons=buttons});await Task.Delay(200);}
    static Vector2 WorldPoint()
    {
        var pointer=new PointerEventData(EventSystem.current);var hits=new List<RaycastResult>();
        for(int y=3;y<8;y++)for(int x=2;x<8;x++)
        {pointer.position=new Vector2(Screen.width*x/10f,Screen.height*y/10f);hits.Clear();EventSystem.current.RaycastAll(pointer,hits);if(hits.Count==0)return pointer.position;}
        throw new Exception("No unobstructed world point.");
    }
    public static async Task<string> VerifyNavigation()
    {
        var game=Game();Mouse originalMouse=Mouse.current;bool wasEnabled=originalMouse.enabled;
        var inputBehavior=InputSystem.settings.editorInputBehaviorInPlayMode;
        var backgroundBehavior=InputSystem.settings.backgroundBehavior;
        InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        if(wasEnabled)InputSystem.DisableDevice(originalMouse);
        Mouse reviewMouse=InputSystem.AddDevice<Mouse>("ExpansionReviewMouse");
        try
        {
            Call(game,"ResetCampaign",true);Call(game,"ResetStory",false);Call(game,"FinishTutorial");
            Call(game,"ShowGalaxyOverview");await Task.Delay(1500);
            var worlds=(IList)Get(game,"planets");Check(worlds.Count==36,"Expanded worlds missing.");
            var camera=(Camera)Get(game,"gameCamera");ScreenCapture.CaptureScreenshot("Temp/expanded_galaxy.png");
            int target=28;Vector3 world=(Vector3)Get(worlds[target],"position");Vector2 screen=(Vector2)camera.WorldToScreenPoint(world)+new Vector2(10,0);
            await MouseAt(screen);Check(!EventSystem.current.IsPointerOverGameObject(),"Planet click is under a panel.");
            await MouseAt(screen,1);await MouseAt(screen);await Task.Delay(1100);
            Check((int)Get(game,"selectedPlanet")==target,"Planet click: selected="+Get(game,"selectedPlanet")+" hovered="+Get(game,"hoveredPlanet")+" camera="+camera.WorldToScreenPoint(world)+" pointer="+Mouse.current.position.ReadValue()+" ui="+EventSystem.current.IsPointerOverGameObject());
            Check(Vector3.Distance((Vector3)Get(game,"cameraFocus"),world)<.01f,"Click did not target the selected planet.");
            Check(!(bool)Get(game,"zoomed"),"Single-click focus hid the neighboring worlds.");
            Check(Vector3.Distance((Vector3)Get(game,"cameraLookFocus"),world)<2,"Camera did not move to the clicked planet.");
            Check((float)Get(game,"mapDistance")<=24,"Far overview did not approach the selected planet.");
            ScreenCapture.CaptureScreenshot("Temp/planet_click_focus.png");
            Vector2 start=WorldPoint();Vector3 before=(Vector3)Get(game,"cameraFocus");
            await MouseAt(start);await MouseAt(start,2);await MouseAt(start+new Vector2(180,80),2);await MouseAt(start+new Vector2(180,80));
            Check(Vector3.Distance((Vector3)Get(game,"cameraFocus"),before)>1,"Holding right mouse and dragging did not move the map.");
            before=(Vector3)Get(game,"cameraFocus");await MouseAt(start);
            Check(Vector3.Distance((Vector3)Get(game,"cameraFocus"),before)<.01f,"Map moved after releasing right mouse.");
            var button=(Button)Get(game,"mapOverviewButton");var rect=(RectTransform)button.transform;
            Vector2 ui=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
            await MouseAt(ui);await MouseAt(ui,2);await MouseAt(ui+new Vector2(80,0),2);await MouseAt(ui);
            Check(Vector3.Distance((Vector3)Get(game,"cameraFocus"),before)<.01f,"Drag started on UI moved the map.");
            button.onClick.Invoke();await Task.Delay(1300);
            Check((Vector3)Get(game,"cameraFocus")==Vector3.zero&&(float)Get(game,"mapDistance")==OblationGalaxyLayout.OverviewDistance,"Overview button did not restore the galaxy view.");
            return "36-world overview, real outer-planet click/focus, visible camera approach, right-button drag/release, UI drag blocking and overview restore passed.";
        }
        finally
        {
            InputSystem.RemoveDevice(reviewMouse);
            if(wasEnabled)InputSystem.EnableDevice(originalMouse);
            originalMouse.MakeCurrent();
            InputSystem.settings.editorInputBehaviorInPlayMode=inputBehavior;
            InputSystem.settings.backgroundBehavior=backgroundBehavior;
        }
    }
    public static async Task<string> VerifyStory()
    {
        var game=Game();Call(game,"ResetCampaign",true);await Task.Delay(900);
        Check((bool)Get(game,"storyActive"),"Prologue did not start.");
        float elapsed=(float)Get(game,"campaignElapsed");var camera=(Camera)Get(game,"gameCamera");Vector3 before=camera.transform.position;
        await Task.Delay(700);Check((float)Get(game,"campaignElapsed")==elapsed,"Simulation advanced during the story.");
        if(!OblationGame.ReducedMotion)Check(Vector3.Distance(before,camera.transform.position)>.05f,"Story camera is static.");
        ((Button)Get(game,"storyNextButton")).onClick.Invoke();await Task.Delay(1300);
        Check(((Canvas)Get(game,"storyCanvas")).gameObject.activeInHierarchy,"Story overlay is hidden.");
        ScreenCapture.CaptureScreenshot("Temp/story_prologue.png");await Task.Delay(200);
        Check((int)Get(game,"storyShot")==1,"Story next button failed.");
        foreach(string label in new[]{"storyTitle","storyBody"})
        {var text=(Text)Get(game,label);Check(text.font.name=="Silver"&&text.preferredHeight<=text.rectTransform.rect.height,"Story text is clipped or wrong font.");}
        ((Button)Get(game,"storySkipButton")).onClick.Invoke();await Task.Delay(300);
        Check(!(bool)Get(game,"storyActive")&&(bool)Get(game,"tutorialActive"),"Skipping story lost the interactive tutorial.");
        var chapterType=typeof(OblationGame).GetNestedType("StoryChapter",BindingFlags.NonPublic);
        Call(game,"QueueStory",Enum.Parse(chapterType,"Rival"));Call(game,"QueueStory",Enum.Parse(chapterType,"Rival"));await Task.Delay(900);
        Check((bool)Get(game,"storyActive")&&Get(game,"storyChapter").ToString()=="Rival","Rival chapter did not open.");
        Check(((ICollection)Get(game,"storyQueue")).Count==0,"Rival story was queued twice.");
        ScreenCapture.CaptureScreenshot("Temp/story_rival.png");await Task.Delay(200);
        InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(Key.Escape));await Task.Delay(120);
        InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState());await Task.Delay(180);
        Check(!(bool)Get(game,"storyActive")&&(bool)Get(game,"tutorialActive"),"Escape routing: story="+Get(game,"storyActive")+" tutorial="+Get(game,"tutorialActive")+" consumed="+Get(game,"storyConsumedInput")+" key="+Keyboard.current.escapeKey.isPressed);
        return "Prologue and rival cinematic shots, moving camera, paused simulation, readable Silver subtitles, next/skip, one-time triggers and tutorial resumption passed.";
    }
    public static async Task<string> CaptureEffects()
    {
        var game=Game();Call(game,"ResetStory",false);Call(game,"FinishTutorial");
        Set(game,"selectedPlanet",0);Set(game,"mapDistance",10f);Set(game,"cameraFocus",Vector3.zero);await Task.Delay(1400);
        var director=(OblationVisualDirector)Get(game,"visualDirector");var parent=(Transform)Get(game,"effectsRoot");
        var line=(Material)Get(game,"lineMaterial");var kinds=(OblationEffectKind[])Enum.GetValues(typeof(OblationEffectKind));
        foreach(var kind in kinds)
        {
            var color=kind==OblationEffectKind.Plague?new Color(.25f,1,.3f):kind==OblationEffectKind.Orbital||kind==OblationEffectKind.Research?new Color(.7f,.35f,1):Color.cyan;
            director.Event(Vector3.zero,color,kind,7);await Task.Delay(600);
            bool found=false;foreach(var effect in parent.GetComponentsInChildren<OblationActionFx>())if(effect.Kind==kind)found=true;
            Check(found,"Missing action effect "+kind);ScreenCapture.CaptureScreenshot("Temp/action_"+kind+".png");await Task.Delay(2500);
        }
        Check(parent.GetComponentsInChildren<OblationActionFx>().Length==0,"Action effect did not clean up.");
        foreach(var kind in new[]{OblationEffectKind.Assault,OblationEffectKind.Plague,OblationEffectKind.Orbital})
        {
            var go=new GameObject("Review "+kind);go.transform.SetParent(parent);go.AddComponent<LineRenderer>();
            var effect=go.AddComponent<OblationAttackVisual>();effect.Initialize(new Vector3(-3,0,0),new Vector3(3,0,0),Color.cyan,line,kind,false);
            for(int i=0;i<20;i++){effect.SetProgress(i/20f);await Task.Delay(35);}
            ScreenCapture.CaptureScreenshot("Temp/attack_"+kind+".png");await Task.Delay(100);UnityEngine.Object.Destroy(go);
        }
        return "Seven action shader variants rendered and cleaned up; assault formation, plague helix and orbital beam captured separately.";
    }
}
