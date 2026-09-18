using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace BasketballBetting
{
    public static class GameInput
    {
        public static bool ShootPressedThisFrame()
        {
            if (IsPointerOverUi())
                return KeyboardPressed();

            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                return true;

            if (Touchscreen.current != null)
            {
                var touch = Touchscreen.current.primaryTouch;
                if (touch.press.wasPressedThisFrame)
                    return true;
            }

            return KeyboardPressed();
        }

        public static bool MenuPressedThisFrame()
        {
            if (Keyboard.current == null)
                return false;
            return Keyboard.current.escapeKey.wasPressedThisFrame
                || Keyboard.current.pKey.wasPressedThisFrame;
        }

        public static bool KeyboardPressed()
        {
            if (Keyboard.current == null)
                return false;
            return Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.enterKey.wasPressedThisFrame;
        }

        public static bool IsPointerOverUi()
        {
            EventSystem es = EventSystem.current;
            if (es == null)
                return false;

            if (Mouse.current != null && es.IsPointerOverGameObject())
                return true;

            if (Touchscreen.current != null)
            {
                var touch = Touchscreen.current.primaryTouch;
                if (touch.press.isPressed)
                    return es.IsPointerOverGameObject(touch.touchId.ReadValue());
            }

            return false;
        }

        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null)
                return;

            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
            Object.DontDestroyOnLoad(go);
        }
    }

    public static class FontUtil
    {
        static Font _cached;

        public static Font Get()
        {
            if (_cached != null)
                return _cached;

            string[] prefer = { "Segoe UI", "Inter", "Roboto", "Helvetica Neue", "Arial" };
            // Browsers expose no OS fonts; only ask for fonts the platform actually reports as installed.
            var installed = new System.Collections.Generic.HashSet<string>(
                Application.platform == RuntimePlatform.WebGLPlayer ? new string[0] : Font.GetOSInstalledFontNames());
            for (int i = 0; i < prefer.Length; i++)
            {
                if (!installed.Contains(prefer[i]))
                    continue;
                try
                {
                    var os = Font.CreateDynamicFontFromOSFont(prefer[i], 16);
                    if (os != null)
                    {
                        _cached = os;
                        return _cached;
                    }
                }
                catch { }
            }

            _cached = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_cached == null)
                _cached = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return _cached;
        }
    }
}
