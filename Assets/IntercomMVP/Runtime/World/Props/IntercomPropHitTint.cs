using UnityEngine;

namespace IntercomMvp
{
    /// <summary>Captures visual renderers once. Reuses property blocks and restores each authored override after the flash.</summary>
    internal sealed class IntercomPropHitTint
    {
        private Renderer[] renderers;
        private MaterialPropertyBlock[] originals, working;
        private Color[] baseColors;
        private bool applied;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        internal void Capture(Transform visual, Renderer excluded)
        {
            renderers = visual != null ? visual.GetComponentsInChildren<Renderer>(true) : new Renderer[0];
            originals = new MaterialPropertyBlock[renderers.Length];
            working = new MaterialPropertyBlock[renderers.Length];
            baseColors = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null || renderer == excluded) { renderers[i] = null; continue; }
                originals[i] = new MaterialPropertyBlock();
                working[i] = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(originals[i]);
                Material material = renderer.sharedMaterial;
                baseColors[i] = originals[i].HasColor(BaseColorId) ? originals[i].GetColor(BaseColorId)
                    : originals[i].HasColor(ColorId) ? originals[i].GetColor(ColorId)
                    : material != null && material.HasProperty(BaseColorId) ? material.GetColor(BaseColorId)
                    : material != null && material.HasProperty(ColorId) ? material.GetColor(ColorId)
                    : new Color(.72f, .69f, .59f, 1f);
            }
        }
        internal void Apply(float strength, Color tint)
        {
            if (renderers == null) return;
            if (strength <= 0f) { Reset(); return; }
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (renderer == null) continue;
                renderer.GetPropertyBlock(working[i]);
                Color color = Color.Lerp(baseColors[i], tint, strength); color.a = baseColors[i].a;
                working[i].SetColor(BaseColorId, color); working[i].SetColor(ColorId, color);
                renderer.SetPropertyBlock(working[i]);
            }
            applied = true;
        }
        internal void Reset()
        {
            if (!applied || renderers == null) return;
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null) renderers[i].SetPropertyBlock(originals[i].isEmpty ? null : originals[i]);
            applied = false;
        }
    }
}
