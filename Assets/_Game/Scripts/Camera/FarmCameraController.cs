using UnityEngine;

namespace LittleFarmStory.CameraSystem
{
    /// <summary>
    /// Elevated, slightly angled follow camera tuned for portrait mobile farming gameplay.
    /// Deliberately independent of the player: the target is a serialized reference and can be
    /// swapped at runtime for vehicles, building focus or area transitions in later phases.
    /// </summary>
    [DisallowMultipleComponent]
    public class FarmCameraController : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private Transform target;
        [Tooltip("Focus point offset. Raised and pushed forward so the player sits below "
                 + "screen centre, which leaves room to see where you are walking and keeps "
                 + "the character clear of the bottom-screen touch controls.")]
        [SerializeField] private Vector3 targetOffset = new Vector3(0f, 1.7f, 2.6f);

        [Header("Framing")]
        [Tooltip("Shallow enough that buildings keep their silhouette and the treeline reads "
                 + "as a horizon, steep enough that farm plots stay legible from above.")]
        [Range(20f, 80f)] [SerializeField] private float pitch = 46f;
        [Range(-180f, 180f)] [SerializeField] private float yaw = 0f;
        [Tooltip("Distance to the focus point. Paired with a narrow field of view this gives "
                 + "the flatter, near-isometric look of a polished mobile farm game while "
                 + "keeping a whole field on a portrait screen.")]
        [SerializeField] private float distance = 28f;
        [SerializeField] private Vector2 distanceRange = new Vector2(14f, 44f);

        [Header("Follow")]
        [Tooltip("Approximate time in seconds to catch up to the target. 0 = instant.")]
        [SerializeField] private float followSmoothTime = 0.2f;
        [SerializeField] private float zoomSmoothTime = 0.3f;

        [Header("Bounds")]
        [SerializeField] private bool clampToBounds = true;
        [Tooltip("XZ rectangle the camera focus point is kept inside.")]
        [SerializeField] private Rect focusBounds = new Rect(-30f, -30f, 60f, 60f);

        private Vector3 smoothedFocus;
        private Vector3 followVelocity;
        private float smoothedDistance;
        private float distanceVelocity;
        private bool initialised;

        public Transform Target => target;

        public float Distance => smoothedDistance;

        private void Awake()
        {
            smoothedDistance = Mathf.Clamp(distance, distanceRange.x, distanceRange.y);
            distance = smoothedDistance;
        }

        private void OnEnable()
        {
            initialised = false;
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            Vector3 focus = ClampFocus(target.position + targetOffset);

            if (!initialised)
            {
                smoothedFocus = focus;
                followVelocity = Vector3.zero;
                initialised = true;
            }
            else if (followSmoothTime > 0.001f)
            {
                smoothedFocus = Vector3.SmoothDamp(smoothedFocus, focus, ref followVelocity, followSmoothTime);
            }
            else
            {
                smoothedFocus = focus;
            }

            if (zoomSmoothTime > 0.001f)
            {
                smoothedDistance = Mathf.SmoothDamp(smoothedDistance, distance, ref distanceVelocity, zoomSmoothTime);
            }
            else
            {
                smoothedDistance = distance;
            }

            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            transform.SetPositionAndRotation(
                smoothedFocus - rotation * Vector3.forward * smoothedDistance,
                rotation);
        }

        /// <summary>Swap what the camera follows. Used later for vehicles and building focus.</summary>
        public void SetTarget(Transform newTarget, bool snap = false)
        {
            target = newTarget;

            if (snap)
            {
                SnapToTarget();
            }
        }

        public void SetZoom(float newDistance)
        {
            distance = Mathf.Clamp(newDistance, distanceRange.x, distanceRange.y);
        }

        public void SetYaw(float newYaw)
        {
            yaw = Mathf.Repeat(newYaw + 180f, 360f) - 180f;
        }

        /// <summary>Enable or replace the focus bounds, e.g. when the farm is expanded.</summary>
        public void SetBounds(Rect bounds, bool boundsEnabled = true)
        {
            focusBounds = bounds;
            clampToBounds = boundsEnabled;
        }

        public void SnapToTarget()
        {
            initialised = false;
            smoothedDistance = distance;
            distanceVelocity = 0f;
            LateUpdate();
        }

        private Vector3 ClampFocus(Vector3 focus)
        {
            if (!clampToBounds)
            {
                return focus;
            }

            focus.x = Mathf.Clamp(focus.x, focusBounds.xMin, focusBounds.xMax);
            focus.z = Mathf.Clamp(focus.z, focusBounds.yMin, focusBounds.yMax);
            return focus;
        }
    }
}
