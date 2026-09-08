using System;
using LittleFarmStory.Input;
using UnityEngine;

namespace LittleFarmStory.Interaction
{
    /// <summary>
    /// Finds the nearest interactable around the player and routes the interact button to it.
    /// Scans on a timer (not every frame) with a non-allocating overlap query to stay cheap on mobile.
    /// </summary>
    [DisallowMultipleComponent]
    public class InteractionController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerInputProvider input;

        [Header("Detection")]
        [SerializeField] private float interactionRadius = 2.6f;
        [SerializeField] private Vector3 detectionOffset = new Vector3(0f, 0.9f, 0f);
        [SerializeField] private LayerMask interactableLayers = ~0;
        [Tooltip("Seconds between proximity scans. Lower = more responsive, higher = cheaper.")]
        [SerializeField] private float scanInterval = 0.12f;
        [SerializeField] private int maxCandidates = 12;

        private Collider[] hitBuffer;
        private float scanTimer;

        /// <summary>Currently focused interactable, or null.</summary>
        public IInteractable Current { get; private set; }

        /// <summary>Raised when the focused interactable changes (including to null).</summary>
        public event Action<IInteractable> FocusChanged;

        private void Awake()
        {
            hitBuffer = new Collider[Mathf.Max(1, maxCandidates)];

            if (input == null)
            {
                Debug.LogWarning($"{nameof(InteractionController)} on '{name}' has no {nameof(PlayerInputProvider)} assigned; interaction input is disabled.", this);
            }
        }

        private void Update()
        {
            scanTimer -= Time.deltaTime;
            if (scanTimer <= 0f)
            {
                scanTimer = scanInterval;
                Scan();
            }

            if (input != null && input.ConsumeInteractPressed())
            {
                TryInteract();
            }
        }

        public void TryInteract()
        {
            if (Current == null)
            {
                return;
            }

            if (Current.CanInteract(gameObject))
            {
                Current.Interact(gameObject);
            }
        }

        private void Scan()
        {
            Vector3 origin = transform.TransformPoint(detectionOffset);
            int count = Physics.OverlapSphereNonAlloc(
                origin, interactionRadius, hitBuffer, interactableLayers, QueryTriggerInteraction.Collide);

            IInteractable best = null;
            float bestSqr = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                Collider hit = hitBuffer[i];
                if (hit == null)
                {
                    continue;
                }

                IInteractable candidate = hit.GetComponentInParent<IInteractable>();
                if (candidate == null || !candidate.CanInteract(gameObject))
                {
                    continue;
                }

                float sqr = (candidate.Transform.position - origin).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = candidate;
                }
            }

            if (!ReferenceEquals(best, Current))
            {
                Current = best;
                FocusChanged?.Invoke(Current);
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.35f);
            Gizmos.DrawWireSphere(transform.TransformPoint(detectionOffset), interactionRadius);
        }
#endif
    }
}
