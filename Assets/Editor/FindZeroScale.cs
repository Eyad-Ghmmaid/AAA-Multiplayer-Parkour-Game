using UnityEngine;
using UnityEditor;
using UnityEngine.SceneManagement;

public static class FindZeroScale
{
    [MenuItem("Tools/Find Zero Scale in Scene")] 
    public static void Find()
    {
        int count = 0;
        var scene = SceneManager.GetActiveScene();
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                var lossy = t.lossyScale;
                // treat very small values as problematic, not only exact zero
                if (lossy.x <= 0f || lossy.y <= 0f || lossy.z <= 0f || lossy.x < 0.0001f || lossy.y < 0.0001f || lossy.z < 0.0001f)
                {
                    Debug.Log($"Zero or near-zero scale: path={GetPath(t)} localScale={t.localScale} lossyScale={lossy}", t.gameObject);
                    count++;
                }
            }
        }

        Debug.Log($"FindZeroScale: found {count} transforms with zero/near-zero scale in scene '{scene.name}'.");
    }

    private static string GetPath(Transform t)
    {
        string path = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            path = t.name + "/" + path;
        }
        return path;
    }
}
