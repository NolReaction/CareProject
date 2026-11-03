using UnityEngine;
using UnityEngine.Events;
namespace IntercomMvp
{
    /// <summary>Health, team filtering and optional hit immunity. Death behaviour is explicit through events.</summary>
    [DisallowMultipleComponent]
    public sealed class IntercomHealth : MonoBehaviour, IIntercomDamageable
    {
        [Min(1f)] public float maximumHealth=100f;
        public IntercomTeam team;
        public bool invulnerable;
        [Min(0f)] public float postHitInvulnerability;
        public UnityEvent onDamaged=new UnityEvent();
        public UnityEvent onDeath=new UnityEvent();
        public float Current => State.Current;
        public float Maximum => State.Maximum;
        public bool IsDead => State.IsDead;
        public event System.Action<IntercomDamage> Damaged;
        public event System.Action Died;
        private IntercomHealthState state;
        private float nextDamageTime = float.NegativeInfinity;
        private uint resetGeneration;
        private IntercomHealthState State => state ?? (state=new IntercomHealthState(maximumHealth));
        private void Awake() { ResetHealth(); }
        public bool TakeDamage(IntercomDamage damage)
        {
            if(!isActiveAndEnabled || invulnerable || Time.timeScale <= 0f) return false;
            float now = Time.time;
            if(float.IsNaN(now) || float.IsInfinity(now) || now < nextDamageTime) return false;
            if(damage.Instigator != null)
            {
                if(damage.Instigator.transform == transform || damage.Instigator.transform.IsChildOf(transform)) return false;
                IntercomHealth source = damage.Instigator.GetComponentInParent<IntercomHealth>();
                if(source == this || (source != null && team != IntercomTeam.Neutral && source.team == team)) return false;
            }
            if(!State.Apply(damage.Amount)) return false;
            float immunity = float.IsNaN(postHitInvulnerability) || float.IsInfinity(postHitInvulnerability)
                ? 0f : Mathf.Clamp(postHitInvulnerability, 0f, 10f);
            nextDamageTime = now + immunity;
            bool died=State.IsDead;
            uint generation = resetGeneration;
            Damaged?.Invoke(damage); onDamaged?.Invoke();
            // A damage callback may explicitly reset the actor. Do not issue a
            // stale death callback against that new life.
            if(died && resetGeneration == generation && State.IsDead)
            {
                Died?.Invoke();
                if(resetGeneration == generation && State.IsDead) onDeath?.Invoke();
            }
            return true;
        }
        [ContextMenu("Reset Health")]
        public void ResetHealth()
        {
            State.Reset(maximumHealth); nextDamageTime = float.NegativeInfinity;
            unchecked { resetGeneration++; }
        }
    }
}
