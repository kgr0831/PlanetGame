using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class OblationButtonFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler, IPointerDownHandler, ISubmitHandler
{
    bool hover;
    public OblationUiAudio audioFeedback;
    bool selected;
    Button button;
    OblationHologram hologram;
    float pressed;
    void Awake() { button = GetComponent<Button>(); hologram=GetComponent<OblationHologram>(); }
    public void OnPointerDown(PointerEventData data)
    {
        RectTransform rect=(RectTransform)transform;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(rect,data.position,data.pressEventCamera,out Vector2 local);
        Pulse(new Vector2(local.x/rect.rect.width+rect.pivot.x,local.y/rect.rect.height+rect.pivot.y));
    }
    public void OnSubmit(BaseEventData data) => Pulse(new Vector2(.5f,.5f));
    void Pulse(Vector2 uv) { if(button==null||!button.interactable)return; pressed=1;if(hologram!=null)hologram.Press(uv); }
    public void OnPointerEnter(PointerEventData data) { hover = true;if(button!=null&&button.interactable&&audioFeedback!=null)audioFeedback.Hover(); }
    public void OnPointerExit(PointerEventData data) => hover = false;
    public void OnSelect(BaseEventData data) { selected = true;if(button!=null&&button.interactable&&audioFeedback!=null)audioFeedback.Hover(); }
    public void OnDeselect(BaseEventData data) => selected = false;
    void OnDisable() { hover = selected = false; pressed=0; transform.localScale = Vector3.one; }
    void Update()
    {
        float target = !OblationGame.ReducedMotion && button != null && button.interactable && (hover || selected) ? 1.025f : 1;
        pressed=Mathf.Max(0,pressed-Time.unscaledDeltaTime*4);
        if(!OblationGame.ReducedMotion)target-=pressed*.045f;
        if(hologram!=null)hologram.Focus(button!=null&&button.interactable&&(hover||selected));
        transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one * target, 1 - Mathf.Exp(-Time.unscaledDeltaTime * 14));
    }
}
