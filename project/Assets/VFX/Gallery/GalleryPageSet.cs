using System;
using UnityEngine;

namespace VFXComposer.Gallery
{
    public enum GalleryPageMode { Matrix3x3 = 0, ExplicitList = 1, TierComparison = 2 }

    public enum GalleryAxis { None = 0, Archetype = 1, Element = 2, Style = 3, Tier = 4 }

    /// <summary>
    /// Page data source for the gallery scenes (GALLERY_SPEC.md section 3.3).
    /// Matrix pages declare a row axis and a column axis out of
    /// {archetype, element, style, tier}; the other two dimensions take fixed
    /// values. Missing products render as "not compiled" placeholders — the
    /// gallery is an inspection tool, holes are normal, never an error.
    /// </summary>
    [CreateAssetMenu(menuName = "VFXComposer/Gallery Page Set", fileName = "GalleryPages")]
    public sealed class GalleryPageSet : ScriptableObject
    {
        [Serializable]
        public sealed class GalleryPage
        {
            public string title = "Page";
            public GalleryPageMode mode = GalleryPageMode.Matrix3x3;
            public GalleryAxis rowAxis = GalleryAxis.Archetype;
            public GalleryAxis colAxis = GalleryAxis.Element;
            public string[] rowValues = new string[3];
            public string[] colValues = new string[3];
            public string fixedArchetype = "";
            public string fixedElement = "none";
            public string fixedStyle = "none";
            public string fixedTier = "PM";
            public string[] explicitCells = new string[9];
        }

        [SerializeField] private string dimension = "3d";
        [SerializeField] private string prefabRootPath = "Assets/VFX/Generated/";
        [SerializeField] private GalleryPage[] pages = new GalleryPage[0];

        public string Dimension { get { return dimension; } }
        public string PrefabRootPath { get { return prefabRootPath; } }
        public GalleryPage[] Pages { get { return pages; } }

        /// <summary>GA-5: page axis declarations must be self-consistent.</summary>
        public bool Validate(out string error)
        {
            for (int i = 0; i < pages.Length; i++)
            {
                GalleryPage page = pages[i];
                if (page.mode == GalleryPageMode.Matrix3x3)
                {
                    if (page.rowAxis == page.colAxis)
                    {
                        error = $"page {i} '{page.title}': rowAxis == colAxis";
                        return false;
                    }
                    if (page.rowValues == null || page.rowValues.Length != 3 ||
                        page.colValues == null || page.colValues.Length != 3)
                    {
                        error = $"page {i} '{page.title}': rowValues/colValues must have length 3";
                        return false;
                    }
                }
                else if (page.mode == GalleryPageMode.ExplicitList)
                {
                    if (page.explicitCells == null || page.explicitCells.Length != 9)
                    {
                        error = $"page {i} '{page.title}': explicitCells must have length 9";
                        return false;
                    }
                }
            }
            error = null;
            return true;
        }
    }
}
