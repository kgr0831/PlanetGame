using UnityEngine;

public sealed class OblationUiAudio : MonoBehaviour
{
    public OblationGame game;
    public AudioSource source;
    AudioClip typing,hover,opening;
    float nextTyping,nextHover,nextOpening;
    void Awake()
    {
        typing=Chirp("통신 타이핑",1500,1050,.025f,.065f);
        hover=Chirp("선택 신호",780,1200,.045f,.09f);
        opening=Chirp("패널 접속",280,1250,.16f,.14f);
    }
    static AudioClip Chirp(string name,float start,float end,float seconds,float amplitude)
    {
        const int rate=22050;int count=Mathf.CeilToInt(seconds*rate);float[] samples=new float[count];float phase=0;
        for(int i=0;i<count;i++)
        {
            float t=i/(float)count;phase+=Mathf.Lerp(start,end,t)*2*Mathf.PI/rate;
            float envelope=Mathf.Min(1,t*14)*Mathf.Pow(1-t,2);
            samples[i]=(Mathf.Sin(phase)+.18f*Mathf.Sin(phase*2))*envelope*amplitude;
        }
        var clip=AudioClip.Create(name,count,1,rate,false);clip.SetData(samples,0);return clip;
    }
    void Play(AudioClip clip)
    {
        if(clip!=null&&source!=null&&game!=null&&game.UiSfxVolume>.001f)source.PlayOneShot(clip,game.UiSfxVolume);
    }
    public void Typing(){if(Time.unscaledTime<nextTyping)return;nextTyping=Time.unscaledTime+.075f;Play(typing);}
    public void Hover(){if(Time.unscaledTime<nextHover)return;nextHover=Time.unscaledTime+.1f;Play(hover);}
    public void Open(){if(Time.unscaledTime<nextOpening)return;nextOpening=Time.unscaledTime+.16f;Play(opening);}
    void OnDestroy(){if(typing!=null)Destroy(typing);if(hover!=null)Destroy(hover);if(opening!=null)Destroy(opening);}
}
