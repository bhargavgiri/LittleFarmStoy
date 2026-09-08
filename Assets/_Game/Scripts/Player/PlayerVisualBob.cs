using UnityEngine;

namespace LittleFarmStory.Player
{
    /// <summary>
    /// Lightweight procedural life for the farmer: a slow breathing rise when idle, and a
    /// stride bob with a little lean when walking.
    ///
    /// This is deliberately not an animation system. It reads
    /// <see cref="PlayerController.NormalisedSpeed"/> and writes only to its own transform,
    /// so it cannot affect movement, and it sits on a child of the transform the controller
    /// rotates, so the two never fight over the same property.
    ///
    /// When a rigged model with an Animator arrives, delete this component - nothing depends on it.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerVisualBob : MonoBehaviour
    {
        [Header("Source")]
        [SerializeField] private PlayerController player;

        [Header("Idle")]
        [Tooltip("Breaths per second while standing still.")]
        [SerializeField] private float idleRate = 0.55f;
        [SerializeField] private float idleRise = 0.012f;
        [SerializeField] private float idleSquash = 0.014f;

        [Header("Walk")]
        [Tooltip("Full stride cycles per second at top speed.")]
        [SerializeField] private float strideRate = 2.3f;
        [SerializeField] private float strideBob = 0.055f;
        [SerializeField] private float strideRoll = 4.5f;
        [SerializeField] private float leanAngle = 6.5f;

        [Header("Response")]
        [Tooltip("Seconds to blend between the idle and walk poses.")]
        [SerializeField] private float blendTime = 0.16f;

        private Vector3 basePosition;
        private Vector3 baseScale;
        private float phase;
        private float blendedSpeed;
        private float blendVelocity;

        private void Awake()
        {
            basePosition = transform.localPosition;
            baseScale = transform.localScale;

            if (player == null)
            {
                player = GetComponentInParent<PlayerController>();
            }
        }

        private void OnDisable()
        {
            // Leave the visual exactly where it started so a disabled bob is invisible.
            transform.localPosition = basePosition;
            transform.localScale = baseScale;
            transform.localRotation = Quaternion.identity;
        }

        private void LateUpdate()
        {
            float target = player != null ? Mathf.Clamp01(player.NormalisedSpeed) : 0f;
            blendedSpeed = Mathf.SmoothDamp(blendedSpeed, target, ref blendVelocity, blendTime);

            float dt = Time.deltaTime;

            // One phase drives both poses, running faster the quicker the farmer moves.
            float rate = Mathf.Lerp(idleRate, strideRate, blendedSpeed);
            phase += dt * rate * Mathf.PI * 2f;

            if (phase > Mathf.PI * 2f)
            {
                phase -= Mathf.PI * 2f;
            }

            float wave = Mathf.Sin(phase);

            // Idle: gentle vertical breath with a matching squash so it reads as volume.
            float idleOffset = wave * idleRise * (1f - blendedSpeed);
            float idleScale = wave * idleSquash * (1f - blendedSpeed);

            // Walk: the body rises twice per stride, hence the doubled phase.
            float walkOffset = Mathf.Abs(Mathf.Sin(phase)) * strideBob * blendedSpeed;
            float roll = Mathf.Sin(phase * 0.5f) * strideRoll * blendedSpeed;

            transform.localPosition = basePosition + new Vector3(0f, idleOffset + walkOffset, 0f);

            transform.localScale = new Vector3(
                baseScale.x * (1f - idleScale * 0.5f),
                baseScale.y * (1f + idleScale),
                baseScale.z * (1f - idleScale * 0.5f));

            transform.localRotation = Quaternion.Euler(
                leanAngle * blendedSpeed,
                0f,
                roll);
        }
    }
}
