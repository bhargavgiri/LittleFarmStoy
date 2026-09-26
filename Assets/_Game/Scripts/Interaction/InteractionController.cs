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

        [Header("Line of sight")]
        [Tooltip("Reject an interactable with solid geometry between it and the player, so the " +
                 "farmer cannot reach through a barn wall. " +
                 "DEFAULT OFF. This was added at the same time the animal loops stopped " +
                 "responding and has never been proven safe, so it stays off until the " +
                 "automated gameplay test passes with it enabled. A missed interaction is a " +
                 "far worse bug than reaching through a wall.")]
        [SerializeField] private bool requireLineOfSight;
        [Tooltip("Targets closer than this skip the check - at arm's length nothing can " +
                 "meaningfully be in the way, and the cast would only risk a false negative.")]
        [SerializeField] private float lineOfSightSkipDistance = 1.2f;
        [Tooltip("How high above a target's origin to aim. Keeps the cast clear of the ground.")]
        [SerializeField] private float lineOfSightTargetHeight = 0.5f;
        [Tooltip("Layers that block reach. Leave the ground layer out of this mask.")]
        [SerializeField] private LayerMask lineOfSightBlockers = ~0;

        [Header("Diagnostics")]
        [Tooltip("Development only. Logs what the USE button resolved to and why. Leave off " +
                 "unless you are diagnosing an interaction that appears to do nothing.")]
        [SerializeField] private bool logInteractions;

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
                if (logInteractions)
                {
                    Debug.Log("[Interaction] USE pressed with nothing in range.", this);
                }

                return;
            }

            if (!Current.CanInteract(gameObject))
            {
                if (logInteractions)
                {
                    Debug.Log("[Interaction] USE pressed but '" + Current.InteractionLabel +
                              "' refused CanInteract.", this);
                }

                return;
            }

            if (logInteractions)
            {
                Debug.Log("[Interaction] USE -> '" + Current.InteractionLabel + "' (" +
                          Current.Transform.name + ", priority " + Current.InteractionPriority + ")", this);
            }

            Current.Interact(gameObject);
        }

        private void Scan()
        {
            Vector3 origin = transform.TransformPoint(detectionOffset);
            int count = Physics.OverlapSphereNonAlloc(
                origin, interactionRadius, hitBuffer, interactableLayers, QueryTriggerInteraction.Collide);

            IInteractable best = null;
            int bestPriority = int.MinValue;
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

                int priority = candidate.InteractionPriority;
                if (priority < bestPriority)
                {
                    continue;
                }

                float sqr = (candidate.Transform.position - origin).sqrMagnitude;

                if (!HasLineOfSight(origin, candidate, sqr))
                {
                    continue;
                }

                // Priority first, distance only as the tie-break: a chicken standing inside the
                // coop's area trigger must still win the interact button.
                if (priority > bestPriority || sqr < bestSqr)
                {
                    bestPriority = priority;
                    bestSqr = sqr;
                    best = candidate;
                }
            }

            if (!ReferenceEquals(best, Current))
            {
                Current = best;
                FocusChanged?.Invoke(Current);

                if (logInteractions)
                {
                    Debug.Log("[Interaction] focus -> " + (best == null
                        ? "nothing"
                        : "'" + best.InteractionLabel + "' on " + best.Transform.name +
                          " (priority " + best.InteractionPriority + ", " +
                          Mathf.Sqrt(bestSqr).ToString("0.00") + "m) out of " + count + " collider(s)"), this);
                }
            }
        }

        /// <summary>
        /// One linecast per candidate, on the scan timer rather than per frame. Triggers are
        /// ignored, so other interactables can never shadow each other; only solid geometry
        /// (walls, fence posts) blocks reach.
        /// </summary>
        private bool HasLineOfSight(Vector3 origin, IInteractable candidate, float sqrDistance)
        {
            if (!requireLineOfSight || sqrDistance <= lineOfSightSkipDistance * lineOfSightSkipDistance)
            {
                return true;
            }

            Vector3 target = candidate.Transform.position + Vector3.up * lineOfSightTargetHeight;
            Vector3 offset = target - origin;
            float distance = offset.magnitude;

            if (distance <= 0.001f)
            {
                return true;
            }

            if (!Physics.Raycast(origin, offset / distance, out RaycastHit hit, distance,
                    lineOfSightBlockers, QueryTriggerInteraction.Ignore))
            {
                return true;
            }

            // Hitting the target's own geometry is not an obstruction.
            if (hit.transform == candidate.Transform || hit.transform.IsChildOf(candidate.Transform))
            {
                return true;
            }

            if (logInteractions)
            {
                Debug.Log("[Interaction] line of sight to '" + candidate.InteractionLabel +
                          "' blocked by '" + hit.transform.name + "'.", this);
            }

            return false;
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
