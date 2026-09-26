using LittleFarmStory.Animals;
using LittleFarmStory.Farming;
using UnityEngine;

namespace LittleFarmStory.UI
{
    /// <summary>
    /// A small floating badge over a plot or an animal that has something waiting.
    ///
    /// Without it the player has to walk to every plot to find out which one ripened. With it
    /// the farm itself tells them where to go, which is the difference between a readable game
    /// and a screen full of identical brown squares.
    ///
    /// Kept deliberately quiet: one small billboard, a slow bob, and nothing at all when there
    /// is nothing to collect. It self-wires from whichever gameplay component sits above it, so
    /// dropping it into a prefab is the entire integration.
    ///
    /// Strictly a listener - it reads state and never writes any.
    /// </summary>
    [DisallowMultipleComponent]
    public class ReadyMarker : MonoBehaviour
    {
        [Header("Visual")]
        [Tooltip("The badge itself. Hidden whenever nothing is ready.")]
        [SerializeField] private SpriteRenderer badge;

        [Header("Motion")]
        [SerializeField] private float bobHeight = 0.12f;
        [SerializeField] private float bobRate = 0.85f;
        [Tooltip("Face the camera. Off for a marker that should keep its authored rotation.")]
        [SerializeField] private bool billboard = true;

        private FarmPlot plot;
        private AnimalController animal;

        private Transform cameraTransform;
        private Vector3 restPosition;
        private float phase;
        private bool shown;

        private void Awake()
        {
            restPosition = transform.localPosition;

            // A per-instance offset stops a whole field of ready plots bobbing in lockstep.
            phase = Random.value * Mathf.PI * 2f;

            plot = GetComponentInParent<FarmPlot>();

            if (plot == null)
            {
                animal = GetComponentInParent<AnimalController>();
            }

            Show(false);
        }

        private void OnEnable()
        {
            if (plot != null)
            {
                plot.StateChanged += OnPlotChanged;
                OnPlotChanged(plot);
            }
            else if (animal != null)
            {
                animal.StateChanged += OnAnimalChanged;
                OnAnimalChanged(animal);
            }
        }

        private void OnDisable()
        {
            if (plot != null)
            {
                plot.StateChanged -= OnPlotChanged;
            }

            if (animal != null)
            {
                animal.StateChanged -= OnAnimalChanged;
            }
        }

        private void OnPlotChanged(FarmPlot changed)
        {
            Show(changed != null && changed.State == PlotState.ReadyToHarvest);
        }

        private void OnAnimalChanged(AnimalController changed)
        {
            Show(changed != null && changed.HasProductReady);
        }

        private void Show(bool visible)
        {
            shown = visible;

            if (badge != null && badge.gameObject.activeSelf != visible)
            {
                badge.gameObject.SetActive(visible);
            }
        }

        private void Update()
        {
            // Nothing waiting means no work at all - the common case across a whole farm.
            if (!shown)
            {
                return;
            }

            phase += Time.deltaTime * bobRate;
            transform.localPosition = restPosition + new Vector3(0f, Mathf.Sin(phase * Mathf.PI * 2f) * bobHeight, 0f);

            if (!billboard)
            {
                return;
            }

            if (cameraTransform == null)
            {
                Camera main = Camera.main;

                if (main == null)
                {
                    return;
                }

                cameraTransform = main.transform;
            }

            // Yaw and pitch to face the camera, which reads correctly under the tilted
            // near-isometric view without the badge ever appearing edge-on.
            transform.rotation = Quaternion.LookRotation(
                transform.position - cameraTransform.position, Vector3.up);
        }

#if UNITY_EDITOR
        public void EditorConfigure(SpriteRenderer markerBadge, float height, float rate)
        {
            badge = markerBadge;
            bobHeight = height;
            bobRate = rate;
        }
#endif
    }
}
