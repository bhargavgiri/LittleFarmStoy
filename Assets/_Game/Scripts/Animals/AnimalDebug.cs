using UnityEngine;

namespace LittleFarmStory.Animals
{
    /// <summary>
    /// Development-only tracing for the animal gameplay chain.
    ///
    /// Every call site is wrapped so that a non-development build compiles the logging away
    /// entirely - there is no string building and no branch left in a shipped player.
    /// <see cref="AnimalHabitat"/> owns the toggle, so one tick box turns tracing on for the
    /// whole farm.
    /// </summary>
    public static class AnimalDebug
    {
        /// <summary>Off unless a habitat turns it on. Meaningless outside the Editor.</summary>
        public static bool Enabled { get; set; }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        public static void Log(Object context, string headline, params string[] lines)
        {
            if (!Enabled)
            {
                return;
            }

            string body = "[ANIMAL DEBUG] " + headline;

            if (lines != null)
            {
                for (int i = 0; i < lines.Length; i++)
                {
                    if (!string.IsNullOrEmpty(lines[i]))
                    {
                        body += "\n  " + lines[i];
                    }
                }
            }

            Debug.Log(body, context);
        }
    }
}
