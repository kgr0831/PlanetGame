using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

public static class OblationCinematicReview
{
    const string Preference="Oblation.CinematicTutorialComplete";
    static OblationGame Game()
    {if(!EditorApplication.isPlaying)throw new InvalidOperationException("Enter Play mode first.");return GameObject.Find("Root").transform.Find("Util/Runtime/GameManager").GetComponent<OblationGame>();}
    static object Get(object obj,string name)=>obj.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public).GetValue(obj);
    static void Call(OblationGame game,string name,params object[] args)=>typeof(OblationGame).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(game,args);
    static void Click(OblationGame game,string field)
    {
        Button button=(Button)Get(game,field);
        if(!button.gameObject.activeInHierarchy||!button.interactable)throw new Exception(field+" is not available to the player.");
        button.onClick.Invoke();
    }
    public static string Start()
    {
        if(!SessionState.GetBool("Oblation.CinematicReview",false))
        {SessionState.SetInt("Oblation.CinematicReview.Preference",PlayerPrefs.GetInt(Preference,0));SessionState.SetBool("Oblation.CinematicReview",true);}
        var game=Game();Call(game,"ResetCampaign",true);Call(game,"BeginTutorial");Call(game,"RefreshUi");return Snapshot();
    }
    public static string Act()
    {
        var game=Game();Call(game,"RefreshUi");int beat=(int)Get(game,"tutorialStep");
        if((bool)Get(game,"storyActive")) { if((float)Get(game,"storyAge")>.7f)Click(game,"storyNextButton"); return Snapshot(); }
        if(beat==13)InputSystem.QueueStateEvent(Mouse.current,new MouseState { position=new Vector2(Screen.width*.4f,Screen.height*.6f),scroll=new Vector2(0,2) });
        else if(beat==14)
        {
            Vector2 current=Mouse.current.position.ReadValue();
            InputSystem.QueueStateEvent(Mouse.current,new MouseState { position=current+new Vector2(80,0),buttons=2 });
        }
        else if(beat==15) { InputSystem.QueueStateEvent(Mouse.current,new MouseState { position=new Vector2(Screen.width*.4f,Screen.height*.6f) }); if(!(bool)Get(game,"resourceDetailsOpen"))Click(game,"resourceDetailsButton"); }
        else if(beat==1||beat==4||beat==9)Click(game,"cinematicTargetButton");
        else if(beat==2)Click(game,"quickProduceButton");
        else if(beat==5)Click(game,(float)Get(game,"combatUnits")<4?"quickProduceButton":"assaultButton");
        else if(beat==8)Click(game,"extractorButton");
        else if(beat==11)Click(game,(bool)Get(game,"factoryOpen")?"orbitalTechButton":"doctrineOpenButton");
        return Snapshot();
    }
    public static string Snapshot()
    {
        var game=Game();Call(game,"RefreshUi");int visibleResources=0;
        foreach(Text text in (Text[])Get(game,"resourceValues"))if(text.gameObject.activeInHierarchy)visibleResources++;
        var planets=(IList)Get(game,"planets");
        return "beat="+Get(game,"tutorialStep")+" active="+Get(game,"tutorialActive")+" reticle="+((RectTransform)Get(game,"cinematicReticle")).gameObject.activeInHierarchy+
            " resources="+visibleResources+" selected="+Get(game,"selectedPlanet")+" owner="+Get(planets[2],"owner")+
            " assignment="+Get(game,"pendingAssignment")+" antenna="+Get(planets[2],"antennaActive")+" research="+Get(game,"researchRemaining");
    }
    public static async Task<string> Walkthrough()
    {
        Start();var game=Game();var seen=new HashSet<int>();float start=Time.realtimeSinceStartup;float lastAction=-10;
        int battleFrames=0;bool captured=false;bool sawRadio=false;
        while((bool)Get(game,"tutorialActive")&&Time.realtimeSinceStartup-start<120)
        {
            Call(game,"RefreshUi");int beat=(int)Get(game,"tutorialStep");seen.Add(beat);
            if(beat==10&&((Transform)Get(game,"effectsRoot")).GetComponentsInChildren<OblationSignalVisual>().Length>0)sawRadio=true;
            if(beat==6 && ++battleFrames==9)ScreenCapture.CaptureScreenshot("Temp/vfx_battle.png");
            if(beat==7 && !captured){captured=true;ScreenCapture.CaptureScreenshot("Temp/vfx_capture.png");}
            if(Time.realtimeSinceStartup-lastAction>.45f&&((bool)Get(game,"storyActive")||beat==1||beat==2||beat==4||beat==5||beat==8||beat==9||beat==11||beat>=13))
            {Act();lastAction=Time.realtimeSinceStartup;}
            await Task.Delay(250);
        }
        InputSystem.QueueStateEvent(Mouse.current,new MouseState { position=Mouse.current.position.ReadValue() });
        if((bool)Get(game,"tutorialActive"))throw new Exception("Cinematic tutorial did not finish: "+Snapshot());
        var planets=(IList)Get(game,"planets");
        if(Get(planets[2],"owner").ToString()!="Player"||!(bool)Get(planets[2],"antennaActive")||Get(game,"activeResearch")==null)
            throw new Exception("Tutorial did not perform the real conquest, antenna and research actions.");
        if(seen.Count<16)throw new Exception("A cinematic beat was skipped: "+string.Join(",",seen));
        if(!sawRadio||((int)Get(game,"storySeen")&3)!=3)throw new Exception("Antenna VFX or the first-connection story was not triggered by real gameplay.");
        VerifyCompact(game);
        return "All 16 cinematic beats completed through actual controls in "+(Time.realtimeSinceStartup-start).ToString("0.0")+" seconds. "+Snapshot();
    }
    public static async Task<string> VerifyEffects()
    {
        var game=Game();var parent=(Transform)Get(game,"effectsRoot");
        Vector3 target=new Vector3(8,0,8);
        OblationCombatBurst.Spawn(parent,target,Color.cyan,(Material)Get(game,"impactMaterial"),12,false);
        var bursts=parent.GetComponentsInChildren<OblationCombatBurst>();var burst=bursts[bursts.Length-1];
        var particles=burst.GetComponent<ParticleSystem>();await Task.Delay(100);
        var emitted=new ParticleSystem.Particle[128];int count=particles.GetParticles(emitted);
        if(count<12)throw new Exception("Expected a visible impact burst.");
        for(int i=0;i<count;i++)if(Vector3.Distance(emitted[i].position,target)>3)throw new Exception("Impact particles spawned away from their world-space target.");
        bool original=(bool)Get(game,"reducedEffects");
        try
        {
            typeof(OblationGame).GetField("reducedEffects",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(game,true);
            Call(game,"ApplyGraphicsPreferences");await Task.Delay(200);
            var volume=(UnityEngine.Rendering.Volume)Get(game,"gameplayVolume");
            volume.profile.TryGet(out UnityEngine.Rendering.Universal.Bloom bloom);
            volume.profile.TryGet(out UnityEngine.Rendering.Universal.ChromaticAberration chromatic);
            if(bloom.intensity.value>.25f||chromatic.intensity.value!=0)throw new Exception("Reduced-effects preference was overridden by the visual director.");
        }
        finally
        {
            typeof(OblationGame).GetField("reducedEffects",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(game,original);Call(game,"ApplyGraphicsPreferences");
        }
        await Task.Delay(1700);
        if(burst!=null)throw new Exception("Transient burst did not clean itself up.");
        return "Impact particles emit at the target, clean up after their lifetime, and reduced effects retain low bloom with no chromatic aberration.";
    }
    public static async Task<string> SkipAndRestore()
    {
        Start();await Task.Delay(700);var game=Game();if((bool)Get(game,"storyActive"))Call(game,"FinishStory");await Task.Delay(100);Click(game,"tutorialSkip");await Task.Delay(1200);Call(game,"RefreshUi");VerifyCompact(game);
        if((bool)Get(game,"tutorialActive")||((Canvas)Get(game,"cinematicCanvas")).gameObject.activeInHierarchy)throw new Exception("Skip left the cinematic overlay active.");
        return "Skip restores compact HUD and removes cinematic input targets.";
    }
    static void VerifyCompact(OblationGame game)
    {
        foreach(Text rate in (Text[])Get(game,"resourceRates"))if(rate.gameObject.activeInHierarchy)throw new Exception("Resource income is expanded by default.");
        if(((Text)Get(game,"eventLogText")).transform.parent.gameObject.activeInHierarchy)throw new Exception("Event history is expanded by default.");
        if(((GameObject)Get(game,"planetPanel")).activeInHierarchy)throw new Exception("Inspector should stay closed until selection.");
    }
    public static string Capture()
    {
        string path=System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath,"../Temp/cinematic_review.png"));
        ScreenCapture.CaptureScreenshot(path);return path;
    }
    public static string ProgressiveDisclosure()
    {
        var game=Game();Call(game,"RefreshUi");VerifyCompact(game);
        Click(game,"resourceDetailsButton");Call(game,"RefreshUi");
        foreach(Text rate in (Text[])Get(game,"resourceRates"))if(!rate.gameObject.activeInHierarchy)throw new Exception("Income did not expand.");
        Click(game,"resourceDetailsButton");Call(game,"SelectPlanet",0);Call(game,"RefreshUi");
        if(((Text)Get(game,"planetTraitText")).gameObject.activeInHierarchy)throw new Exception("Planet details should start collapsed.");
        Click(game,"planetDetailsButton");Call(game,"RefreshUi");
        if(!((Text)Get(game,"planetTraitText")).gameObject.activeInHierarchy)throw new Exception("Planet details did not expand.");
        Click(game,"planetDetailsButton");Click(game,"operationsOpenButton");Call(game,"RefreshUi");
        foreach(Button button in (Button[])Get(game,"traitButtons"))if(button.gameObject.activeInHierarchy)throw new Exception("Development actions leaked into production tab.");
        ((Button[])Get(game,"operationTabs"))[1].onClick.Invoke();Call(game,"RefreshUi");
        foreach(Button button in (Button[])Get(game,"unitButtons"))if(button.gameObject.activeInHierarchy)throw new Exception("Production actions leaked into development tab.");
        if(!((Button)Get(game,"antennaButton")).gameObject.activeInHierarchy)throw new Exception("Antenna action is hidden.");
        return "Income and planet details expand on demand; production and development tabs show only their own actions.";
    }
    public static string RestorePreferences()
    {
        PlayerPrefs.SetInt(Preference,SessionState.GetInt("Oblation.CinematicReview.Preference",0));PlayerPrefs.Save();
        SessionState.SetBool("Oblation.CinematicReview",false);return "Original tutorial preference restored.";
    }
}
