#if CMPSETUP_COMPLETE
using System;
using System.Collections.Generic;
using System.IO;
using Fusion;
using UnityEngine.SceneManagement;

namespace AvocadoShark
{
    internal static class RaceMapCatalog
    {
        private const string SceneFolder = "Assets/Clean Multiplayer Pro/Scenes/";
        internal const string GameScenePath = SceneFolder + "Game.unity";
        internal const string MapProperty = "map";

        internal static IReadOnlyList<string> Paths { get; } = GetBuildMaps();

        private static List<string> GetBuildMaps()
        {
            var paths = new List<string>();
            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                string path = SceneUtility.GetScenePathByBuildIndex(i);
                if (path.StartsWith(SceneFolder, StringComparison.Ordinal) &&
                    path != GameScenePath && path != SceneFolder + "Menu.unity")
                    paths.Add(path);
            }
            return paths;
        }

        internal static string GetName(string path) => Path.GetFileNameWithoutExtension(path);

        internal static bool TryGetRoomMap(SessionInfo session, out string path)
        {
            path = null;
            if (session?.Properties == null ||
                !session.Properties.TryGetValue(MapProperty, out var property) ||
                property == null || !property.IsString)
                return false;

            path = property;
            return !string.IsNullOrEmpty(path);
        }

        internal static bool TryGetLoadedMap(out Scene scene)
        {
            foreach (string path in Paths)
            {
                scene = SceneManager.GetSceneByPath(path);
                if (scene.isLoaded)
                    return true;
            }
            scene = default;
            return false;
        }

        internal static RaceStartPoint FindStartPoint(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var start = root.GetComponentInChildren<RaceStartPoint>();
                if (start != null && start.isActiveAndEnabled)
                    return start;
            }
            return null;
        }
    }
}
#endif
