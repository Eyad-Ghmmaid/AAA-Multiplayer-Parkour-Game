using System.Collections.Generic;
using UnityEngine;

namespace FS_Core
{
    /// <summary>
    /// Central registry of all damage type names in the project.
    ///
    /// SETUP: Create one asset via Assets > Create > FS System > Damage > Damage Type Definitions,
    /// name it exactly <c>DamageTypeDefinitions</c>, and place it inside any <c>Resources</c> folder.
    /// This makes it accessible via <c>Resources.Load</c> at both edit-time and runtime.
    ///
    /// The <see cref="DamageTypeDropdownAttribute"/> drawer will fall back to scanning the whole
    /// project if the asset is not found in Resources, but runtime code requires the Resources path.
    /// </summary>
    [CreateAssetMenu(menuName = "FS System/Damage/Damage Type Definitions")]
    public class DamageTypeDefinitions : ScriptableObject
    {
        [Tooltip("All damage type names available in the project.")]
        public List<string> damageTypes = new List<string>
        {
            "Blunt", "Piercing", "Fire", "Spark", "VehicleHit", "Poison", "Electric"
        };

        static DamageTypeDefinitions cachedInstance;
        static bool instanceResolved;

        /// <summary>
        /// The project's definitions asset, loaded once from <c>Resources/Data/DamageTypeDefinitions</c>.
        /// Returns null if the asset isn't placed in a Resources folder.
        /// </summary>
        public static DamageTypeDefinitions Instance
        {
            get
            {
                if (!instanceResolved)
                {
                    cachedInstance = Resources.Load<DamageTypeDefinitions>("Data/DamageTypeDefinitions");

                    // Fallback: the asset may be named differently on disk (e.g. "Damage Type Definitions"),
                    // in which case the path-based load above returns null. Find it by type instead.
                    if (cachedInstance == null)
                    {
                        var all = Resources.LoadAll<DamageTypeDefinitions>("");
                        if (all != null && all.Length > 0) cachedInstance = all[0];
                    }
                    instanceResolved = true;
                }
                return cachedInstance;
            }
        }

        /// <summary>
        /// Bitmask containing exactly the bits for the currently defined damage types (bits 0..Count-1).
        ///
        /// Damage-type mask fields default to <c>~0</c> ("Everything"), which also sets the unused upper
        /// bits (Count..31). The dropdown only ever toggles defined bits, so those phantom bits linger and
        /// would make every weapon mask overlap every target mask — defeating the filter. AND a mask with
        /// this before comparing two masks to strip the phantom bits.
        /// </summary>
        public static int DefinedBitsMask
        {
            get
            {
                int count = Instance != null && Instance.damageTypes != null ? Instance.damageTypes.Count : 0;
                if (count <= 0 || count >= 32) return ~0;   // nothing defined / fallback: don't strip
                return (1 << count) - 1;
            }
        }
    }
}
