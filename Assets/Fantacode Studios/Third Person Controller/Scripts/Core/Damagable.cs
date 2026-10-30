using FS_Core;
using FS_Util;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;


namespace FS_Core
{
    public class Damagable : MonoBehaviour, ISavable
    {
        [field: SerializeField] public virtual float MaxHealth { get; set; } = 100;

        [Tooltip("When enabled, only damage from the listed damage types will be accepted.")]
        [SerializeField] bool filterByDamageType = false;

        [Tooltip("Bitmask of damage types that are allowed to deal damage to this object. " +
                 "Select types from the DamageTypeDefinitions asset. 'Everything' = accept all.")]
        [ShowIf("filterByDamageType", true)]
        [DamageTypeDropdown]
        [SerializeField] int allowedDamageTypesMask = ~0;   // Everything by default
        [field: SerializeField] public virtual float MaxBreath { get; set; } = 100;
        [Space(5)]
        [SerializeField] UnityEvent OnDeadEvent;

        public virtual float CurrentHealth { get; set; }
        public virtual float CurrentBreath { get; set; }
        public event Action OnHealthUpdated;
        public event Action OnBreathUpdating;
        public event Action OnDead;
        public event Action<ImpactData> OnImpactDamage;
        public virtual Damagable Parent => this;
        public virtual bool CanTakeHit { get; set; } = true;
        public bool IsDead => CurrentHealth <= 0;
        public float Health01 => (CurrentHealth / MaxHealth);

        ItemAttacher itemAttacher;
        protected virtual void Awake()
        {
            CurrentHealth = MaxHealth;
            itemAttacher = GetComponent<ItemAttacher>();

            CurrentBreath = MaxBreath;
        }

        /// <param name="damageTypeMask">
        /// Bitmask of the weapon's damage type(s) (indices into DamageTypeDefinitions).
        /// Pass 0 if the weapon has no specific damage type — the type filter is skipped.
        /// </param>
        /// <returns>
        /// True if the damage was accepted and applied; false if this object is immune to the
        /// weapon's damage type(s) (filtered out). Callers should use this to skip reactions /
        /// effects when the hit was rejected — otherwise an immune target still flinches.
        /// </returns>
        public virtual bool TakeDamage(float damage, int damageTypeMask = 0)
        {
            if (filterByDamageType)
            {
                // Only the bits for actually-defined damage types are meaningful. The mask fields
                // default to ~0 ("Everything"), which also sets unused upper bits; the dropdown only
                // toggles defined bits, so those leftover bits would make every weapon overlap every
                // target and defeat the filter. Strip them before testing for overlap.
                int weaponBits = damageTypeMask & DamageTypeDefinitions.DefinedBitsMask;

                // Untyped weapon (no defined damage-type bits) — skip the filter, it always hits.
                // Immune: none of the weapon's damage-type bits are in the accepted set.
                if (weaponBits != 0 && (allowedDamageTypesMask & weaponBits) == 0)
                    return false;
            }

            UpdateHealth(-damage);

            if (CurrentHealth <= 0)
            {
                OnDead?.Invoke();
                OnDeadEvent?.Invoke();
            }

            return true;
        }

        /// <summary>
        /// Applies damage and fires the impact event with force data.
        /// Use this for any heavy-impact source: explosions, vehicle hits, strong knockbacks.
        /// </summary>
        /// <returns>True if the damage was applied; false if filtered out by damage type
        /// (in which case the impact event is not fired — no knockback on an immune target).</returns>
        public virtual bool TakeImpactDamage(float damage, ImpactData impact, int damageTypeMask = 0)
        {
            if (!TakeDamage(damage, damageTypeMask))
                return false;

            impact.damage = damage;
            OnImpactDamage?.Invoke(impact);
            return true;
        }

        public virtual void UpdateHealth(float hpRestore)
        {
            CurrentHealth = Mathf.Clamp(CurrentHealth + hpRestore, 0, MaxHealth);
            OnHealthUpdated?.Invoke();
        }

        public virtual void UpdateBreath(float breathRestore)
        {
            CurrentBreath = Mathf.Clamp(CurrentBreath + breathRestore, 0, MaxBreath);
            OnBreathUpdating?.Invoke();
        }

        public void ResetHealth()
        {
            CurrentHealth = MaxHealth;
            CurrentBreath = MaxBreath;
        }

        public float GetDefense()
        {
            return itemAttacher != null ? itemAttacher.GetTotalDefense() : 0;
        }

        public object CaptureState()
        {
            return new DamagableSaveData() { currentHealth = CurrentHealth };
        }

        public void RestoreState(object state)
        {
            var saveData = state as DamagableSaveData;
            CurrentHealth = saveData.currentHealth;
            OnHealthUpdated?.Invoke();
        }

        public Type GetSavaDataType()
        {
            return typeof(DamagableSaveData);
        }
    }

    [Serializable]
    public class DamagableSaveData
    {
        public float currentHealth;
    }

    /// <summary>
    /// Data passed with heavy-impact damage events (explosions, vehicle hits, etc.).
    /// Contains everything a reaction handler needs to apply ragdoll force and knockdown.
    /// </summary>
    public struct ImpactData
    {
        public Vector3 position;
        public float force;
        public float radius;

        /// <summary>
        /// Explicit force direction. When set (non-zero), the reaction handler uses
        /// this directly instead of computing direction from (target - position).
        /// Use for vehicle hits where the force should follow the vehicle's velocity.
        /// Leave as default (zero) for explosions where force radiates outward.
        /// </summary>
        public Vector3 direction;

        /// <summary>Damage actually applied by this impact. Populated by Damagable.TakeImpactDamage.</summary>
        public float damage;
    }
}
