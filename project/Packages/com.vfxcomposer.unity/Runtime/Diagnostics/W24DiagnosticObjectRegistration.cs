using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VFXComposer.W24
{
    /// <summary>
    /// Explicit, authored membership in a W24 object-ID diagnostic pass.  Runtime rendering never
    /// infers IDs from names, hierarchy paths, materials, layers, or Beauty pixels.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class W24DiagnosticObjectRegistration : MonoBehaviour
    {
        [SerializeField] private Renderer targetRenderer;
        [SerializeField, Min(1)] private uint objectId = 1;
        [SerializeField] private string semanticRole;
        [SerializeField] private bool required = true;

        public Renderer TargetRenderer { get { return targetRenderer; } }
        public uint ObjectId { get { return objectId; } }
        public string SemanticRole { get { return semanticRole; } }
        public bool Required { get { return required; } }

        /// <summary>Authoring/test seam. Formal production data is still serialized on the component.</summary>
        public void Configure(Renderer renderer, uint id, string role, bool isRequired)
        {
            targetRenderer = renderer;
            objectId = id;
            semanticRole = role;
            required = isRequired;
        }

        public static IReadOnlyList<W24DiagnosticObjectRegistration> Validate(IEnumerable<W24DiagnosticObjectRegistration> values)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            var registrations = values.ToArray();
            if (registrations.Any(value => value == null)) throw new InvalidOperationException("W24 object-ID diagnostics reject null registration entries; membership may not be silently filtered.");
            if (registrations.Length == 0) throw new InvalidOperationException("W24 object-ID diagnostics require at least one explicit registration.");
            var ids = new HashSet<uint>();
            foreach (var item in registrations)
            {
                if (item.targetRenderer == null) throw new InvalidOperationException("W24 object-ID registration has no target Renderer: " + item.name);
                if (item.objectId == 0 || item.objectId > Int32.MaxValue) throw new InvalidOperationException("W24 object-ID must be in [1, Int32.MaxValue]: " + item.name);
                if (!ids.Add(item.objectId)) throw new InvalidOperationException("W24 object-ID registrations must be unique: " + item.objectId);
                if (string.IsNullOrWhiteSpace(item.semanticRole)) throw new InvalidOperationException("W24 object-ID registration requires a semantic role: " + item.name);
                if (item.required && (!item.targetRenderer.enabled || !item.targetRenderer.gameObject.activeInHierarchy))
                    throw new InvalidOperationException("W24 required object-ID Renderer must be enabled and active: " + item.name);
                if (!(item.targetRenderer is MeshRenderer) && !(item.targetRenderer is SkinnedMeshRenderer))
                    throw new NotSupportedException("W24 P0 object-ID diagnostics support MeshRenderer and SkinnedMeshRenderer only: " + item.targetRenderer.GetType().Name);
            }
            return registrations;
        }
    }
}
