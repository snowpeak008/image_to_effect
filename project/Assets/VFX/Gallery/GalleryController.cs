using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using VFXComposer.TechniqueFamilies;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace VFXComposer.Gallery
{
    /// <summary>
    /// Gallery controller (GALLERY_SPEC.md sections 2-5). Play-mode inspection
    /// tool: keyboard + UI + optional auto-advance paging over a 3x3 cell grid,
    /// per-cell replay, Bloom toggle on the scene Volume, structure-inspection
    /// mode. Scene tooling only — Assets/VFX/Gallery/** never enters the
    /// product dependency allow-list (GA-9) and this assembly is not referenced
    /// by VFXComposer.Runtime (GA-10). Product loading uses AssetDatabase and
    /// is editor-only (GA-8: the gallery never ships in player builds).
    /// </summary>
    public sealed class GalleryController : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private GalleryPageSet pageSet;

        [Header("Scene wiring")]
        [SerializeField] private Transform[] cellAnchors = new Transform[9];
        [SerializeField] private Volume postVolume;
        [SerializeField] private Canvas galleryUi;
        [SerializeField] private GameObject gridLines;
        [SerializeField] private Light inspectionLight;

        [Header("Behaviour")]
        [SerializeField, Min(1f)] private float autoAdvanceSeconds = 6f;
        [SerializeField] private bool is2D;

        private readonly List<GameObject> liveInstances = new List<GameObject>();
        private Bloom bloom;
        private int pageIndex;
        private bool autoAdvance;
        private bool structureMode;
        private float autoTimer;

        public int PageIndex { get { return pageIndex; } }
        public bool BloomEnabled { get { return bloom != null && bloom.active; } }
        public bool StructureMode { get { return structureMode; } }
        public GalleryPageSet PageSet { get { return pageSet; } }
        public Transform[] CellAnchors { get { return cellAnchors; } }

        private void Awake()
        {
            if (postVolume != null && postVolume.profile != null)
                postVolume.profile.TryGet(out bloom);
        }

        private void Start()
        {
            LoadPage(0);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.RightArrow)) NextPage();
            if (Input.GetKeyDown(KeyCode.LeftArrow)) PreviousPage();
            if (Input.GetKeyDown(KeyCode.Space)) ReplayAll();
            if (Input.GetKeyDown(KeyCode.Alpha0)) StopAll();
            for (int i = 0; i < 9; i++)
                if (Input.GetKeyDown(KeyCode.Alpha1 + i)) ReplayCell(i);
            if (Input.GetKeyDown(KeyCode.B)) ToggleBloom();
            if (Input.GetKeyDown(KeyCode.L)) autoAdvance = !autoAdvance;
            if (Input.GetKeyDown(KeyCode.S)) ToggleStructureMode();
            if (Input.GetKeyDown(KeyCode.H) && galleryUi != null) galleryUi.enabled = !galleryUi.enabled;
            if (Input.GetKeyDown(KeyCode.P)) CaptureInteractiveShot();

            if (autoAdvance)
            {
                autoTimer += Time.deltaTime;
                if (autoTimer >= autoAdvanceSeconds)
                {
                    autoTimer = 0f;
                    NextPage();
                    ReplayAll();
                }
            }
        }

        public void NextPage()
        {
            int count = pageSet != null ? Mathf.Max(pageSet.Pages.Length, 1) : 1;
            LoadPage((pageIndex + 1) % count);
        }

        public void PreviousPage()
        {
            int count = pageSet != null ? Mathf.Max(pageSet.Pages.Length, 1) : 1;
            LoadPage((pageIndex - 1 + count) % count);
        }

        public void ToggleBloom()
        {
            if (bloom != null) bloom.active = !bloom.active;
        }

        public void ToggleStructureMode()
        {
            structureMode = !structureMode;
            if (inspectionLight != null) inspectionLight.enabled = structureMode;
            if (structureMode && bloom != null) bloom.active = false;
            RenderSettings.ambientLight = structureMode
                ? new Color(0.35f, 0.35f, 0.38f)
                : new Color(0.06f, 0.065f, 0.08f);
        }

        public void ReplayAll()
        {
            for (int i = 0; i < liveInstances.Count; i++) ReplayInstance(liveInstances[i]);
        }

        public void ReplayCell(int index)
        {
            if (index >= 0 && index < liveInstances.Count) ReplayInstance(liveInstances[index]);
        }

        public void StopAll()
        {
            foreach (GameObject instance in liveInstances)
            {
                if (instance == null) continue;
                var controller = instance.GetComponent<VfxController>();
                if (controller != null) controller.SendEvent("end", default);
            }
        }

        private static void ReplayInstance(GameObject instance)
        {
            if (instance == null) return;
            var controller = instance.GetComponent<VfxController>();
            if (controller == null) return;
            controller.ResetForPool();
            controller.SendEvent("launch", new VfxEventPayload { Position = instance.transform.position });
        }

        public void LoadPage(int index)
        {
            pageIndex = index;
            foreach (GameObject instance in liveInstances)
                if (instance != null) Destroy(instance);
            liveInstances.Clear();

#if UNITY_EDITOR
            if (pageSet == null || pageSet.Pages.Length == 0) return;
            GalleryPageSet.GalleryPage page = pageSet.Pages[Mathf.Clamp(index, 0, pageSet.Pages.Length - 1)];
            for (int cell = 0; cell < 9 && cell < cellAnchors.Length; cell++)
            {
                if (cellAnchors[cell] == null) continue;
                string prefabPath = ResolveCellPrefabPath(page, cell);
                if (string.IsNullOrEmpty(prefabPath)) continue;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab == null) continue; // "not compiled" placeholder: holes are normal
                GameObject instance = Instantiate(prefab, cellAnchors[cell]);
                instance.transform.localPosition = Vector3.zero;
                if (is2D) ApplySortingIsolation(instance, cell);
                liveInstances.Add(instance);
            }
#endif
        }

        /// <summary>2D cell sorting isolation: +100 * cellOrdinal (GALLERY_SPEC section 2.3).</summary>
        public static void ApplySortingIsolation(GameObject instance, int cellOrdinal)
        {
            var renderers = instance.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer r in renderers)
                r.sortingOrder += cellOrdinal * 100;
        }

        public string ResolveCellPrefabPath(GalleryPageSet.GalleryPage page, int cell)
        {
            if (page.mode == GalleryPageMode.ExplicitList)
                return cell < page.explicitCells.Length ? page.explicitCells[cell] : null;

            int row = cell / 3;
            int col = cell % 3;
            string archetype = page.fixedArchetype;
            string element = page.fixedElement;
            string style = page.fixedStyle;
            string tier = page.fixedTier;

            if (page.mode == GalleryPageMode.TierComparison)
            {
                string[] tiers = { "ML", "MM", "MH", "PL", "PM", "PH" };
                if (cell >= tiers.Length) return null;
                tier = tiers[cell];
            }
            else
            {
                Assign(page.rowAxis, page.rowValues[row], ref archetype, ref element, ref style, ref tier);
                Assign(page.colAxis, page.colValues[col], ref archetype, ref element, ref style, ref tier);
            }
            if (string.IsNullOrEmpty(archetype)) return null;
            string recipeId = $"{archetype}_{element}_{style}";
            return $"{pageSet.PrefabRootPath}{recipeId}/{recipeId}__{tier}.prefab";
        }

        private static void Assign(GalleryAxis axis, string value, ref string archetype, ref string element, ref string style, ref string tier)
        {
            switch (axis)
            {
                case GalleryAxis.Archetype: archetype = value; break;
                case GalleryAxis.Element: element = value; break;
                case GalleryAxis.Style: style = value; break;
                case GalleryAxis.Tier: tier = value; break;
            }
        }

        /// <summary>Interactive lightweight shot (GA-7: never writes the evidence directory).</summary>
        private void CaptureInteractiveShot()
        {
            string dir = System.IO.Path.Combine(System.IO.Directory.GetParent(Application.dataPath).Parent.FullName, "test-results", "gallery-shots");
            System.IO.Directory.CreateDirectory(dir);
            string file = System.IO.Path.Combine(dir, $"{gameObject.scene.name}_{pageIndex}_{System.DateTime.Now:yyyyMMdd-HHmmss}.png");
            ScreenCapture.CaptureScreenshot(file);
        }
    }
}
