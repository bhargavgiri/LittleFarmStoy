using System;
using UnityEngine;

namespace LittleFarmStory.Core
{
    /// <summary>
    /// One-line relay so gameplay code can say "Tilled!" without knowing a HUD exists.
    /// Lives on the player; the HUD holds a serialized reference and subscribes.
    /// This is intentionally not a notification framework - no queue, no priorities, no icons.
    /// </summary>
    [DisallowMultipleComponent]
    public class ActionFeedbackChannel : MonoBehaviour
    {
        /// <summary>Raised with the message text. Listeners decide how (or whether) to show it.</summary>
        public event Action<string> MessagePosted;

        public void Post(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            MessagePosted?.Invoke(message);
        }
    }
}
