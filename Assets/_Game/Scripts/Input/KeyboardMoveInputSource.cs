using UnityEngine;
using UnityEngine.InputSystem;

namespace LittleFarmStory.Input
{
    /// <summary>
    /// Editor / desktop convenience input so the prototype is testable without a touch screen.
    /// Reads the Input System keyboard directly; it is never required at runtime on Android.
    /// </summary>
    [DisallowMultipleComponent]
    public class KeyboardMoveInputSource : MonoBehaviour, IMoveInputSource, IActionInputSource
    {
        [SerializeField] private bool enableOnMobileBuilds = false;

        private Vector2 cached;

        private bool IsAvailable
        {
            get
            {
                if (!enableOnMobileBuilds && Application.isMobilePlatform)
                {
                    return false;
                }

                return Keyboard.current != null;
            }
        }

        public Vector2 MoveInput
        {
            get
            {
                ReadKeyboard();
                return cached;
            }
        }

        public bool HasMoveInput
        {
            get
            {
                ReadKeyboard();
                return cached.sqrMagnitude > 0.0001f;
            }
        }

        public bool ConsumeInteractPressed()
        {
            if (!IsAvailable)
            {
                return false;
            }

            Keyboard kb = Keyboard.current;
            return kb.eKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame;
        }

        private void ReadKeyboard()
        {
            if (!IsAvailable)
            {
                cached = Vector2.zero;
                return;
            }

            Keyboard kb = Keyboard.current;
            float x = 0f;
            float y = 0f;

            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) { x -= 1f; }
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) { x += 1f; }
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) { y -= 1f; }
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) { y += 1f; }

            cached = new Vector2(x, y);
            if (cached.sqrMagnitude > 1f)
            {
                cached = cached.normalized;
            }
        }
    }
}
