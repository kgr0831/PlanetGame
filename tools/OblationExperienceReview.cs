using System;
using System.Collections;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Uses the actual serialized buttons in Play mode; never saves the scene.
public static class OblationExperienceReview
{
    static OblationGame Game()
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play mode first.");
        return GameObject.Find("Root").transform.Find("Util/Runtime/GameManager").GetComponent<OblationGame>();
    }
    static object Get(object obj,string field)=>obj.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public).GetValue(obj);
    static void Call(OblationGame game,string method,params object[] args)=>typeof(OblationGame).GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(game,args);
    static void Click(OblationGame game,string field)=>((Button)Get(game,field)).onClick.Invoke();
    public static string Start()
    {
        var game=Game(); Click(game,"startButton"); Call(game,"BeginTutorial");
        if((float)Get(game,"combatUnits")<4)throw new Exception("Starting troops are insufficient for an immediate action.");
        return Snapshot();
    }
    public static string TutorialAction() { var game=Game(); Click(game,"tutorialAction"); return Snapshot(); }
    public static string ChooseProductionSpecialization() { var game=Game(); Click(game,"extractorButton"); return Snapshot(); }
    public static string OpenOperations()
    {
        var game=Game(); Click(game,"operationsOpenButton");
        if((bool)Get(game,"paused"))throw new Exception("Operations must not stop production.");
        return Snapshot();
    }
    public static string CloseOperations() { var game=Game(); Click(game,"operationsCloseButton");return Snapshot(); }
    public static string ToggleAccessibility()
    {
        var game=Game(); bool before=OblationGame.ReducedMotion;
        Click(game,"reducedMotionButton");
        if(OblationGame.ReducedMotion==before)throw new Exception("Reduced motion toggle failed.");
        Click(game,"reducedMotionButton");
        Click(game,"contrastButton");Click(game,"contrastButton");
        Click(game,"reducedEffectsButton");Click(game,"reducedEffectsButton");
        return "Accessibility toggles work and original preferences were restored.";
    }
    public static string ShowOptions() { var game=Game(); Click(game,"pauseOptionsButton");return Snapshot(); }
    public static string ScaleReview(float value) { var game=Game();((Slider)Get(game,"uiScaleSlider")).value=value;return Snapshot(); }
    public static string Capture()
    {
        string path=System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath,"../Temp/experience_actual.png"));
        ScreenCapture.CaptureScreenshot(path);
        return path;
    }
    public static string InspectUi()
    {
        var game=Game();var modal=(GameObject)Get(game,"operationsModal");
        var bar=((Text[])Get(game,"resourceValues"))[0].transform.parent.parent.GetComponent<Canvas>();
        return "overlay="+modal.GetComponent<Image>().color+" card="+modal.transform.Find("Card").GetComponent<Image>().color+
            " canvas="+bar.sortingOrder+" override="+bar.overrideSorting+" root="+bar.rootCanvas.sortingOrder;
    }
    public static async Task<string> CombatFrame()
    {
        Start();var game=Game();Call(game,"SelectPlanet",2);Click(game,"assaultButton");
        await Task.Delay(1600);
        return Capture();
    }
    public static string Snapshot()
    {
        var game=Game(); Call(game,"RefreshUi"); var planets=(IList)Get(game,"planets");
        object home=planets[0]; var labels=(Text[])Get(game,"resourceValues");
        if(labels.Length!=5)throw new Exception("Five resource cards are required.");
        foreach(Text label in labels) if(!label.gameObject.activeInHierarchy || label.fontSize<30)throw new Exception("Resource card is hidden or too small.");
        return "tutorial="+Get(game,"tutorialStep")+" troops="+Get(game,"combatUnits")+" queued="+Get(home,"queuedUnitId")+
            " remaining="+Get(home,"unitRemaining")+" operations="+Get(game,"operationsOpen")+" paused="+Get(game,"paused")+
            " pendingAssignment="+Get(game,"pendingAssignment")+" targetOwner="+Get(planets[2],"owner")+" research="+Get(game,"researchRemaining");
    }
}
