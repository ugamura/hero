using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Hero.Input
{
    /// <summary>Touch / Mouse / Accelerometer を単一のジェスチャ入力へ変換する。</summary>
    public class InputDispatcher : MonoBehaviour
    {
        [Header("Tap thresholds")]
        [Range(0.1f, 0.5f)] public float doubleTapWindowSec = 0.25f;
        [Range(0.3f, 1f)] public float longPressSec = 0.5f;
        [Min(5f)] public float tapMoveTolerancePx = 20f;

        [Header("Shake threshold")]
        [Range(1f, 4f)] public float shakeDeltaMagnitude = 1.8f;
        [Min(0.2f)] public float shakeCooldownSec = 1.0f;
        public bool editorKeyboardShortcuts = true;

        public event Action<Vector2> OnSingleTap;
        public event Action<Vector2> OnDoubleTap;
        public event Action<Vector2> OnLongPress;
        public event Action OnShake;

        private bool pressing;
        private bool pressBlockedByUi;
        private Vector2 pressStartPosition;
        private float pressStartTime;
        private bool longPressFired;
        private bool hasPendingTap;
        private Vector2 pendingTapPosition;
        private float pendingTapTime;
        private float lastShakeTime = -10f;
        private Vector3 filteredAcceleration;

        void Update()
        {
            HandlePointer();
            HandlePendingTap();
            HandleShake();
#if UNITY_EDITOR
            if (editorKeyboardShortcuts && UnityEngine.Input.GetKeyDown(KeyCode.H)) OnShake?.Invoke();
#endif
        }

        private bool TryGetPointer(out Vector2 position, out bool down, out bool up, out bool held, out bool overUi)
        {
            if (UnityEngine.Input.touchCount > 0)
            {
                Touch touch = UnityEngine.Input.GetTouch(0);
                position = touch.position;
                down = touch.phase == TouchPhase.Began;
                up = touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled;
                held = touch.phase == TouchPhase.Stationary || touch.phase == TouchPhase.Moved;
                overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(touch.fingerId);
                return true;
            }
            position = UnityEngine.Input.mousePosition;
            down = UnityEngine.Input.GetMouseButtonDown(0);
            up = UnityEngine.Input.GetMouseButtonUp(0);
            held = UnityEngine.Input.GetMouseButton(0);
            overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            return down || up || held;
        }

        private void HandlePointer()
        {
            if (!TryGetPointer(out Vector2 position, out bool down, out bool up, out bool held, out bool overUi)) return;
            if (down)
            {
                pressBlockedByUi = overUi;
                if (pressBlockedByUi) { pressing = false; return; }
                pressing = true;
                pressStartPosition = position;
                pressStartTime = Time.unscaledTime;
                longPressFired = false;
                if (hasPendingTap && Time.unscaledTime - pendingTapTime < doubleTapWindowSec &&
                    Vector2.Distance(position, pendingTapPosition) < tapMoveTolerancePx * 2f)
                {
                    OnDoubleTap?.Invoke(position);
                    hasPendingTap = false;
                    pressing = false;
                }
            }
            else if (pressBlockedByUi)
            {
                if (up) pressBlockedByUi = false;
            }
            else if (pressing && held)
            {
                if (!longPressFired && Time.unscaledTime - pressStartTime >= longPressSec &&
                    Vector2.Distance(position, pressStartPosition) < tapMoveTolerancePx)
                {
                    longPressFired = true;
                    hasPendingTap = false;
                    OnLongPress?.Invoke(position);
                }
            }
            else if (pressing && up)
            {
                pressing = false;
                float duration = Time.unscaledTime - pressStartTime;
                if (!longPressFired && duration < longPressSec &&
                    Vector2.Distance(position, pressStartPosition) < tapMoveTolerancePx)
                {
                    hasPendingTap = true;
                    pendingTapPosition = position;
                    pendingTapTime = Time.unscaledTime;
                }
            }
        }

        private void HandlePendingTap()
        {
            if (hasPendingTap && Time.unscaledTime - pendingTapTime >= doubleTapWindowSec)
            {
                OnSingleTap?.Invoke(pendingTapPosition);
                hasPendingTap = false;
            }
        }

        private void HandleShake()
        {
            Vector3 acceleration = UnityEngine.Input.acceleration;
            filteredAcceleration = Vector3.Lerp(filteredAcceleration, acceleration, 0.1f);
            Vector3 delta = acceleration - filteredAcceleration;
            if (Time.unscaledTime - lastShakeTime < shakeCooldownSec || delta.magnitude < shakeDeltaMagnitude) return;
            lastShakeTime = Time.unscaledTime;
            OnShake?.Invoke();
        }
    }
}
