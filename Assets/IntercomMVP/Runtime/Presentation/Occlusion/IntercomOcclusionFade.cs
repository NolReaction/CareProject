using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace IntercomMvp
{
    // Per-camera leases keep shared renderers faded until the final camera
    // releases them. Source assets, colliders and visibility flags are untouched.
    internal static class IntercomOcclusionFade
    {
        private sealed class Lease
        {
            internal Material[] original, faded;
            internal Color[] colors;
            internal bool changedMaterials;
            internal readonly Dictionary<int, float> owners = new Dictionary<int, float>();
            internal readonly HashSet<int> retainers = new HashSet<int>();
            internal Shader shader;
            internal float lastOpacity = float.NaN;
        }
        private static readonly Dictionary<Renderer, Lease> Leases = new Dictionary<Renderer, Lease>();
        private static readonly List<Renderer> Released = new List<Renderer>();
        internal static uint Epoch { get; private set; }

        internal static void Set(Renderer renderer, int owner, float opacity, Material variantTemplate, bool retainForCamera = false)
        {
            if (renderer == null) return;
            if (variantTemplate == null) { Release(renderer, owner); return; }
            bool existing = Leases.TryGetValue(renderer, out Lease lease);
            if (existing && lease.owners.Count == 0)
            {
                // While solid, an external owner may replace a slot or add an
                // MPB. Do not reinstall a stale cached appearance over it.
                Material[] current = renderer.sharedMaterials;
                bool compatible = lease.shader == variantTemplate.shader && !renderer.HasPropertyBlock()
                    && current.Length == lease.original.Length;
                for (int i = 0; compatible && i < current.Length; i++)
                {
                    compatible = current[i] == lease.original[i];
                    if (lease.faded[i] != lease.original[i])
                        compatible &= lease.original[i] != null && lease.original[i].shader == variantTemplate.shader;
                }
                if (!compatible)
                {
                    Dispose(lease); Leases.Remove(renderer); existing = false;
                }
                else if (lease.changedMaterials)
                {
                    for (int i = 0; i < lease.faded.Length; i++)
                    {
                        Material original = lease.original[i], faded = lease.faded[i];
                        if (original == null || faded == null || original == faded) continue;
                        faded.CopyPropertiesFromMaterial(original);
                        ConfigureTransparent(faded);
                        lease.colors[i] = original.GetColor("_BaseColor");
                    }
                    renderer.sharedMaterials = lease.faded;
                    lease.lastOpacity = float.NaN;
                }
            }
            if (!existing)
            {
                // Custom MPB colour animation owns its material appearance.
                // Such renderers remain visible until given a compatible fade.
                if (renderer.HasPropertyBlock()) return;
                Material[] originals = renderer.sharedMaterials;
                lease = new Lease { original = originals, faded = new Material[originals.Length], colors = new Color[originals.Length], shader = variantTemplate.shader };
                bool supported = false;
                for (int i = 0; i < originals.Length; i++)
                {
                    Material original = originals[i];
                    lease.faded[i] = original;
                    // A serialized template keeps this shader's transparent
                    // variant in a player build. Unsupported shaders stay solid.
                    if (original == null || original.shader != variantTemplate.shader || !original.HasProperty("_BaseColor")) continue;
                    Material clone = new Material(original) { name = original.name + " (Camera Fade)", hideFlags = HideFlags.HideAndDontSave };
                    ConfigureTransparent(clone);
                    lease.colors[i] = original.GetColor("_BaseColor");
                    lease.faded[i] = clone;
                    supported = true;
                }
                lease.changedMaterials = supported;
                Leases.Add(renderer, lease);
                if (supported) renderer.sharedMaterials = lease.faded;
            }
            if (retainForCamera) lease.retainers.Add(owner);
            lease.owners[owner] = Mathf.Clamp01(opacity);
            Apply(lease);
        }

        internal static void ConfigureTransparent(Material material)
        {
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_AlphaClip", 0f);
            material.DisableKeyword("_ALPHATEST_ON");
            material.SetFloat("_BlendModePreserveSpecular", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.DisableKeyword("_ALPHAMODULATE_ON");
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;
        }

        private static void Apply(Lease lease)
        {
            float opacity = 1f;
            foreach (float ownerOpacity in lease.owners.Values) opacity = Mathf.Min(opacity, ownerOpacity);
            if (lease.lastOpacity == opacity) return;
            lease.lastOpacity = opacity;
            for (int i = 0; i < lease.faded.Length; i++)
            {
                Material material = lease.faded[i];
                if (material == null || material == lease.original[i]) continue;
                Color color = lease.colors[i];
                color.a *= opacity;
                material.SetColor("_BaseColor", color);
                if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            }
        }

        internal static void Release(Renderer renderer, int owner)
        {
            if (ReferenceEquals(renderer, null) || !Leases.TryGetValue(renderer, out Lease lease)) return;
            if (!lease.owners.Remove(owner) && renderer != null) return;
            if (lease.owners.Count > 0 && renderer != null) { Apply(lease); return; }
            Restore(renderer, lease);
            if (renderer != null && lease.retainers.Count > 0) return;
            Dispose(lease);
            Leases.Remove(renderer);
        }

        /// <summary>Bound cached variants to the owning camera's current scenery, not every renderer ever seen.</summary>
        internal static void ReleaseUnused(int owner, HashSet<Renderer> keep)
        {
            Released.Clear();
            foreach (KeyValuePair<Renderer, Lease> pair in Leases)
            {
                if (pair.Key != null && keep != null && keep.Contains(pair.Key)) continue;
                Lease lease = pair.Value;
                bool used = lease.owners.Remove(owner);
                used |= lease.retainers.Remove(owner);
                if (!used && pair.Key != null) continue;
                if (pair.Key != null && lease.owners.Count > 0) { Apply(lease); continue; }
                Restore(pair.Key, lease);
                if (pair.Key != null && lease.retainers.Count > 0) continue;
                Dispose(lease); Released.Add(pair.Key);
            }
            for (int i = 0; i < Released.Count; i++) Leases.Remove(Released[i]);
            Released.Clear();
        }

        private static void Restore(Renderer renderer, Lease lease)
        {
            if (renderer != null && lease.changedMaterials)
            {
                Material[] current = renderer.sharedMaterials;
                for (int i = 0; i < current.Length && i < lease.faded.Length; i++)
                    if (current[i] == lease.faded[i]) current[i] = lease.original[i];
                renderer.sharedMaterials = current;
            }
        }

        private static void Dispose(Lease lease)
        {
            for (int i = 0; i < lease.faded.Length; i++)
                if (lease.faded[i] != null && lease.faded[i] != lease.original[i])
                    Object.Destroy(lease.faded[i]);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            foreach (KeyValuePair<Renderer, Lease> pair in Leases)
            { Restore(pair.Key, pair.Value); Dispose(pair.Value); }
            Leases.Clear();
            Released.Clear();
            unchecked { Epoch++; }
        }
    }
}
