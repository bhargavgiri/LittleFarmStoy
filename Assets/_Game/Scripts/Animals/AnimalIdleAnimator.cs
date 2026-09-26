using UnityEngine;

namespace LittleFarmStory.Animals
{
    /// <summary>
    /// Gentle idle motion for a segmented animal prop: breathing, head sway, tail swish and
    /// an occasional peck or graze dip.
    ///
    /// This is presentation only. There is no AI, no pathing, no gameplay state - the animals
    /// stand where the scene builder placed them and simply look alive.
    ///
    /// Two things keep it cheap enough to scale to a full flock:
    ///   - each instance gets a random phase offset, so nothing moves in lockstep and no
    ///     shared timer or manager is needed;
    ///   - motion is skipped entirely while the animal is off screen.
    ///
    /// Phase 5 adds a walk: <see cref="SetLocomotion"/> is pushed in by the gameplay layer once
    /// per frame and blends a stride into the same joints. This is an extension of the existing
    /// segmented architecture, not an Animator - there is still no controller, no clip and no
    /// state machine, and with locomotion at zero the idle behaves exactly as it did before.
    ///
    /// Transform ownership: this component writes ONLY child joint locals plus the body's local
    /// position and scale. The animal ROOT belongs to <c>AnimalMovement</c>. Nothing overlaps.
    /// </summary>
    [DisallowMultipleComponent]
    public class AnimalIdleAnimator : MonoBehaviour
    {
        [Header("Joints")]
        [SerializeField] private Transform body;
        [SerializeField] private Transform head;
        [SerializeField] private Transform tail;
        [Tooltip("Optional. Legs shift weight subtly; leave empty for animals without them.")]
        [SerializeField] private Transform[] legs;

        [Header("Breathing")]
        [SerializeField] private float breathRate = 0.42f;
        [SerializeField] private float breathScale = 0.018f;
        [SerializeField] private float bodyBob = 0.012f;

        [Header("Head")]
        [SerializeField] private float headSwayRate = 0.31f;
        [SerializeField] private float headSwayAngle = 7f;
        [SerializeField] private float headNodAngle = 4f;

        [Header("Dip (peck / graze)")]
        [Tooltip("Average seconds between dips. Zero disables them.")]
        [SerializeField] private float dipInterval = 5.5f;
        [SerializeField] private float dipAngle = 34f;
        [SerializeField] private float dipDuration = 0.55f;

        [Header("Tail")]
        [SerializeField] private float tailRate = 0.72f;
        [SerializeField] private float tailAngle = 12f;

        [Header("Walk")]
        [Tooltip("Full stride cycles per second while walking.")]
        [SerializeField] private float strideRate = 2.6f;
        [SerializeField] private float legStrideAngle = 26f;
        [Tooltip("Extra vertical bounce added to the body while walking.")]
        [SerializeField] private float walkBob = 0.022f;
        [Tooltip("Seconds to blend the walk in and out.")]
        [SerializeField] private float locomotionBlend = 0.18f;

        [Header("Visibility")]
        [Tooltip("Skip all motion while off screen. Leave on unless debugging.")]
        [SerializeField] private bool pauseWhenOffScreen = true;

        private Vector3 bodyRestPosition;
        private Vector3 bodyRestScale;
        private Quaternion headRest;
        private Quaternion tailRest;
        private Quaternion[] legRest;

        private float phase;
        private float stridePhase;
        private float locomotion;
        private float locomotionTarget;
        private float dipTimer;
        private float dipElapsed = -1f;
        private bool visible = true;

        private void Awake()
        {
            // A per-instance offset is what stops a flock from pulsing in unison.
            phase = Random.value * Mathf.PI * 2f;
            dipTimer = dipInterval * (0.35f + Random.value);

            if (body != null)
            {
                bodyRestPosition = body.localPosition;
                bodyRestScale = body.localScale;
            }

            if (head != null) { headRest = head.localRotation; }
            if (tail != null) { tailRest = tail.localRotation; }

            if (legs != null)
            {
                legRest = new Quaternion[legs.Length];
                for (int i = 0; i < legs.Length; i++)
                {
                    if (legs[i] != null)
                    {
                        legRest[i] = legs[i].localRotation;
                    }
                }
            }
        }

        /// <summary>
        /// Drive from the gameplay layer: 0 standing, 1 walking at full speed.
        /// Safe to call every frame, and safe never to call at all.
        /// </summary>
        public void SetLocomotion(float normalisedSpeed)
        {
            locomotionTarget = Mathf.Clamp01(normalisedSpeed);
        }

        /// <summary>Starts a peck or graze dip now, e.g. the moment the animal is fed.</summary>
        public void TriggerDip()
        {
            if (dipElapsed < 0f)
            {
                dipElapsed = 0f;
            }
        }

        private void OnBecameVisible()
        {
            visible = true;
        }

        private void OnBecameInvisible()
        {
            visible = false;
        }

        private void OnDisable()
        {
            RestorePose();
        }

        private void RestorePose()
        {
            if (body != null)
            {
                body.localPosition = bodyRestPosition;
                body.localScale = bodyRestScale;
            }

            if (head != null) { head.localRotation = headRest; }
            if (tail != null) { tail.localRotation = tailRest; }

            if (legs == null || legRest == null)
            {
                return;
            }

            for (int i = 0; i < legs.Length && i < legRest.Length; i++)
            {
                if (legs[i] != null)
                {
                    legs[i].localRotation = legRest[i];
                }
            }
        }

        private void Update()
        {
            if (pauseWhenOffScreen && !visible)
            {
                return;
            }

            float dt = Time.deltaTime;
            phase += dt;

            locomotion = Mathf.MoveTowards(
                locomotion, locomotionTarget, dt / Mathf.Max(0.01f, locomotionBlend));
            stridePhase += dt * strideRate * locomotion;

            // ---- breathing: a slight vertical swell, wider than it is tall
            float breath = Mathf.Sin(phase * breathRate * Mathf.PI * 2f);

            if (body != null)
            {
                float bounce = Mathf.Abs(Mathf.Sin(stridePhase * Mathf.PI * 2f)) * walkBob * locomotion;
                body.localPosition = bodyRestPosition + new Vector3(0f, breath * bodyBob + bounce, 0f);
                body.localScale = new Vector3(
                    bodyRestScale.x * (1f + breath * breathScale * 0.6f),
                    bodyRestScale.y * (1f + breath * breathScale),
                    bodyRestScale.z * (1f + breath * breathScale * 0.6f));
            }

            // ---- occasional dip: a chicken pecks, a cow grazes
            float dip = 0f;

            if (dipInterval > 0.01f)
            {
                if (dipElapsed >= 0f)
                {
                    dipElapsed += dt;
                    float t = Mathf.Clamp01(dipElapsed / dipDuration);

                    // Down and back up in one smooth arc.
                    dip = Mathf.Sin(t * Mathf.PI) * dipAngle;

                    if (t >= 1f)
                    {
                        dipElapsed = -1f;
                        dipTimer = dipInterval * (0.6f + Random.value * 0.9f);
                    }
                }
                else
                {
                    dipTimer -= dt;
                    if (dipTimer <= 0f)
                    {
                        dipElapsed = 0f;
                    }
                }
            }

            // ---- head sway and nod
            if (head != null)
            {
                float sway = Mathf.Sin(phase * headSwayRate * Mathf.PI * 2f);
                float nod = Mathf.Sin(phase * headSwayRate * Mathf.PI * 3.1f);

                head.localRotation = headRest * Quaternion.Euler(
                    dip + nod * headNodAngle,
                    sway * headSwayAngle,
                    sway * headNodAngle * 0.4f);
            }

            // ---- tail
            if (tail != null)
            {
                float swish = Mathf.Sin(phase * tailRate * Mathf.PI * 2f);
                tail.localRotation = tailRest * Quaternion.Euler(0f, swish * tailAngle, swish * tailAngle * 0.5f);
            }

            // ---- legs shift weight almost imperceptibly, which stops a standing animal
            // from looking welded to the ground
            if (legs == null || legRest == null)
            {
                return;
            }

            for (int i = 0; i < legs.Length && i < legRest.Length; i++)
            {
                if (legs[i] == null)
                {
                    continue;
                }

                // Weight shift while standing: an animal welded to the ground reads as a prop.
                float offset = Mathf.Sin(phase * 0.37f * Mathf.PI * 2f + i * 1.7f);

                // Stride while walking. Legs 0 and 3 swing together against 1 and 2, which is a
                // diagonal gait on a four-legged animal and a simple alternation on a two-legged one.
                float legPhase = (i == 0 || i == 3) ? 0f : Mathf.PI;
                float stride = Mathf.Sin(stridePhase * Mathf.PI * 2f + legPhase) * legStrideAngle * locomotion;

                legs[i].localRotation = legRest[i] * Quaternion.Euler(offset * 1.8f + stride, 0f, 0f);
            }
        }
    }
}
