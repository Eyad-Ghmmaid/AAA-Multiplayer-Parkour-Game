using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FS_Core
{

    public class Crosshair : MonoBehaviour
    {
        public float size = 1.0f;
        public Transform View { get; set; }
        private SpriteRenderer spriteRenderer;
        private MeshRenderer meshRenderer;

        void Start()
        {
            if (View == null && Camera.main != null)
                View = Camera.main.transform;
            spriteRenderer = GetComponent<SpriteRenderer>();
            meshRenderer = GetComponent<MeshRenderer>();
        }

        public void Update()
        {
            if (View == null)
                return;
            transform.rotation = View.rotation;
            float distance = Vector3.Distance(transform.position, View.position);
            float scaleFactor = (distance / 5) * size;
            var y = spriteRenderer != null && spriteRenderer.sprite != null
                ? spriteRenderer.sprite.bounds.size.y : GetMeshBoundsSize();
            transform.localScale = Vector3.one * scaleFactor / Mathf.Max(0.001f, y);
        }

        public float GetMeshBoundsSize()
        {
            if (meshRenderer != null && meshRenderer.bounds.size != Vector3.zero)
                return meshRenderer.bounds.size.y;
            return 1.0f;
        }
    }
}
