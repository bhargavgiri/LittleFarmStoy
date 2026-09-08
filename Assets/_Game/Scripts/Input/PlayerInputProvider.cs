using System.Collections.Generic;
using UnityEngine;

namespace LittleFarmStory.Input
{
    /// <summary>
    /// Single place the gameplay code asks for input. Sources are wired in the inspector
    /// (no singletons, no Find calls) and polled on demand, so there is no script execution
    /// order dependency between input, movement and interaction.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerInputProvider : MonoBehaviour
    {
        [Tooltip("Components implementing IMoveInputSource and/or IActionInputSource. " +
                 "Order matters: the first source reporting input wins.")]
        [SerializeField] private List<MonoBehaviour> sourceBehaviours = new List<MonoBehaviour>();

        private readonly List<IMoveInputSource> moveSources = new List<IMoveInputSource>();
        private readonly List<IActionInputSource> actionSources = new List<IActionInputSource>();

        private void Awake()
        {
            for (int i = 0; i < sourceBehaviours.Count; i++)
            {
                Register(sourceBehaviours[i]);
            }
        }

        /// <summary>Runtime registration, for sources spawned after scene load.</summary>
        public void Register(MonoBehaviour behaviour)
        {
            if (behaviour == null)
            {
                return;
            }

            if (behaviour is IMoveInputSource move && !moveSources.Contains(move))
            {
                moveSources.Add(move);
            }

            if (behaviour is IActionInputSource action && !actionSources.Contains(action))
            {
                actionSources.Add(action);
            }
        }

        public void Unregister(MonoBehaviour behaviour)
        {
            if (behaviour == null)
            {
                return;
            }

            if (behaviour is IMoveInputSource move)
            {
                moveSources.Remove(move);
            }

            if (behaviour is IActionInputSource action)
            {
                actionSources.Remove(action);
            }
        }

        /// <summary>Highest priority active movement vector, clamped to unit length.</summary>
        public Vector2 Move
        {
            get
            {
                for (int i = 0; i < moveSources.Count; i++)
                {
                    IMoveInputSource source = moveSources[i];
                    if (source == null || !source.HasMoveInput)
                    {
                        continue;
                    }

                    return Vector2.ClampMagnitude(source.MoveInput, 1f);
                }

                return Vector2.zero;
            }
        }

        /// <summary>True once per interact press from any source. Call from exactly one consumer.</summary>
        public bool ConsumeInteractPressed()
        {
            bool pressed = false;
            for (int i = 0; i < actionSources.Count; i++)
            {
                IActionInputSource source = actionSources[i];
                if (source != null && source.ConsumeInteractPressed())
                {
                    pressed = true;
                }
            }

            return pressed;
        }
    }
}
