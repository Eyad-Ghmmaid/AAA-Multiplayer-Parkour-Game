using System.Collections.Generic;
using UnityEngine;

namespace FS_Util
{
    /// <summary>
    /// Generic GameObject pool. Reuses inactive instances instead of repeated
    /// Instantiate/Destroy, avoiding allocation/GC churn for frequently spawned
    /// short-lived objects (VFX, one-shot audio, projectiles, etc.).
    ///
    /// Spawn from a prefab, or pass a null prefab to pool plain empty
    /// GameObjects (handy for runtime-built objects like one-shot audio sources).
    /// Every pooled instance gets a <see cref="PooledObject"/> component, which
    /// powers timed auto-return via <see cref="ReleaseAfter"/> so callers don't
    /// need their own coroutines.
    /// </summary>
    public class FSObjectPool
    {
        readonly Queue<GameObject> available = new Queue<GameObject>();
        readonly GameObject prefab;
        readonly Transform parent;
        readonly string objectName;

        /// <param name="prefab">Prefab to clone, or null to create empty GameObjects.</param>
        /// <param name="parent">Optional parent for pooled instances (also where released instances are reparented).</param>
        /// <param name="objectName">Name used for empty (null-prefab) instances.</param>
        /// <param name="prewarm">Number of instances to create up front.</param>
        public FSObjectPool(GameObject prefab, Transform parent = null, string objectName = "Pooled Object", int prewarm = 0)
        {
            this.prefab = prefab;
            this.parent = parent;
            this.objectName = string.IsNullOrEmpty(objectName)
                ? (prefab != null ? prefab.name : "Pooled Object")
                : objectName;

            for (int i = 0; i < prewarm; i++)
                available.Enqueue(CreateInstance());
        }

        GameObject CreateInstance()
        {
            GameObject go = prefab != null
                ? Object.Instantiate(prefab, parent)
                : new GameObject(objectName);

            if (prefab == null && parent != null)
                go.transform.SetParent(parent, false);

            var pooled = go.GetComponent<PooledObject>();
            if (pooled == null) pooled = go.AddComponent<PooledObject>();
            pooled.Init(this);

            go.SetActive(false);
            return go;
        }

        /// <summary>Fetches an active instance from the pool (creating one if empty).</summary>
        public GameObject Get()
        {
            GameObject go = null;
            // Skip over any instances destroyed externally (Unity-null).
            while (available.Count > 0 && go == null)
                go = available.Dequeue();
            if (go == null)
                go = CreateInstance();

            go.GetComponent<PooledObject>().OnSpawned();
            go.SetActive(true);
            return go;
        }

        /// <summary>Fetches an active instance positioned/oriented in world space.</summary>
        public GameObject Get(Vector3 position, Quaternion rotation)
        {
            GameObject go = Get();
            go.transform.SetPositionAndRotation(position, rotation);
            return go;
        }

        /// <summary>Returns an instance to the pool immediately.</summary>
        public void Release(GameObject go)
        {
            if (go == null) return;

            var pooled = go.GetComponent<PooledObject>();
            if (pooled != null)
            {
                if (pooled.IsInPool) return; // guard against double release
                pooled.OnReleased();
            }

            go.SetActive(false);
            if (parent != null && go.transform.parent != parent)
                go.transform.SetParent(parent, false);

            available.Enqueue(go);
        }

        /// <summary>Returns an instance to the pool after <paramref name="delay"/> seconds.</summary>
        public void ReleaseAfter(GameObject go, float delay)
        {
            if (go == null) return;
            if (delay <= 0f) { Release(go); return; }

            var pooled = go.GetComponent<PooledObject>();
            if (pooled != null) pooled.ReleaseAfter(delay);
            else Release(go);
        }
    }
}
