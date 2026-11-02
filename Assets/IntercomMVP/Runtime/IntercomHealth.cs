using UnityEngine;
using UnityEngine.Events;
namespace IntercomMvp
{
    /// <summary>Reusable health for a future enemy or destructible prop. Death behaviour is explicit through onDeath.</summary>
    [DisallowMultipleComponent]
    public sealed class IntercomHealth : MonoBehaviour, IIntercomDamageable
    {
        [Min(1f)] public float maximumHealth=100f;
        public bool invulnerable;
        public UnityEvent onDamaged=new UnityEvent();
        public UnityEvent onDeath=new UnityEvent();
        public float Current => State.Current;
        public bool IsDead => State.IsDead;
        public event System.Action<IntercomDamage> Damaged;
        public event System.Action Died;
        private IntercomHealthState state;
        private IntercomHealthState State => state ?? (state=new IntercomHealthState(maximumHealth));
        private void Awake() { ResetHealth(); }
        public bool TakeDamage(IntercomDamage damage)
        {
            if(!isActiveAndEnabled || invulnerable || !State.Apply(damage.Amount))return false;
            bool died=State.IsDead;
            Damaged?.Invoke(damage); onDamaged?.Invoke();
            if(died){Died?.Invoke();onDeath?.Invoke();}
            return true;
        }
        [ContextMenu("Reset Health")]
        public void ResetHealth() { State.Reset(maximumHealth); }
    }
}
