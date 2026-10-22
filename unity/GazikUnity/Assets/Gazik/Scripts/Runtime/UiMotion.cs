using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Gazik
{
    public sealed class UiMotion:MonoBehaviour,IPointerEnterHandler,IPointerExitHandler,IPointerDownHandler,IPointerUpHandler
    {
        bool hover,pressed,entering;float enteredAt;CanvasGroup group;Selectable selectable;
        void Awake(){selectable=GetComponent<Selectable>();}
        public void Enter(){entering=true;enteredAt=Time.unscaledTime;group=gameObject.AddComponent<CanvasGroup>();group.alpha=0;transform.localScale=Vector3.one*.975f;}
        void Update()
        {
            if(entering){float t=Mathf.Clamp01((Time.unscaledTime-enteredAt)/.22f);group.alpha=t;transform.localScale=Vector3.one*Mathf.Lerp(.975f,1,1-Mathf.Pow(1-t,3));if(t>=1)entering=false;return;}
            float target=selectable&&selectable.IsInteractable()?(pressed?.965f:hover?1.018f:1):1;
            transform.localScale=Vector3.Lerp(transform.localScale,Vector3.one*target,1-Mathf.Exp(-Time.unscaledDeltaTime*20));
        }
        public void OnPointerEnter(PointerEventData e){hover=true;}public void OnPointerExit(PointerEventData e){hover=false;pressed=false;}
        public void OnPointerDown(PointerEventData e){pressed=true;}public void OnPointerUp(PointerEventData e){pressed=false;}
        void OnDisable(){hover=pressed=false;transform.localScale=Vector3.one;}
    }
}
