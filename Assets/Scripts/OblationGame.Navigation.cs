using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public sealed partial class OblationGame
{
    [SerializeField] Button mapOverviewButton;
    [SerializeField] Text navigationHint;
    [SerializeField] Text[] planetMapMarkers;
    bool mapDragging;
    Vector2 lastDragPointer;
    float tutorialZoomTravel;
    float tutorialPanTravel;
    bool CameraControlsAvailable => state==ScreenState.Playing&&!storyActive&&!paused&&!optionsOpen&&!factoryOpen&&!operationsOpen&&pendingAssignment<0;
    bool NavigationLesson => tutorialActive&&(Beat==TutorialBeat.CameraZoom||Beat==TutorialBeat.CameraPan);

    int PlanetAtPointer()
    {
        Vector2 pointer=PointerPosition();Ray ray=gameCamera.ScreenPointToRay(pointer);
        if(Physics.Raycast(ray,out RaycastHit hit,1200))
        {var marker=hit.collider.GetComponent<PlanetMarker>();if(marker!=null)return marker.index;}
        int nearest=-1;float distance=18*18;
        for(int i=0;i<planets.Count;i++)
        {
            if(planets[i].destroyed||zoomed&&i!=selectedPlanet)continue;
            Vector3 screen=gameCamera.WorldToScreenPoint(planets[i].position);
            if(screen.z<=0)continue;
            float sqr=((Vector2)screen-pointer).sqrMagnitude;
            if(sqr<distance){distance=sqr;nearest=i;}
        }
        return nearest;
    }
    void RefreshMapMarkers()
    {
        var hud=(RectTransform)hudUiScaler.transform;
        for(int i=0;i<planetMapMarkers.Length;i++)
        {
            Vector3 center=gameCamera.WorldToScreenPoint(planets[i].position);
            Vector3 edge=gameCamera.WorldToScreenPoint(planets[i].position+gameCamera.transform.right*planets[i].scale*.5f);
            bool visible=state==ScreenState.Playing&&!tutorialActive&&!storyActive&&!zoomed&&center.z>0&&
                center.x>0&&center.x<Screen.width&&center.y>0&&center.y<Screen.height&&Vector3.Distance(center,edge)<8;
            var marker=planetMapMarkers[i];marker.gameObject.SetActive(visible);if(!visible)continue;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(hud,center,null,out Vector2 local);
            marker.rectTransform.anchoredPosition=local;marker.color=OwnerColor(planets[i].owner);marker.text=planets[i].destroyed?"×":"◇";
        }
    }

    void FocusSelectedPlanet()
    {
        if(tutorialActive||storyActive||selectedPlanet<0)return;
        cameraFocus=planets[selectedPlanet].position;
        cameraFocusVelocity=Vector3.zero;
        if(!zoomed)mapDistance=Mathf.Clamp(mapDistance,8,24);
        mapDragging=false;
    }
    void ShowGalaxyOverview()
    {
        if(!CameraControlsAvailable||tutorialActive)return;
        zoomed=false;cameraFocus=Vector3.zero;cameraFocusVelocity=Vector3.zero;mapDistance=OblationGalaxyLayout.OverviewDistance;
        PlayClick();
    }
    void HandleMapNavigation(float dt)
    {
        if(!CameraControlsAvailable||tutorialActive&&!NavigationLesson){mapDragging=false;return;}
#if ENABLE_INPUT_SYSTEM
        bool held=Mouse.current!=null&&Mouse.current.rightButton.isPressed;
        bool pressed=Mouse.current!=null&&Mouse.current.rightButton.wasPressedThisFrame;
        if(Keyboard.current!=null&&Keyboard.current.mKey.wasPressedThisFrame)ShowGalaxyOverview();
#else
        bool held=Input.GetMouseButton(1),pressed=Input.GetMouseButtonDown(1);
        if(Input.GetKeyDown(KeyCode.M))ShowGalaxyOverview();
#endif
        Vector2 pointer=PointerPosition();
        if(pressed&&!PointerOverUi())
        {
            mapDragging=true;lastDragPointer=pointer;
            if(zoomed){zoomed=false;mapDistance=Mathf.Clamp(focusDistance*.66f,OblationGalaxyLayout.MapNear,OblationGalaxyLayout.MapFar);}
            cameraFocus=cameraLookFocus;cameraFocusVelocity=Vector3.zero;
        }
        if(!held)mapDragging=false;
        Vector3 movement=Vector3.zero;
        if(mapDragging)
        {
            var plane=new Plane(Vector3.up,Vector3.zero);
            Ray previous=gameCamera.ScreenPointToRay(lastDragPointer),current=gameCamera.ScreenPointToRay(pointer);
            if(plane.Raycast(previous,out float a)&&plane.Raycast(current,out float b))movement=(previous.GetPoint(a)-current.GetPoint(b))*cameraSensitivity;
            lastDragPointer=pointer;
        }
        if(!zoomed)
        {
            Vector2 keys=ReadMove();movement+=new Vector3(keys.x,0,keys.y)*dt*Mathf.Max(7,mapDistance*.45f)*cameraSensitivity;
            cameraFocus+=movement;
            cameraFocus.x=Mathf.Clamp(cameraFocus.x,-OblationGalaxyLayout.PanLimit,OblationGalaxyLayout.PanLimit);
            cameraFocus.z=Mathf.Clamp(cameraFocus.z,-OblationGalaxyLayout.PanLimit,OblationGalaxyLayout.PanLimit);
            if(NavigationLesson)tutorialPanTravel+=movement.magnitude;
        }
    }
}
