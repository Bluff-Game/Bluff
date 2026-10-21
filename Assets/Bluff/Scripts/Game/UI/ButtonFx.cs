using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Bluff.Game
{
    /// <summary>Efek kecil untuk tombol: membesar saat disorot, mengecil saat ditekan.</summary>
    public sealed class ButtonFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler,
        ISelectHandler, IDeselectHandler
    {
        Selectable selectable;
        Outline focusOutline;
        bool hovered;
        bool pressed;
        bool focused;

        void Awake()
        {
            selectable = GetComponent<Selectable>();
            focusOutline = gameObject.AddComponent<Outline>();
            focusOutline.effectColor = new Color(0.71f, 0.60f, 0.38f, 0.95f);
            focusOutline.effectDistance = new Vector2(2f, 2f);
            focusOutline.enabled = false;
        }

        public void OnPointerEnter(PointerEventData eventData) => hovered = true;

        public void OnPointerExit(PointerEventData eventData)
        {
            hovered = false;
            pressed = false;
        }

        public void OnPointerDown(PointerEventData eventData) => pressed = true;

        public void OnPointerUp(PointerEventData eventData) => pressed = false;

        public void OnSelect(BaseEventData eventData) => focused = true;

        public void OnDeselect(BaseEventData eventData) => focused = false;

        void OnDisable()
        {
            hovered = false;
            pressed = false;
            focused = false;
            transform.localScale = Vector3.one;
            if (focusOutline != null)
                focusOutline.enabled = false;
        }

        void Update()
        {
            bool interactable = selectable == null || selectable.IsInteractable();
            float target = !interactable ? 1f : pressed ? 0.985f : hovered ? 1.008f : 1f;
            float s = Mathf.Lerp(transform.localScale.x, target, 1f - Mathf.Exp(-20f * Time.unscaledDeltaTime));
            transform.localScale = new Vector3(s, s, 1f);
            if (focusOutline != null)
                focusOutline.enabled = interactable && focused;
        }
    }
}
