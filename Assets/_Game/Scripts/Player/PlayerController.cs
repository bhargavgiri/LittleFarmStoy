using LittleFarmStory.Input;
using UnityEngine;

namespace LittleFarmStory.Player
{
    /// <summary>
    /// Camera-relative, acceleration-based movement for the farmer.
    /// Knows nothing about joysticks or the camera controller - only an input provider
    /// and a reference transform used to orient input.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerInputProvider input;
        [Tooltip("Transform used to make input camera-relative. Usually the gameplay camera.")]
        [SerializeField] private Transform inputSpace;
        [Tooltip("Optional child holding the visual mesh. Falls back to this transform.")]
        [SerializeField] private Transform visualRoot;

        [Header("Movement")]
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float acceleration = 34f;
        [SerializeField] private float deceleration = 42f;
        [SerializeField] private float turnSpeedDegrees = 720f;

        [Header("Gravity")]
        [SerializeField] private float gravity = -22f;
        [SerializeField] private float groundedStick = -2f;

        private CharacterController controller;
        private Vector3 planarVelocity;
        private float verticalVelocity;

        /// <summary>Current horizontal speed in units/second. Useful for animation later.</summary>
        public float CurrentSpeed => planarVelocity.magnitude;

        /// <summary>Normalised speed 0..1, for blend trees in a later phase.</summary>
        public float NormalisedSpeed => moveSpeed > 0.001f ? Mathf.Clamp01(CurrentSpeed / moveSpeed) : 0f;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();

            if (visualRoot == null)
            {
                visualRoot = transform;
            }

            if (input == null)
            {
                Debug.LogWarning(nameof(PlayerController) + " on " + name + " has no PlayerInputProvider assigned; the player will not move.", this);
            }
        }

        public void SetInputSpace(Transform space)
        {
            inputSpace = space;
        }

        /// <summary>Teleport helper for future area transitions and save loading.</summary>
        public void Teleport(Vector3 position, Quaternion rotation)
        {
            controller.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            controller.enabled = true;

            planarVelocity = Vector3.zero;
            verticalVelocity = groundedStick;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            Vector3 desired = ResolveDesiredVelocity();

            float rate = desired.sqrMagnitude > 0.0001f ? acceleration : deceleration;
            planarVelocity = Vector3.MoveTowards(planarVelocity, desired, rate * dt);

            ApplyGravity(dt);
            controller.Move((planarVelocity + Vector3.up * verticalVelocity) * dt);

            FaceMovementDirection(dt);
        }

        private Vector3 ResolveDesiredVelocity()
        {
            if (input == null)
            {
                return Vector3.zero;
            }

            Vector2 raw = input.Move;
            if (raw.sqrMagnitude < 0.0001f)
            {
                return Vector3.zero;
            }

            Vector3 forward = Vector3.forward;
            Vector3 right = Vector3.right;

            if (inputSpace != null)
            {
                forward = Vector3.ProjectOnPlane(inputSpace.forward, Vector3.up);
                if (forward.sqrMagnitude < 0.0001f)
                {
                    // Camera is looking almost straight down; use its up vector instead.
                    forward = Vector3.ProjectOnPlane(inputSpace.up, Vector3.up);
                }

                forward = forward.sqrMagnitude < 0.0001f ? Vector3.forward : forward.normalized;
                right = Vector3.Cross(Vector3.up, forward);
            }

            Vector3 direction = right * raw.x + forward * raw.y;
            if (direction.sqrMagnitude > 1f)
            {
                direction.Normalize();
            }

            return direction * moveSpeed;
        }

        private void ApplyGravity(float dt)
        {
            if (controller.isGrounded && verticalVelocity <= 0f)
            {
                verticalVelocity = groundedStick;
                return;
            }

            verticalVelocity += gravity * dt;
        }

        private void FaceMovementDirection(float dt)
        {
            if (planarVelocity.sqrMagnitude < 0.02f)
            {
                return;
            }

            Quaternion target = Quaternion.LookRotation(planarVelocity.normalized, Vector3.up);
            visualRoot.rotation = Quaternion.RotateTowards(visualRoot.rotation, target, turnSpeedDegrees * dt);
        }
    }
}
