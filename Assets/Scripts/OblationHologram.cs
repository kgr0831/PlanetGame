using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public sealed class OblationHologram : MonoBehaviour
{
    public Color accent = new Color(.16f,.78f,1);
    public CanvasGroup revealGroup;
    public OblationUiAudio audioFeedback;
    public bool panelFrame;
    Material ownedMaterial;
    RectTransform rect;
    float age;
    float press = 1;
    float hover;
    float desiredHover;
    bool openingSound;
    Vector2 click = new Vector2(.5f,.5f);
    void Awake()
    {
        rect = (RectTransform)transform;
        var image = GetComponent<Image>();
        ownedMaterial = new Material(image.material);
        image.material = ownedMaterial;
    }
    void OnEnable() { age=0; press=1; hover=desiredHover=0;openingSound=true;if(revealGroup!=null)revealGroup.alpha=1; }
    public void Focus(bool active) => desiredHover=active?1:0;
    public void Press(Vector2 uv) { click=uv; press=0; }
    void LateUpdate()
    {
        if (ownedMaterial == null) return;
        age += Time.unscaledDeltaTime;
        if(openingSound){openingSound=false;if(revealGroup!=null&&audioFeedback!=null)audioFeedback.Open();}
        press = Mathf.Min(1,press+Time.unscaledDeltaTime*3.6f);
        hover = Mathf.Lerp(hover,desiredHover,1-Mathf.Exp(-Time.unscaledDeltaTime*16));
        bool still = OblationGame.ReducedMotion;
        ownedMaterial.SetVector("_Size",new Vector4(rect.rect.width,rect.rect.height,0,0));
        ownedMaterial.SetColor("_Accent",accent);
        ownedMaterial.SetFloat("_PanelMode",panelFrame?1:0);
        ownedMaterial.SetFloat("_Hover",hover);
        ownedMaterial.SetFloat("_Press",still?0:press);
        ownedMaterial.SetVector("_Click",click);
        float revealSpeed=4.4f;
        ownedMaterial.SetFloat("_Reveal",still?1.1f:Mathf.Min(1.1f,age*revealSpeed));
        if(revealGroup != null) revealGroup.alpha=1;
    }
    void OnDestroy() { if(ownedMaterial!=null) Destroy(ownedMaterial); }
}
