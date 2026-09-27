using System.Text;
using UnityEngine;
using UnityEngine.UI;

// Keep the Korean source and layout intact; substitute only glyph UVs during decoding.
[RequireComponent(typeof(Text))]
public sealed class OblationTerminalText : BaseMeshEffect
{
    public const string AlienSymbols="\u25b3\u25bd\u25c7\u25cb\u25a1\u2299\u2295\u2234\u2235\u00d7";
    public OblationUiAudio audioFeedback;
    [Range(.15f,1f)] public float scrambleDuration=.6f;
    public bool sound=true;
    Text label;
    string message;
    string symbols;
    float age;
    int phase=-1;
    int glyphCount;
    int glyphSize;
    public float DecodeProgress=>OblationGame.ReducedMotion?1:Mathf.Clamp01(age/Mathf.Max(.08f,scrambleDuration));
    protected override void Awake(){base.Awake();label=GetComponent<Text>();}
    protected override void OnEnable()
    {
        base.OnEnable();age=0;phase=-1;message=null;
        WarmSymbols();
    }
    void WarmSymbols()
    {
        if(label==null)label=GetComponent<Text>();
        if(label.font==null)return;
        if(symbols==null)
        {
            var supported=new StringBuilder();
            foreach(char c in AlienSymbols)if(label.font.HasCharacter(c))supported.Append(c);
            symbols=supported.Length>=4?supported.ToString():"<>+*/=|";
        }
        glyphSize=label.resizeTextForBestFit?label.cachedTextGenerator.fontSizeUsedForBestFit:label.fontSize;
        if(glyphSize<=0)glyphSize=label.fontSize;
        label.font.RequestCharactersInTexture(symbols,glyphSize,label.fontStyle);
    }
    bool Synchronize()
    {
        if(label==null)label=GetComponent<Text>();
        if(label.text==message)return false;
        string next=label.text;
        bool onlyNumbers=message!=null&&next.Length==message.Length;
        if(onlyNumbers)for(int i=0;i<next.Length;i++)if(next[i]!=message[i]&&!(char.IsDigit(next[i])&&char.IsDigit(message[i]))){onlyNumbers=false;break;}
        message=next;if(!onlyNumbers){age=0;phase=-1;}
        return true;
    }
    void LateUpdate()
    {
        bool changed=Synchronize();age+=Time.unscaledDeltaTime;
        int nextPhase=DecodeProgress>=1?1000:Mathf.FloorToInt(age*18);
        if(nextPhase!=phase||changed)
        {
            if(DecodeProgress<1)
            {
                WarmSymbols();
                if(sound&&glyphCount>0&&audioFeedback!=null)audioFeedback.Typing();
            }
            graphic.SetVerticesDirty();phase=nextPhase;
        }
    }
    public override void ModifyMesh(VertexHelper vertices)
    {
        if(!IsActive())return;
        Synchronize();glyphCount=vertices.currentVertCount/4;
        float progress=DecodeProgress;
        if(progress>=1||string.IsNullOrEmpty(symbols)||label.font==null)return;
        int resolved=Mathf.FloorToInt(progress*glyphCount);
        for(int g=resolved;g<glyphCount;g++)
        {
            char symbol=symbols[(g*17+Mathf.Max(0,phase)*3)%symbols.Length];
            if(!label.font.GetCharacterInfo(symbol,out CharacterInfo character,glyphSize,label.fontStyle))continue;
            for(int corner=0;corner<4;corner++)
            {
                UIVertex vertex=new UIVertex();int index=g*4+corner;vertices.PopulateUIVertex(ref vertex,index);
                vertex.uv0=corner==0?character.uvTopLeft:corner==1?character.uvTopRight:corner==2?character.uvBottomRight:character.uvBottomLeft;
                Color32 tint=Color.Lerp(new Color32(155,235,248,vertex.color.a),vertex.color,progress);
                vertex.color=tint;vertices.SetUIVertex(vertex,index);
            }
        }
    }
}
