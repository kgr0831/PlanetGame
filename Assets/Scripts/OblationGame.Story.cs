using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public sealed partial class OblationGame
{
    enum StoryChapter { Prologue, Signal, Rival }
    [Header("Inspector - story transmission")]
    [SerializeField] Canvas storyCanvas;
    [SerializeField] Text storyTitle;
    [SerializeField] Text storyBody;
    [SerializeField] Text storyChapterLabel;
    [SerializeField] Image storyProgress;
    [SerializeField] Button storyNextButton;
    [SerializeField] Button storySkipButton;
    bool storyActive;
    bool storyConsumedInput;
    StoryChapter storyChapter;
    int storyShot;
    float storyAge;
    int storySeen;
    int signalPlanet = 2;
    Vector3 storyLook;
    readonly Queue<StoryChapter> storyQueue = new Queue<StoryChapter>();
    static readonly string[][] StoryTitles = {
        new[]{"긴 침묵","마지막 신호","당신의 응답"},
        new[]{"침묵을 가르는 다리","기억의 파편","더 먼 곳으로"},
        new[]{"또 다른 응답자","왕관의 명령","선택의 대가"}
    };
    static readonly string[][] StoryLines = {
        new[]{"별과 별 사이의 목소리가 끊어진 뒤,\n우리는 작은 행성 하나에 남겨졌다.","VESPER의 심연에서 오래된 신호가 깨어났다.\n그 신호는 은하의 끝을 가리키고 있었다.","흩어진 세계를 잇고, 신호의 근원을 찾아라.\n첫 함대가 당신의 명령을 기다린다."},
        new[]{"첫 안테나가 먼 세계에 닿았다.\n고립된 행성이 하나의 목소리로 이어진다.","신호 속에는 우리가 모르는 기억이 있었다.\n누군가 이미 이 길을 지나갔다.","연결된 세계는 자원과 함대를 나눈다.\n다음 신호는 은하 외곽에서 온다."},
        new[]{"우리가 신호를 보낸 순간,\n다른 누군가도 눈을 떴다.","NEMESIS의 함대가 움직인다.\n그들에게 은하를 잇는 목소리는 하나뿐이다.","보급로를 지켜라. 고립된 세계는 침묵한다.\n어떤 미래를 남길지는 당신에게 달려 있다."}
    };
    void BindStory()
    {
        storyNextButton.onClick.AddListener(AdvanceStory);
        storySkipButton.onClick.AddListener(FinishStory);
    }
    void ResetStory(bool begin)
    {
        storyActive=false;storySeen=0;storyQueue.Clear();signalPlanet=2;
        if(begin)QueueStory(StoryChapter.Prologue);
        if(storyCanvas!=null)storyCanvas.gameObject.SetActive(false);
    }
    void QueueStory(StoryChapter chapter)
    {
        int mask=1<<(int)chapter;if((storySeen&mask)!=0)return;
        storySeen|=mask;storyQueue.Enqueue(chapter);
    }
    void UpdateStory(float dt)
    {
        storyConsumedInput=false;
        if(state!=ScreenState.Playing)return;
        if(!storyActive&&storyQueue.Count>0&&!optionsOpen&&!factoryOpen&&!operationsOpen&&!paused&&pendingAssignment<0)
        {
            storyChapter=storyQueue.Dequeue();storyActive=true;storyShot=0;storyAge=0;storyLook=cameraLookFocus;
            mapDragging=false;storyCanvas.gameObject.SetActive(true);StoryShotCue();
        }
        if(!storyActive)return;
        storyConsumedInput=true;
        storyAge+=dt;
        storyTitle.text=StoryTitles[(int)storyChapter][storyShot];storyBody.text=StoryLines[(int)storyChapter][storyShot];
        storyChapterLabel.text=(storyChapter==StoryChapter.Prologue?"프롤로그 · 끊어진 은하":storyChapter==StoryChapter.Signal?"첫 연결 · 기억의 신호":"조우 · 왕관의 함대")+"   "+(storyShot+1)+" / 3";
        storyNextButton.interactable=storyAge>=.65f;
        SetButtonText(storyNextButton,storyShot==2?"계속하기 · Space":"다음 장면 · Space");
        SetProgress(storyProgress,(storyShot+Mathf.Clamp01(storyAge/6f))/3f);
#if ENABLE_INPUT_SYSTEM
        bool advance=Keyboard.current!=null&&Keyboard.current.spaceKey.wasPressedThisFrame;
#else
        bool advance=Input.GetKeyDown(KeyCode.Space);
#endif
        if(EscapePressed()){FinishStory();return;}
        if(advance||storyAge>=6f)AdvanceStory();
    }
    void AdvanceStory()
    {
        if(!storyActive||storyAge<.65f)return;
        if(++storyShot>=3){FinishStory();return;}
        storyAge=0;StoryShotCue();PlayClick();
    }
    void FinishStory()
    {
        storyActive=false;storyCanvas.gameObject.SetActive(false);
        cameraFocusVelocity=Vector3.zero;ApplyGraphicsPreferences();
    }
    void StoryShotCue()
    {
        if(visualDirector==null)return;
        int index=storyChapter==StoryChapter.Rival?1:storyChapter==StoryChapter.Signal?signalPlanet:0;
        visualDirector.Event(planets[index].position,storyChapter==StoryChapter.Rival?EnemyColor:PlayerColor,
            storyChapter==StoryChapter.Rival?OblationEffectKind.Orbital:OblationEffectKind.Antenna,5);
    }
    bool UpdateStoryCamera(float dt)
    {
        if(!storyActive)return false;
        int index=storyChapter==StoryChapter.Rival?1:storyChapter==StoryChapter.Signal?signalPlanet:0;
        Vector3 look=planets[index].position;
        float t=ReducedMotion?.5f:Mathf.SmoothStep(0,1,Mathf.Clamp01(storyAge/6));
        Vector3 offset;
        if(storyChapter==StoryChapter.Prologue&&storyShot==0){look=Vector3.zero;offset=Vector3.Lerp(new Vector3(30,105,-135),new Vector3(15,65,-85),t);}
        else if(storyShot==1)offset=Quaternion.Euler(0,Mathf.Lerp(-18,18,t),0)*new Vector3(3.8f,2.6f,-5.8f);
        else if(storyShot==2){look=(planets[0].position+look)*.5f;offset=Vector3.Lerp(new Vector3(0,12,-18),new Vector3(0,28,-38),t);}
        else offset=Vector3.Lerp(new Vector3(-8,7,-12),new Vector3(-3.5f,3.5f,-6),t);
        float blend=ReducedMotion?1:1-Mathf.Exp(-dt*3.5f);
        storyLook=Vector3.Lerp(storyLook,look,blend);
        gameCamera.transform.position=Vector3.Lerp(gameCamera.transform.position,look+offset,blend);
        gameCamera.transform.rotation=Quaternion.Slerp(gameCamera.transform.rotation,Quaternion.LookRotation(storyLook-gameCamera.transform.position),blend);
        gameCamera.fieldOfView=Mathf.Lerp(gameCamera.fieldOfView,storyShot==1?38:50,blend);
        cameraLookFocus=storyLook;
        return true;
    }
}
