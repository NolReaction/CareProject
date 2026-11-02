using System;
namespace IntercomMvp
{
    /// <summary>Ideal balance with optional magazine/reload, first shot at t=0, all projectiles hit one target.</summary>
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
        public static double IdealTtkWithReload(float health, float damage, int projectiles, float interval, int magazine, float reload)
        {
            int volleys = VolleysToDefeat(health, damage, projectiles);
            int perMagazine = Math.Max(1, magazine / Math.Max(1, projectiles));
            int reloads = Math.Max(0, volleys - 1) / perMagazine;
            double gap = Positive(interval, 0.18f);
            // Auto reload begins on the last volley. Its duration overlaps the normal shot cooldown.
            return Math.Max(0, volleys - 1) * gap + reloads * Math.Max(0d, Positive(reload,1.15f,0.1f) - gap);
        }
        public static double SustainedDps(float damage, int projectiles, float interval, int magazine, float reload)
        {
            int perMagazine = Math.Max(1, magazine / Math.Max(1, projectiles));
            double cycle = (perMagazine - 1) * (double)Positive(interval,0.18f) + Math.Max(Positive(interval,0.18f),Positive(reload,1.15f,0.1f));
            return perMagazine * Math.Max(1,projectiles) * (double)Positive(damage,15f) / cycle;
        }
        public static double Dps(float damage, int projectiles, float interval)
            => Positive(damage, 20f) * (double)Math.Max(1, projectiles) / Positive(interval, 0.3f);
    }
}
