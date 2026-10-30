using UnityEngine;

namespace FS_Util
{
    /// <summary>
    /// Added automatically to every instance an <see cref="FSObjectPool"/>
    /// creates. Tracks pool ownership and handles timed auto-return (via Invoke)
    /// so callers don't have to run their own coroutines to recycle objects.
    /// </summary>
    public class PooledObject : MonoBehaviour
    {
        FSObjectPool pool;

        /// <summary>True while this instance is sitting idle in its pool.</summary>
        public bool IsInPool { get; private set; }

        public void Init(FSObjectPool owner) => pool = owner;

        // Called by the pool when the instance is handed out.
        public void OnSpawned()
        {
            IsInPool = false;
            CancelInvoke(nameof(ReleaseNow));
        }

        // Called by the pool when the instance is returned.
        public void OnReleased()
        {
            IsInPool = true;
            CancelInvoke(nameof(ReleaseNow));
        }

        /// <summary>Schedules a return to the pool after <paramref name="delay"/> seconds.</summary>
        public void ReleaseAfter(float delay)
        {
            CancelInvoke(nameof(ReleaseNow));
            Invoke(nameof(ReleaseNow), delay);
        }

        /// <summary>Returns this instance to its pool now.</summary>
        public void ReleaseNow()
        {
            if (pool != null) pool.Release(gameObject);
            else gameObject.SetActive(false);
        }

        void OnDisable()
        {
            // Drop any pending timed release if the object is disabled elsewhere.
            CancelInvoke(nameof(ReleaseNow));
        }
    }
}
