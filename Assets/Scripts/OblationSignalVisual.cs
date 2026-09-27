using UnityEngine;

// Construction-time radio pulses. Has no weapon projectile, impact flash or fleet trail.
public sealed class OblationSignalVisual : MonoBehaviour
{
    LineRenderer link;
    LineRenderer mast;
    LineRenderer[] waves;
    LineRenderer[] packets;
    Vector3 from,to;
    Color color;
    bool reduced;
    public void Initialize(Vector3 start,Vector3 target,Color tint,Material material,bool reduce)
    {
        from=start;to=target;color=tint;reduced=reduce;
        link=GetComponent<LineRenderer>();link.sharedMaterial=material;link.positionCount=2;
        link.startWidth=link.endWidth=.024f;link.startColor=link.endColor=new Color(tint.r,tint.g,tint.b,.2f);
        link.SetPosition(0,from+Vector3.up*1.1f);link.SetPosition(1,to+Vector3.up*1.1f);
        mast=Line("안테나 기둥",material,.07f,2);mast.SetPosition(0,to+Vector3.up*.7f);mast.SetPosition(1,to+Vector3.up*3.2f);
        waves=new LineRenderer[reduce?1:3];for(int i=0;i<waves.Length;i++)waves[i]=Line("전파",material,.035f,49);
        packets=new LineRenderer[reduce?1:4];for(int i=0;i<packets.Length;i++)packets[i]=Line("신호 묶음",material,.09f,2);
    }
    LineRenderer Line(string name,Material material,float width,int points)
    {
        var go=new GameObject(name);go.transform.SetParent(transform,false);var line=go.AddComponent<LineRenderer>();
        line.sharedMaterial=material;line.useWorldSpace=true;line.positionCount=points;line.startWidth=line.endWidth=width;
        line.startColor=line.endColor=color*2;line.numCapVertices=3;return line;
    }
    public void SetProgress(float progress)
    {
        float time=OblationGame.ReducedMotion?0:Time.unscaledTime*.7f;
        for(int j=0;j<waves.Length;j++)
        {
            float phase=Mathf.Repeat(time+j/(float)waves.Length,1),radius=.4f+phase*2.7f;
            waves[j].startColor=waves[j].endColor=new Color(color.r*2,color.g*2,color.b*2,(1-phase)*(reduced?.3f:.85f));
            for(int i=0;i<49;i++){float a=i*Mathf.PI*2/48;waves[j].SetPosition(i,to+Vector3.up*(1.2f+progress)+new Vector3(Mathf.Cos(a),0,Mathf.Sin(a))*radius);}
        }
        for(int j=0;j<packets.Length;j++)
        {
            float t=Mathf.Repeat(time*.65f+j/(float)packets.Length,1);
            packets[j].SetPosition(0,Vector3.Lerp(from,to,t)+Vector3.up*1.1f);
            packets[j].SetPosition(1,Vector3.Lerp(from,to,Mathf.Min(1,t+.035f))+Vector3.up*1.1f);
        }
    }
}
