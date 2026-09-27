using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public static class OblationCameraReview
{
    const BindingFlags Fields=BindingFlags.Instance|BindingFlags.NonPublic;
    static object Get(OblationGame game,string name)=>typeof(OblationGame).GetField(name,Fields).GetValue(game);
    static void Set(OblationGame game,string name,object value)=>typeof(OblationGame).GetField(name,Fields).SetValue(game,value);
    static void Call(OblationGame game,string name,params object[] args)=>typeof(OblationGame).GetMethod(name,Fields).Invoke(game,args);
    static void Check(bool pass,string message){if(!pass)throw new Exception(message);}
    static Vector2 WorldPoint()
    {
        var hits=new List<RaycastResult>();var pointer=new PointerEventData(EventSystem.current);
        for(int y=2;y<8;y++)for(int x=1;x<9;x++)
        {
            pointer.position=new Vector2(Screen.width*x/10f,Screen.height*y/10f);
            hits.Clear();EventSystem.current.RaycastAll(pointer,hits);
            if(hits.Count==0)return pointer.position;
        }
        throw new Exception("No unobstructed map position was available for the wheel test.");
    }
    static async Task Move(Mouse mouse,Vector2 position)
    {InputSystem.QueueDeltaStateEvent(mouse.position,position);await Task.Delay(200);}
    static async Task Wheel(Mouse mouse,float steps)
    {
        float value=steps;
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        if(InputSystem.settings.scrollDeltaBehavior==InputSettings.ScrollDeltaBehavior.KeepPlatformSpecificInputRange)value*=120f;
#endif
        InputSystem.QueueDeltaStateEvent(mouse.scroll,new Vector2(0,value));await Task.Delay(300);
    }
    public static async Task<string> Verify()
    {
        if(!EditorApplication.isPlaying)throw new Exception("Enter Play mode first.");
        var game=GameObject.Find("Root").GetComponentInChildren<OblationGame>();
        Mouse mouse=Mouse.current;if(mouse==null)throw new Exception("No mouse device.");
        Vector2 pointer=mouse.position.ReadValue();float sensitivity=(float)Get(game,"cameraSensitivity");
        int preference=PlayerPrefs.GetInt("Oblation.CinematicTutorialComplete",0);
        bool reduced=OblationGame.ReducedMotion;
        var motion=typeof(OblationGame).GetProperty("ReducedMotion",BindingFlags.Public|BindingFlags.Static);
        try
        {
            Set(game,"cameraSensitivity",1f);motion.SetValue(null,false);
            Call(game,"ResetCampaign",true);Call(game,"ResetStory",false);
            Call(game,"EnterBeat",Enum.Parse(typeof(OblationGame).GetNestedType("TutorialBeat",BindingFlags.NonPublic),"SelectHome"));await Task.Delay(1600);await Move(mouse,WorldPoint());
            await Wheel(mouse,1);Check((float)Get(game,"cinematicZoom")<.99f,"Tutorial wheel-up did not zoom in.");
            await Wheel(mouse,-1);Check(Mathf.Abs((float)Get(game,"cinematicZoom")-1)<.01f,"Tutorial wheel-down did not zoom out.");
            Call(game,"FinishTutorial");await Task.Delay(1200);await Move(mouse,WorldPoint());
            var camera=(Camera)Get(game,"gameCamera");float beforeDistance=camera.transform.position.magnitude;
            await Wheel(mouse,1);
            Check((float)Get(game,"mapDistance")<31,"One normalized wheel step has no meaningful map zoom.");
            Check(camera.transform.position.magnitude<beforeDistance-.5f,"Map camera did not visibly move closer.");
            await Wheel(mouse,-1);Check(Mathf.Abs((float)Get(game,"mapDistance")-OblationGalaxyLayout.StartingDistance)<.01f,"Wheel direction or inverse step is incorrect.");
            await Wheel(mouse,100);Check(Mathf.Abs((float)Get(game,"mapDistance")-OblationGalaxyLayout.MapNear)<.01f,"Map near limit failed.");
            await Wheel(mouse,-100);Check(Mathf.Abs((float)Get(game,"mapDistance")-OblationGalaxyLayout.MapFar)<.01f,"Map far limit failed.");
            Set(game,"mapDistance",OblationGalaxyLayout.StartingDistance);
            var button=(Button)Get(game,"resourceDetailsButton");var rect=(RectTransform)button.transform;
            await Move(mouse,RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center)));
            Check(EventSystem.current.IsPointerOverGameObject(),"UI pointer setup failed.");
            await Wheel(mouse,1);Check((float)Get(game,"mapDistance")==OblationGalaxyLayout.StartingDistance,"Wheel over UI moved the camera.");
            await Move(mouse,WorldPoint());
            foreach(string flag in new[]{"paused","optionsOpen","factoryOpen","operationsOpen"})
            {
                Set(game,flag,true);await Wheel(mouse,1);Check((float)Get(game,"mapDistance")==OblationGalaxyLayout.StartingDistance,flag+" did not block wheel zoom.");Set(game,flag,false);
                Call(game,"RefreshUi");await Move(mouse,WorldPoint());
            }
            Set(game,"pendingAssignment",2);await Wheel(mouse,1);Check((float)Get(game,"mapDistance")==OblationGalaxyLayout.StartingDistance,"Assignment modal did not block wheel zoom.");Set(game,"pendingAssignment",-1);
            Call(game,"SelectPlanet",0);Set(game,"zoomed",true);Call(game,"RefreshUi");await Task.Delay(900);await Move(mouse,WorldPoint());
            await Wheel(mouse,1);Check((float)Get(game,"focusDistance")<17,"Focused planet wheel zoom failed.");
            await Wheel(mouse,-1);Check(Mathf.Abs((float)Get(game,"focusDistance")-18)<.01f,"Focused planet zoom-out failed.");
            await Wheel(mouse,100);Check((float)Get(game,"focusDistance")==OblationGalaxyLayout.FocusNear,"Focus near limit failed.");
            await Wheel(mouse,-100);Check((float)Get(game,"focusDistance")==OblationGalaxyLayout.FocusFar,"Focus far limit failed.");
            Check((float)Get(game,"mapDistance")==24,"Focus zoom changed the saved map distance.");
            Set(game,"zoomed",false);motion.SetValue(null,true);await Move(mouse,WorldPoint());await Wheel(mouse,1);
            Check((float)Get(game,"mapDistance")<31,"Reduced motion disabled wheel zoom.");
            Call(game,"ResetCampaign",true);Check((float)Get(game,"mapDistance")==OblationGalaxyLayout.StartingDistance&&(float)Get(game,"focusDistance")==18,"New campaign did not reset camera zoom.");
            return "Queued mouse-wheel input passed: tutorial, map and focused planet zoom in/out; visible camera movement; near/far limits; UI, pause and modal blocking; reduced motion; new-campaign reset.";
        }
        finally
        {
            Set(game,"cameraSensitivity",sensitivity);motion.SetValue(null,reduced);
            foreach(string flag in new[]{"paused","optionsOpen","factoryOpen","operationsOpen"})Set(game,flag,false);
            Set(game,"pendingAssignment",-1);
            InputSystem.QueueDeltaStateEvent(mouse.scroll,Vector2.zero);InputSystem.QueueDeltaStateEvent(mouse.position,pointer);
            PlayerPrefs.SetInt("Oblation.CinematicTutorialComplete",preference);PlayerPrefs.Save();
        }
    }
}
