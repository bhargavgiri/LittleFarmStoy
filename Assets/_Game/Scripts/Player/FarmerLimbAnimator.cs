using UnityEngine;

namespace LittleFarmStory.Player
{
    /// <summary>
    /// Swings the farmer's limbs from a segmented rig.
    ///
    /// This is procedural rigid-segment animation, not a skinned rig: each limb is its own
    /// mesh rotating about a joint, which is a legitimate technique for chunky stylised
    /// characters and needs no Animator, no clips and no skinning.
    ///
    /// Transform ownership is strictly partitioned so nothing ever fights:
    ///   PlayerController  writes Visual.rotation          (facing)
    ///   PlayerVisualBob   writes BobRoot local pos/scale/rot (body bob)
    ///   this component    writes ONLY the joint local rotations below
    ///
    /// It reads <see cref="PlayerController.NormalisedSpeed"/> and never writes to movement.
    /// When a rigged model with an Animator arrives, delete this component and the joints
    /// with it - nothing else references them.
    /// </summary>
    [DisallowMultipleComponent]
    public class FarmerLimbAnimator : MonoBehaviour
    {
        [Header("Source")]
        [SerializeField] private PlayerController player;

        [Header("Joints")]
        [SerializeField] private Transform legLeft;
        [SerializeField] private Transform legRight;
        [SerializeField] private Transform armLeft;
        [SerializeField] private Transform armRight;
        [SerializeField] private Transform head;

        [Header("Walk")]
        [Tooltip("Full stride cycles per second at top speed.")]
        [SerializeField] private float strideRate = 2.3f;
        [SerializeField] private float legSwing = 34f;
        [SerializeField] private float armSwing = 28f;
        [Tooltip("How far the arms splay outwards while walking.")]
        [SerializeField] private float armSplay = 7f;

        [Header("Idle")]
        [SerializeField] private float idleRate = 0.55f;
        [SerializeField] private float idleArmSway = 3.5f;
        [SerializeField] private float idleHeadSway = 2.5f;

        [Header("Response")]
        [Tooltip("Seconds to blend between the idle and walk poses.")]
        [SerializeField] private float blendTime = 0.16f;

        private float phase;
        private float blendedSpeed;
        private float blendVelocity;

        // Rest poses are captured so the animator is always additive and can restore exactly.
        private Quaternion legLeftRest;
        private Quaternion legRightRest;
        private Quaternion armLeftRest;
        private Quaternion armRightRest;
        private Quaternion headRest;

        private void Awake()
        {
            if (player == null)
            {
                player = GetComponentInParent<PlayerController>();
            }

            CaptureRestPose();
        }

        private void CaptureRestPose()
        {
            if (legLeft != null) { legLeftRest = legLeft.localRotation; }
            if (legRight != null) { legRightRest = legRight.localRotation; }
            if (armLeft != null) { armLeftRest = armLeft.localRotation; }
            if (armRight != null) { armRightRest = armRight.localRotation; }
            if (head != null) { headRest = head.localRotation; }
        }

        private void OnDisable()
        {
            // Return to the authored pose so a disabled animator leaves no trace.
            if (legLeft != null) { legLeft.localRotation = legLeftRest; }
            if (legRight != null) { legRight.localRotation = legRightRest; }
            if (armLeft != null) { armLeft.localRotation = armLeftRest; }
            if (armRight != null) { armRight.localRotation = armRightRest; }
            if (head != null) { head.localRotation = headRest; }
        }

        private void LateUpdate()
        {
            float target = player != null ? Mathf.Clamp01(player.NormalisedSpeed) : 0f;
            blendedSpeed = Mathf.SmoothDamp(blendedSpeed, target, ref blendVelocity, blendTime);

            float rate = Mathf.Lerp(idleRate, strideRate, blendedSpeed);
            phase += Time.deltaTime * rate * Mathf.PI * 2f;

            if (phase > Mathf.PI * 2f)
            {
                phase -= Mathf.PI * 2f;
            }

            float swing = Mathf.Sin(phase);

            // Legs swing in counterphase; arms oppose the legs, which is what makes a walk
            // read as a walk rather than a shuffle.
            float legAngle = swing * legSwing * blendedSpeed;
            float armAngle = -swing * armSwing * blendedSpeed;

            // Idle keeps a little life in the arms and head so a standing farmer is not frozen.
            float idle = Mathf.Sin(phase) * (1f - blendedSpeed);
            float splay = armSplay * blendedSpeed;

            ApplyLocal(legLeft, legLeftRest, Quaternion.Euler(legAngle, 0f, 0f));
            ApplyLocal(legRight, legRightRest, Quaternion.Euler(-legAngle, 0f, 0f));

            ApplyLocal(armLeft, armLeftRest, Quaternion.Euler(armAngle, 0f, -splay - idle * idleArmSway));
            ApplyLocal(armRight, armRightRest, Quaternion.Euler(-armAngle, 0f, splay + idle * idleArmSway));

            // The head leads the walk slightly and drifts gently when idle.
            ApplyLocal(head, headRest, Quaternion.Euler(
                -2.5f * blendedSpeed,
                idle * idleHeadSway,
                swing * 2f * blendedSpeed));
        }

        private static void ApplyLocal(Transform joint, Quaternion rest, Quaternion offset)
        {
            if (joint != null)
            {
                joint.localRotation = rest * offset;
            }
        }
    }
}
