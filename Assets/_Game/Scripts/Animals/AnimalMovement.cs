using UnityEngine;

namespace LittleFarmStory.Animals
{
    /// <summary>
    /// Walks an animal towards a point. That is the entire scope.
    ///
    /// No Rigidbody, no CharacterController, no NavMeshAgent, no raycasts: a MoveTowards and a
    /// RotateTowards, which is all a fenced pen needs and is what keeps twenty animals free on
    /// a mid-range phone. Whether a destination is legal is decided once, by
    /// <see cref="AnimalHabitat.TrySampleDestination"/>, not re-checked every frame.
    ///
    /// Transform ownership: this class writes the animal ROOT's position and its Y rotation.
    /// <see cref="AnimalIdleAnimator"/> writes only child joint locals. The two never overlap.
    /// </summary>
    public class AnimalMovement
    {
        private readonly Transform root;
        private readonly AnimalDefinition definition;

        private Vector3 destination;
        private float travelTimeout;

        private const float ArriveDistance = 0.12f;

        /// <summary>Longest a single walk may take before the animal gives up and idles.</summary>
        private const float MaxTravelSeconds = 12f;

        public AnimalMovement(Transform root, AnimalDefinition definition)
        {
            this.root = root;
            this.definition = definition;
            destination = root != null ? root.position : Vector3.zero;
        }

        public bool HasDestination { get; private set; }

        /// <summary>0 while standing, 1 while walking. Drives the walk cycle in the animator.</summary>
        public float NormalisedSpeed { get; private set; }

        public Vector3 Destination => destination;

        public void SetDestination(Vector3 worldPoint)
        {
            if (root == null)
            {
                return;
            }

            // Keep the animal on its own ground plane; the habitat decides the XZ, not the Y.
            destination = new Vector3(worldPoint.x, root.position.y, worldPoint.z);
            HasDestination = true;
            travelTimeout = MaxTravelSeconds;
        }

        public void Stop()
        {
            HasDestination = false;
            NormalisedSpeed = 0f;
        }

        /// <summary>
        /// Per-frame movement. Returns true on the frame the animal arrives or gives up, so the
        /// controller can switch back to idle without polling a distance itself.
        /// </summary>
        public bool Tick(float deltaSeconds)
        {
            if (root == null || definition == null || !HasDestination || deltaSeconds <= 0f)
            {
                NormalisedSpeed = Mathf.MoveTowards(NormalisedSpeed, 0f, deltaSeconds * 4f);
                return false;
            }

            travelTimeout -= deltaSeconds;

            Vector3 position = root.position;
            Vector3 offset = destination - position;
            offset.y = 0f;

            if (offset.sqrMagnitude <= ArriveDistance * ArriveDistance || travelTimeout <= 0f)
            {
                Stop();
                return true;
            }

            Vector3 direction = offset.normalized;

            // Turn first, then walk along the facing: an animal that strafes sideways towards a
            // point reads as sliding scenery rather than something alive.
            Quaternion desired = Quaternion.LookRotation(direction, Vector3.up);
            root.rotation = Quaternion.RotateTowards(
                root.rotation, desired, definition.TurnSpeed * deltaSeconds);

            float alignment = Mathf.Clamp01(Vector3.Dot(root.forward, direction));
            float step = definition.MoveSpeed * alignment * deltaSeconds;

            root.position = Vector3.MoveTowards(position, destination, step);

            NormalisedSpeed = Mathf.MoveTowards(NormalisedSpeed, alignment, deltaSeconds * 3f);
            return false;
        }
    }
}
