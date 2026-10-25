using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Bluff.Game
{
    /// <summary>
    /// Pembacaan keyboard yang jalan di Input System baru maupun Input Manager lama,
    /// tergantung pengaturan Player Settings > Active Input Handling.
    /// </summary>
    public static class InputCompat
    {
        static readonly List<RaycastResult> uiHits = new List<RaycastResult>();
        static EventSystem pointerEventSystem;
        static PointerEventData pointerEvent;

        static bool ActionsBlocked => GameTutorial.IsShowing || RightMouseHeld() || !Application.isFocused || Time.timeScale <= 0f;

        public static bool RightMouseHeld()
        {
#if ENABLE_INPUT_SYSTEM
            var mouse = UnityEngine.InputSystem.Mouse.current;
            return mouse != null && mouse.rightButton.isPressed;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButton(1);
#else
            return false;
#endif
        }

        public static bool RightMousePressed()
        {
#if ENABLE_INPUT_SYSTEM
            var mouse = UnityEngine.InputSystem.Mouse.current;
            return mouse != null && mouse.rightButton.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetMouseButtonDown(1);
#else
            return false;
#endif
        }

        public static Vector2 MousePosition()
        {
#if ENABLE_INPUT_SYSTEM
            var mouse = UnityEngine.InputSystem.Mouse.current;
            return mouse != null ? mouse.position.ReadValue() : Vector2.zero;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.mousePosition;
#else
            return Vector2.zero;
#endif
        }

        public static Vector2 MouseDelta()
        {
#if ENABLE_INPUT_SYSTEM
            var mouse = UnityEngine.InputSystem.Mouse.current;
            return mouse != null ? mouse.delta.ReadValue() : Vector2.zero;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y")) * 10f;
#else
            return Vector2.zero;
#endif
        }

        public static bool ResetLookPressed()
        {
            if (GameTutorial.IsShowing || !Application.isFocused || Time.timeScale <= 0f)
                return false;
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            var input = selected != null ? selected.GetComponentInParent<InputField>() : null;
            if (input != null && input.isFocused)
                return false;
#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            return kb != null && kb.rKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.R);
#else
            return false;
#endif
        }

        public static bool PointerOverUI()
        {
            RaycastUI(MousePosition());
            foreach (var hit in uiHits)
            {
                if (!(hit.module is GraphicRaycaster) || hit.gameObject == null)
                    continue;
                if (IsModal(hit.gameObject.transform))
                    return true;
                var selectable = hit.gameObject.GetComponentInParent<Selectable>();
                if (selectable != null)
                {
                    if (selectable.IsActive() && selectable.IsInteractable())
                        return true;
                    continue;
                }
                if (ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit.gameObject) != null ||
                    ExecuteEvents.GetEventHandler<IPointerDownHandler>(hit.gameObject) != null ||
                    ExecuteEvents.GetEventHandler<IDragHandler>(hit.gameObject) != null ||
                    ExecuteEvents.GetEventHandler<IScrollHandler>(hit.gameObject) != null)
                    return true;
            }
            return false;
        }

        public static bool ModalIsShowing()
        {
            RaycastUI(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f));
            foreach (var hit in uiHits)
            {
                if (hit.module is GraphicRaycaster && hit.gameObject != null && IsModal(hit.gameObject.transform))
                    return true;
            }
            return false;
        }

        static bool IsModal(Transform target)
        {
            for (var current = target; current != null; current = current.parent)
            {
                if (current.name.StartsWith("Modal ", System.StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        static void RaycastUI(Vector2 position)
        {
            uiHits.Clear();
            var system = EventSystem.current;
            if (system == null || !system.isActiveAndEnabled)
                return;
            if (pointerEvent == null || pointerEventSystem != system)
            {
                pointerEventSystem = system;
                pointerEvent = new PointerEventData(system);
            }
            pointerEvent.Reset();
            pointerEvent.position = position;
            system.RaycastAll(pointerEvent, uiHits);
        }

        public static bool EscapePressed()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            return kb != null && kb.escapeKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.Escape);
#else
            return false;
#endif
        }

        /// <summary>Spasi atau B = BLUFF!</summary>
        public static bool BluffPressed(bool allowSpace = true)
        {
            if (ActionsBlocked)
                return false;
#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            return kb != null && ((allowSpace && kb.spaceKey.wasPressedThisFrame) || kb.bKey.wasPressedThisFrame);
#elif ENABLE_LEGACY_INPUT_MANAGER
            return (allowSpace && Input.GetKeyDown(KeyCode.Space)) || Input.GetKeyDown(KeyCode.B);
#else
            return false;
#endif
        }

        /// <summary>Enter = taruh kartu.</summary>
        public static bool ConfirmPressed()
        {
            if (ActionsBlocked)
                return false;
#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            return kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame);
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
#else
            return false;
#endif
        }

        /// <summary>M = mute or unmute voice chat.</summary>
        public static bool MutePressed()
        {
            if (ActionsBlocked)
                return false;
#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            return kb != null && kb.mKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.M);
#else
            return false;
#endif
        }

        /// <summary>P = pass.</summary>
        public static bool PassPressed()
        {
            if (ActionsBlocked)
                return false;
#if ENABLE_INPUT_SYSTEM
            var kb = UnityEngine.InputSystem.Keyboard.current;
            return kb != null && kb.pKey.wasPressedThisFrame;
#elif ENABLE_LEGACY_INPUT_MANAGER
            return Input.GetKeyDown(KeyCode.P);
#else
            return false;
#endif
        }
    }
}
