using UnityEngine;

namespace FS_Core
{
    public class DamageCalculator
    {
        public static float CalculateDamage(float weaponDamage, float attackDamage, Damagable damagable)
        {
            float damage = weaponDamage + attackDamage;

            return ApplyDefenseReductionPrecentage(damage, damagable);
        }

        public static float ApplyDefenseReductionDirect(float damage, Damagable damagable)
        {
            float defense = damagable != null ? damagable.GetDefense() : 0;
            float maxReduction = damage / 2;

            float defenseReduction = Mathf.Clamp(defense, 0, maxReduction);
            return damage - defenseReduction;
        }

        public static float ApplyDefenseReductionPrecentage(float damage, Damagable damagable)
        {
            // Defense constant k controls how much effect does the defense stat have on damage reduction
            float k = 10;
            float defense = damagable != null ? damagable.GetDefense() : 0;

            float defenseReduction = defense / (defense + k);

            return damage * (1 - defenseReduction);
        }
    }
}
