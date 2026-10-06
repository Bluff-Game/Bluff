using UnityEngine;

namespace Bluff.Game
{
    /// <summary>
    /// Kamera orang pertama: duduk di kursi pemain, sedikit bergoyang seperti bernapas, bisa berguncang
    /// (tembakan) dan mendekat sebentar ke titik tertentu (kartu yang dibuka).
    /// </summary>
    [DefaultExecutionOrder(100)]
    public sealed class CameraRig : MonoBehaviour
    {
        const float YawLimit = 150f;
        const float PitchLimit = 65f;
        const float DegreesPerPixel = 0.12f;
        const float CenterTolerance = 0.05f;

        Camera cam;
        Vector3 basePosition;
        Vector3 baseLookAt;
        float baseFov;
        Vector3 focusPoint;
        float focusWeight;
        float focusTarget;
        float focusFov;
        float shake;
        float shakeStrength;
        float time;
        Vector3 lookOffset;
        Vector3 lookOffsetTarget;
        Vector2 lookAngles;
        Vector2 lookAnglesTarget;
        bool looking;
        bool inputBlocked;
        bool actionFocus;
        bool releaseRequired;
        bool focused;
        bool applicationPaused;
        bool skipMouseDelta;
        CursorLockMode previousCursorLock;
        bool previousCursorVisible;

        bool HasActionFocus => actionFocus || focusTarget > 0f || focusWeight > 0.001f;

        public Camera Camera => cam;
        public bool IsLooking => looking;
        public bool IsViewCentered => lookAngles.sqrMagnitude <= CenterTolerance * CenterTolerance &&
            (HasActionFocus || lookAnglesTarget.sqrMagnitude <= CenterTolerance * CenterTolerance);

        public static CameraRig Attach(Camera camera, Vector3 position, Vector3 lookAt, float fov)
        {
            var rig = camera.GetComponent<CameraRig>();
            if (rig == null)
                rig = camera.gameObject.AddComponent<CameraRig>();
            rig.EndLook();
            rig.lookAngles = Vector2.zero;
            rig.lookAnglesTarget = Vector2.zero;
            rig.lookOffset = Vector3.zero;
            rig.lookOffsetTarget = Vector3.zero;
            rig.focusWeight = 0f;
            rig.focusTarget = 0f;
            rig.shake = 0f;
            rig.shakeStrength = 0f;
            rig.time = 0f;
            rig.inputBlocked = false;
            rig.actionFocus = false;
            rig.cam = camera;
            rig.basePosition = position;
            rig.baseLookAt = lookAt;
            rig.baseFov = fov;
            camera.fieldOfView = fov;
            camera.transform.position = position;
            camera.transform.rotation = Quaternion.LookRotation(lookAt - position);
            return rig;
        }

        public void SetInputBlocked(bool blocked)
        {
            inputBlocked = blocked;
            if (blocked)
                EndLook();
        }

        public void SetActionFocus(bool active)
        {
            actionFocus = active;
            if (active)
                EndLook();
        }

        public void ResetLook()
        {
            EndLook();
            lookAnglesTarget = Vector2.zero;
        }

        void OnEnable()
        {
            focused = Application.isFocused;
            applicationPaused = false;
            releaseRequired = true;
        }

        void OnDisable() => EndLook();

        void OnApplicationFocus(bool hasFocus)
        {
            focused = hasFocus;
            if (!hasFocus)
                EndLook();
        }

        void OnApplicationPause(bool paused)
        {
            applicationPaused = paused;
            if (paused)
                EndLook();
        }

        void EndLook()
        {
            releaseRequired = true;
            skipMouseDelta = false;
            if (!looking)
                return;
            looking = false;
            Cursor.lockState = previousCursorLock;
            Cursor.visible = previousCursorVisible;
        }

        void UpdateLookInput()
        {
            bool held = InputCompat.RightMouseHeld();
            if (!held)
            {
                EndLook();
                releaseRequired = false;
            }
            if (!focused || !Application.isFocused || applicationPaused || Time.timeScale <= 0f ||
                inputBlocked || GameTutorial.IsShowing || HasActionFocus || InputCompat.ModalIsShowing())
            {
                EndLook();
                return;
            }
            if (InputCompat.EscapePressed())
            {
                EndLook();
                return;
            }
            if (InputCompat.ResetLookPressed())
            {
                ResetLook();
                return;
            }
            if (looking)
            {
                if (Cursor.lockState != CursorLockMode.Locked)
                {
                    EndLook();
                    return;
                }
                if (skipMouseDelta)
                {
                    skipMouseDelta = false;
                    return;
                }
                var delta = InputCompat.MouseDelta();
                float sensitivity = GameSettings.LookSensitivity;
                if (float.IsNaN(sensitivity) || float.IsInfinity(sensitivity))
                    sensitivity = 1f;
                sensitivity = Mathf.Max(0f, sensitivity) * DegreesPerPixel;
                lookAnglesTarget.x = Mathf.Clamp(lookAnglesTarget.x + delta.x * sensitivity, -YawLimit, YawLimit);
                lookAnglesTarget.y = Mathf.Clamp(lookAnglesTarget.y - delta.y * sensitivity, -PitchLimit, PitchLimit);
                return;
            }
            if (!held || releaseRequired || !InputCompat.RightMousePressed())
                return;
            if (!cam.pixelRect.Contains(InputCompat.MousePosition()) || InputCompat.PointerOverUI())
            {
                releaseRequired = true;
                return;
            }
            lookAnglesTarget = lookAngles;
            previousCursorLock = Cursor.lockState;
            previousCursorVisible = Cursor.visible;
            looking = true;
            skipMouseDelta = true;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        public void Shake(float strength, float duration)
        {
            shakeStrength = Mathf.Max(shakeStrength * shake, strength);
            shake = Mathf.Max(shake, duration);
        }

        /// <summary>Condong ke sebuah titik (0..1 = seberapa kuat) dengan FOV tertentu.</summary>
        public void Focus(Vector3 point, float weight, float fov)
        {
            focusPoint = point;
            focusTarget = Mathf.Clamp01(weight);
            focusFov = fov;
            if (focusTarget > 0f)
                EndLook();
        }

        public void ClearFocus() => focusTarget = 0f;

        /// <summary>Lirik sedikit ke arah titik (mis. pemain yang sedang giliran), tanpa memutar kepala penuh.</summary>
        public void Glance(Vector3 point)
        {
            var dir = (point - basePosition).normalized;
            var baseDir = (baseLookAt - basePosition).normalized;
            lookOffsetTarget = (dir - baseDir) * 0.12f;
        }

        void LateUpdate()
        {
            if (cam == null)
            {
                EndLook();
                return;
            }
            UpdateLookInput();
            float dt = Time.deltaTime;
            if (dt <= 0f || Time.timeScale <= 0f || !focused || !Application.isFocused || applicationPaused)
                return;
            time += dt;
            focusWeight = Mathf.Lerp(focusWeight, focusTarget, 1f - Mathf.Exp(-4f * dt));
            if (focusTarget == 0f && focusWeight < 0.001f)
                focusWeight = 0f;
            lookOffset = Vector3.Lerp(lookOffset, lookOffsetTarget, 1f - Mathf.Exp(-2f * dt));
            var anglesTarget = HasActionFocus ? Vector2.zero : lookAnglesTarget;
            lookAngles = Vector2.Lerp(lookAngles, anglesTarget, 1f - Mathf.Exp(-(looking ? 18f : 10f) * dt));
            if ((lookAngles - anglesTarget).sqrMagnitude < CenterTolerance * CenterTolerance)
                lookAngles = anglesTarget;

            var breathe = new Vector3(Mathf.Sin(time * 0.7f) * 0.004f, Mathf.Sin(time * 1.3f) * 0.006f, 0f);
            var position = basePosition + breathe;
            var look = baseLookAt + lookOffset;
            if (focusWeight > 0.001f)
            {
                position = Vector3.Lerp(position, Vector3.Lerp(basePosition, focusPoint, 0.35f) + Vector3.up * 0.25f, focusWeight);
                look = Vector3.Lerp(look, focusPoint, focusWeight);
            }

            if (shake > 0f)
            {
                shake -= dt;
                float s = shakeStrength * Mathf.Clamp01(shake * 4f);
                position += new Vector3(
                    (Mathf.PerlinNoise(time * 40f, 0.1f) - 0.5f) * s,
                    (Mathf.PerlinNoise(0.3f, time * 40f) - 0.5f) * s,
                    0f);
            }

            cam.transform.position = position;
            cam.transform.rotation = Quaternion.AngleAxis(lookAngles.x, Vector3.up) *
                Quaternion.LookRotation(look - position) * Quaternion.Euler(lookAngles.y, 0f, 0f);
            cam.fieldOfView = Mathf.Lerp(baseFov, focusFov > 0f ? focusFov : baseFov, focusWeight);
        }
    }
}
