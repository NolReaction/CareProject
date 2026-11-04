using UnityEngine;
namespace IntercomMvp
{
    public enum IntercomBarrelMode { SingleAlternating, DualSimultaneous }

    [CreateAssetMenu(menuName = "Intercom MVP/Weapon Definition", fileName = "Weapon")]
    public sealed class IntercomWeaponDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string displayName = "Dual pistols";
        [TextArea] public string description = "24 rounds per magazine; unlimited reserve. Left and right pistols alternate.";
        [Header("Balance — per projectile and per volley")]
        [Min(0.01f)] public float damagePerProjectile = 15f;
        [Min(0.01f), Tooltip("Seconds between reports. Alternating pistols fire one projectile per report; simultaneous mode fires both.")]
        public float fireInterval = 0.12f;
        public IntercomBarrelMode barrelMode = IntercomBarrelMode.SingleAlternating;
        [Min(1f), Tooltip("Calculator only; target Health is configured on the target itself.")]
        public float referenceTargetHealth = 100f;
        [Header("Magazine — unlimited reserve")]
        [Min(1), Tooltip("Total projectiles in both pistols together, not volleys.")]
        public int magazineSize = 24;
        [Min(0.1f)] public float reloadSeconds = 1.15f;
        public AudioClip reloadStartClip;
        public AudioClip reloadFinishClip;
        [Range(0f,1f)] public float reloadVolume = 0.36f;
        [Header("Animation")]
        public IntercomWeaponPoseProfile poseProfile;
        [Header("Projectile")]
        [Min(0.1f)] public float projectileSpeed = 36f;
        [Min(0.1f)] public float projectileRange = 20f;
        [Min(0.05f)] public float projectileLifetime = 1.4f;
        [Min(0.005f)] public float projectileRadius = 0.035f;
        [Tooltip("Top-down fire: horizontal flight from the actual muzzle, ignoring animation tilt. Props below muzzle height can still be shot over.")]
        public bool planarProjectileDirection = true;
        [Tooltip("Both pistols share the current weapon heading while keeping their individual muzzle origins. Prevents crossed X trajectories.")]
        public bool parallelProjectiles = true;
        [Range(0.03f,0.4f)] public float tracerWidth = 0.15f;
        [Min(0.04f)] public float tracerLength = 0.68f;
        public Material projectileMaterial;
        public Material projectileCoreMaterial;
        [Header("Sound — one report per volley")]
        public AudioClip[] shotClips;
        [Range(0f,1f)] public float shotVolume = 0.68f;
        [Range(0f,0.2f)] public float shotPitchVariation = 0.008f;
        [Header("Muzzle flash")]
        public Material flashMaterial;
        [Tooltip("Optional replacement. Empty uses the built-in cartoon star.")]
        public Mesh flashMesh;
        public Vector3 flashScale = new Vector3(0.29f,0.29f,0.36f);
        [Min(0.01f)] public float flashLifetime = 0.065f;
        [Header("Ejected casing")]
        public bool ejectCasings = true;
        public Material casingMaterial;
        [Tooltip("Optional replacement. Empty uses the built-in bevelled casing.")]
        public Mesh casingMesh;
        public Vector3 casingScale = new Vector3(0.045f,0.10f,0.045f);
        [Min(0.2f)] public float casingLifetime = 2.5f;
        [Min(0f)] public float casingVelocityScale = 1f;

        public int ProjectilesPerVolley => barrelMode == IntercomBarrelMode.DualSimultaneous ? 2 : 1;
        public int MagazineCapacity => System.Math.Max(ProjectilesPerVolley, System.Math.Min(999, magazineSize));
        public float ReloadSeconds => IntercomWeaponBalance.Positive(reloadSeconds,1.15f,0.1f);
        public float Damage => IntercomWeaponBalance.Positive(damagePerProjectile,15f);
        public float Interval => IntercomWeaponBalance.Positive(fireInterval,0.18f);
        public double IdealDps => IntercomWeaponBalance.Dps(Damage,ProjectilesPerVolley,Interval);
        public int IdealVolleys => IntercomWeaponBalance.VolleysToDefeat(referenceTargetHealth,Damage,ProjectilesPerVolley);
        public double IdealTtk => IntercomWeaponBalance.IdealTtkWithReload(referenceTargetHealth,Damage,ProjectilesPerVolley,Interval,MagazineCapacity,ReloadSeconds);
        public double SustainedDps => IntercomWeaponBalance.SustainedDps(Damage,ProjectilesPerVolley,Interval,MagazineCapacity,ReloadSeconds);
    }
}
