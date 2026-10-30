using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FS_Core
{
    /// <summary>
    /// Implemented by the HUD component that owns the throwable aim / fuse widgets
    /// (e.g. <c>ShooterUI</c> in the Shooter module).
    ///
    /// Declared here in Core so <see cref="ThrowableController"/> can drive the HUD
    /// without taking a hard dependency on any specific system. Every member is
    /// optional at runtime — return null for any widget the HUD doesn't use.
    /// </summary>
    public interface IThrowableHUD
    {
        /// <summary>Parent object for the throwable HUD. Activated while aiming, deactivated when aim ends.</summary>
        GameObject ThrowableUI { get; }

        /// <summary>Icon of the throwable being cooked. Enabled while the fuse is active.</summary>
        Image FuseItemImage { get; }

        /// <summary>Image whose fillAmount tracks the fuse countdown (0..1).</summary>
        Image FuseFillImage { get; }

        /// <summary>Text showing the remaining fuse time in seconds.</summary>
        TextMeshProUGUI FuseTimerText { get; }
    }
}
