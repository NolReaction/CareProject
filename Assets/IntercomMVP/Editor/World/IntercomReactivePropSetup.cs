using System;
using UnityEditor;
using UnityEngine;

namespace IntercomMvp.Editor
{
    /// <summary>Wires authored scenery once. Repeated setup preserves an existing prop's tuning.</summary>
    internal static class IntercomReactivePropSetup
    {
        internal static IntercomReactiveProp Configure(GameObject owner, IntercomPropKind kind,
            Transform visual, Transform flap, Renderer indicator, Collider[] colliders,
            IntercomPlayerMotor motor, float maximumHealth, float recoverySeconds, Transform spentVisual = null, float foldLift = .14f)
        {
            if (owner == null || visual == null || owner.transform.IsChildOf(visual))
                throw new InvalidOperationException("Reactive prop needs a collision owner and a separate visual transform.");
            if (spentVisual != null && (owner.transform.IsChildOf(spentVisual)
                || spentVisual.IsChildOf(visual) || visual.IsChildOf(spentVisual)))
                throw new InvalidOperationException("Debris must be a separate visual and cannot contain the damage receiver.");
            if (colliders == null || colliders.Length == 0)
                throw new InvalidOperationException("Reactive prop has no authored hit collider.");
            foreach (Collider collider in colliders)
                if (collider == null || !collider.transform.IsChildOf(owner.transform))
                    throw new InvalidOperationException("Reactive prop damage receiver must be on the collider or its parent.");
            if (kind == IntercomPropKind.Relay && (flap == null || indicator == null))
                throw new InvalidOperationException("Relay needs an explicit door hinge and indicator renderer.");
            IntercomReactiveProp existing = owner.GetComponent<IntercomReactiveProp>();
            if (existing != null) return existing;
            IntercomReactiveProp prop = Undo.AddComponent<IntercomReactiveProp>(owner);
            prop.kind = kind;
            prop.visual = visual;
            prop.spentVisual = spentVisual;
            prop.flap = flap;
            prop.indicator = indicator;
            prop.blockingColliders = colliders;
            prop.resetMotor = motor;
            prop.maxHealth = maximumHealth;
            prop.recoverSeconds = recoverySeconds;
            prop.foldAngle = 78f;
            prop.foldLift = foldLift;
            EditorUtility.SetDirty(prop);
            return prop;
        }
    }
}
