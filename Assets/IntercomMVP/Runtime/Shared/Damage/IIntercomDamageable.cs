using UnityEngine;
namespace IntercomMvp
{
    public readonly struct IntercomDamage
    {
        public readonly float Amount;
        public readonly Vector3 Point, Direction;
        public readonly GameObject Instigator;
        public IntercomDamage(float amount, Vector3 point, Vector3 direction, GameObject instigator)
        { Amount=amount; Point=point; Direction=direction; Instigator=instigator; }
    }
    /// <summary>Implemented by enemies, training actors and destructible objects. False means rejected damage.</summary>
    public interface IIntercomDamageable { bool TakeDamage(IntercomDamage damage); }
}
