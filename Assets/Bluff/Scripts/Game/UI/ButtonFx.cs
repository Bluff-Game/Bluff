using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Bluff.Game
{
    /// <summary>Efek kecil untuk tombol: membesar saat disorot, mengecil saat ditekan.</summary>
    public sealed class ButtonFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        Selectable selectable;
        bool hovered;
        bool pressed;

        void Awake() => selectable = GetComponent<Selectable>();

        public void OnPointerEnter(PointerEventData eventData) => hovered = true;

        public void OnPointerExit(PointerEventData eventData)
        {
            hovered = false;
            pressed = false;
        }

        public void OnPointerDown(PointerEventData eventData) => pressed = true;

        public void OnPointerUp(PointerEventData eventData) => pressed = false;

        void OnDisable()
        {
            hovered = false;
            pressed = false;
            transform.localScale = Vector3.one;
        }

        void Update()
        {
            bool interactable = selectable == null || selectable.IsInteractable();
            float target = !interactable ? 1f : pressed ? 0.985f : hovered ? 1.008f : 1f;
            float s = Mathf.Lerp(transform.localScale.x, target, 1f - Mathf.Exp(-20f * Time.unscaledDeltaTime));
            transform.localScale = new Vector3(s, s, 1f);
        }
    }
}
