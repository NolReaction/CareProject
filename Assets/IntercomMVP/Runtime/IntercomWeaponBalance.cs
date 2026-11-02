using System;
namespace IntercomMvp
{
    /// <summary>Ideal unlimited-ammo balance, first shot at t=0, all projectiles hit one target.</summary>
    public static class IntercomWeaponBalance
    {
        public static float Positive(float value, float fallback, float minimum = 0.01f)
            => float.IsNaN(value) || float.IsInfinity(value) ? fallback : Math.Max(minimum, value);
        public static int VolleysToDefeat(float health, float damagePerProjectile, int projectilesPerVolley)
        {
            if (float.IsNaN(health) || float.IsInfinity(health) || health <= 0f) return 0;
            double damage = Positive(damagePerProjectile, 20f) * (double)Math.Max(1, projectilesPerVolley);
            return (int)Math.Min(int.MaxValue, Math.Ceiling(health / damage));
        }
        public static double IdealTtk(float health, float damage, int projectiles, float interval)
            => Math.Max(0, VolleysToDefeat(health, damage, projectiles) - 1) * (double)Positive(interval, 0.3f);
        public static double Dps(float damage, int projectiles, float interval)
            => Positive(damage, 20f) * (double)Math.Max(1, projectiles) / Positive(interval, 0.3f);
    }
}
