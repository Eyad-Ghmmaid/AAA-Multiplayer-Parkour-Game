#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AvocadoShark
{
    /// <summary>
    /// Detects and repairs the Photon Fusion 2.1 / Photon Voice 2 (Asset Store) packaging conflict.
    ///
    /// Background: Fusion 2.1+ ships Photon Realtime 5 as source (Photon.Realtime.asmdef,
    /// PhotonClient.dll) into Assets/Photon/PhotonRealtime and Assets/Photon/PhotonLibs.
    /// The Asset Store build of Photon Voice 2 (v2.63) still ships Photon Realtime 4
    /// (PhotonRealtime.asmdef, Photon3Unity3D.dll) into the SAME folders, partly with the
    /// SAME meta GUIDs. Importing both corrupts the project: duplicate asmdefs in one folder,
    /// Realtime 4 sources overwriting Realtime 5 sources, and two plugin DLLs with duplicate
    /// types. Compilation dies and the Setup Wizard cannot apply changes.
    ///
    /// The correct pairing for Fusion 2.1 is the "Unity Voice SDK Realtime5" package that
    /// Photon distributes only via their download page (sign-in required), NOT the Asset
    /// Store Voice 2 package.
    /// </summary>
    [InitializeOnLoad]
    public static class PhotonConflictRepairer
    {
        public const string VoiceRt5DownloadPage =
            "https://doc.photonengine.com/fusion/current/getting-started/sdk-download";

        const string RealtimeCodeDir = "Assets/Photon/PhotonRealtime/Code";
        const string NewAsmdef = RealtimeCodeDir + "/Photon.Realtime.asmdef";   // Realtime 5 (Fusion 2.1+)
        const string OldAsmdef = RealtimeCodeDir + "/PhotonRealtime.asmdef";    // Realtime 4 (Voice 2 store / PUN)
        const string Rt5Lib = "Assets/Photon/PhotonLibs/netstandard2.0/release/PhotonClient.dll";
        const string Rt4Lib = "Assets/Photon/PhotonLibs/netstandard2.0/Photon3Unity3D.dll";
        const string Rt4Client = RealtimeCodeDir + "/LoadBalancingClient.cs";
        const string FusionConfigPath = "Assets/Photon/Fusion/Resources/NetworkProjectConfig.fusion";
        const string SessionRepairFlag = "CMP_PhotonRepair_AwaitingFusionReimport";

        public enum PhotonState
        {
            NoPhoton,        // Fusion not imported yet
            Fusion20World,   // Fusion 2.0.x (Realtime 4 only) - store Voice 2 is the right pairing
            Fusion21Clean,   // Fusion 2.1+ (Realtime 5) with no Realtime 4 contamination
            Conflict         // Realtime 4 and Realtime 5 mixed together - project is broken
        }

        static PhotonConflictRepairer()
        {
            EditorApplication.delayCall += () =>
            {
                if (SessionState.GetBool(SessionRepairFlag, false))
                    return; // mid-repair, don't nag
                // Silently drop "Name 2.ext" import/sync artifacts first - a stray duplicate
                // .cs alone breaks compilation (duplicate types in the same assembly).
                if (SweepNumberedDuplicates() > 0)
                    AssetDatabase.Refresh();
                if (DetectState() == PhotonState.Conflict)
                {
                    PromptRepair();
                    return;
                }
#if CMPSETUP_COMPLETE
                // Runs on the first domain reload after setup completes - the first moment
                // every CMP NetworkBehaviour class exists, so the bake can be complete.
                RebakeCmpNetworkObjectsOnce();
#endif
            };
        }

        public static PhotonState DetectState()
        {
            bool fusionPresent = Directory.Exists("Assets/Photon/Fusion");
            bool rt5 = File.Exists(NewAsmdef) || File.Exists(Rt5Lib);
            bool rt4 = File.Exists(OldAsmdef) || File.Exists(Rt4Lib) || File.Exists(Rt4Client);

            if (!fusionPresent && !rt5 && !rt4)
                return PhotonState.NoPhoton;
            if (rt5 && rt4)
                return PhotonState.Conflict;
            // Repeated imports over a GUID clash make Unity materialize incoming files as
            // "Name 2.ext" copies (e.g. "Photon.Realtime 2.asmdef"). More than one asmdef in
            // the Realtime folder always means the folder is corrupted.
            if (Directory.Exists(RealtimeCodeDir) &&
                Directory.GetFiles(RealtimeCodeDir, "*.asmdef", SearchOption.TopDirectoryOnly).Length > 1)
                return PhotonState.Conflict;
            if (rt5)
                return PhotonState.Fusion21Clean;
            return PhotonState.Fusion20World;
        }

        /// <summary>
        /// Removes "Name 2.ext"-style duplicates that Unity (or cloud file sync, e.g. iCloud
        /// Drive on macOS) materializes inside the Photon folders during import collisions.
        /// Restricted to Photon-owned folders where such names are never legitimate.
        /// </summary>
        public static int SweepNumberedDuplicates()
        {
            int removedCount = 0;
            string[] roots = { "Assets/Photon" };
            // "Name 2", "Name 2.cs", "changes-realtime 2.txt", "Photon.Realtime 2.asmdef", ...
            // Only treated as an artifact when the original ("Name.cs") exists next to it, so
            // legitimately numbered assets are never touched.
            var pattern = new System.Text.RegularExpressions.Regex(@"^(.*) \d+((?:\.[^ ./\\]+)*)$");
            foreach (var root in roots)
            {
                if (!Directory.Exists(root))
                    continue;
                var paths = Directory.GetFileSystemEntries(root, "*", SearchOption.AllDirectories)
                    .Where(p => !p.EndsWith(".meta", StringComparison.Ordinal))
                    .Where(p =>
                    {
                        var name = Path.GetFileName(p);
                        var m = pattern.Match(name);
                        if (!m.Success)
                            return false;
                        string original = m.Groups[1].Value + m.Groups[2].Value;
                        return File.Exists(Path.Combine(Path.GetDirectoryName(p), original)) ||
                               Directory.Exists(Path.Combine(Path.GetDirectoryName(p), original));
                    })
                    .OrderByDescending(p => p.Length) // children before parents
                    .ToArray();
                foreach (var p in paths)
                {
                    var assetPath = p.Replace('\\', '/');
                    if (AssetDatabase.DeleteAsset(assetPath) ||
                        DeleteFromDisk(assetPath))
                    {
                        Debug.Log("[CMP Photon Repair] Removed duplicate artifact " + assetPath);
                        removedCount++;
                    }
                }
            }
            return removedCount;
        }

        static bool DeleteFromDisk(string path)
        {
            try
            {
                if (Directory.Exists(path))
                    Directory.Delete(path, true);
                else if (File.Exists(path))
                    File.Delete(path);
                else
                    return false;
                if (File.Exists(path + ".meta"))
                    File.Delete(path + ".meta");
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool IsFusion21OrNewer =>
            DetectState() == PhotonState.Fusion21Clean || File.Exists(NewAsmdef) || File.Exists(Rt5Lib);

        static void PromptRepair()
        {
            bool go = EditorUtility.DisplayDialog(
                "Clean Multiplayer Pro - Photon conflict detected",
                "Your project contains BOTH Photon Realtime 5 (from Fusion 2.1+) and Photon Realtime 4 " +
                "(from the Asset Store version of Photon Voice 2). Photon ships these into the same " +
                "folders, which corrupts the import and breaks compilation " +
                "('Folder contains multiple assembly definition files', followed by hundreds of errors).\n\n" +
                "Fusion 2.1 requires the 'Unity Voice SDK Realtime5' package from Photon's download page " +
                "instead of the Asset Store version.\n\n" +
                "CMP can repair this automatically:\n" +
                "1. Remove the Realtime 4 packages (Voice 2, PUN, Chat)\n" +
                "2. Restore pristine Fusion files from your Asset Store cache\n" +
                "3. Guide you to import the correct Voice package\n\n" +
                "A list of removed folders is printed to the Console. Proceed?",
                "Repair now", "Not now");
            if (go)
                RepairNow();
        }

        [MenuItem("Tools/CMP/Repair Photon Voice-Fusion Conflict")]
        public static void RepairFromMenu()
        {
            var state = DetectState();
            if (state != PhotonState.Conflict)
            {
                EditorUtility.DisplayDialog("CMP Photon Repair",
                    "No Realtime 4 / Realtime 5 conflict detected (state: " + state + ").", "OK");
                return;
            }
            PromptRepair();
        }

        public static void RepairNow()
        {
            // 1. Remove everything belonging to the Realtime-4 world. The Voice RT5 package
            //    reinstalls PhotonVoice afterwards; PhotonRealtime + PhotonLibs are restored
            //    pristine by re-importing Fusion (content of shared-GUID files was corrupted).
            string[] doomed =
            {
                "Assets/Photon/PhotonVoice",
                "Assets/Photon/PhotonUnityNetworking",
                "Assets/Photon/PhotonChat",
                "Assets/Photon/PhotonVoice-Documentation.chm",
                "Assets/Photon/PhotonVoice-Documentation.pdf",
                "Assets/Photon/PhotonRealtime",
                "Assets/Photon/PhotonLibs",
            };
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var path in doomed)
                {
                    if (File.Exists(path) || Directory.Exists(path))
                    {
                        if (AssetDatabase.DeleteAsset(path))
                            Debug.Log("[CMP Photon Repair] Removed " + path);
                        else
                            Debug.LogWarning("[CMP Photon Repair] Could not remove " + path);
                    }
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
            SetupWizard.CleanUpPunDefineSymbols();
            AssetDatabase.Refresh();

            // 2. Restore pristine Fusion content (Realtime 5 + PhotonLibs) from the local
            //    Asset Store download cache - same-GUID/same-path assets are re-created.
            string cached = FindCachedFusionPackage();
            if (cached != null)
            {
                SessionState.SetBool(SessionRepairFlag, true);
                AssetDatabase.importPackageCompleted += OnFusionReimported;
                Debug.Log("[CMP Photon Repair] Re-importing pristine Fusion package: " + cached);
                AssetDatabase.ImportPackage(cached, false);
            }
            else
            {
                EditorUtility.DisplayDialog("CMP Photon Repair - one manual step",
                    "The Realtime 4 packages were removed, but no cached 'Photon Fusion' package was " +
                    "found on this machine.\n\nPlease re-import Photon Fusion from " +
                    "Window > Package Manager > My Assets (or the Asset Store page that will open now). " +
                    "Afterwards, use Tools > CMP > Setup Wizard to install the correct Voice package.",
                    "OK");
                Application.OpenURL("https://assetstore.unity.com/packages/tools/network/photon-fusion-multiplayer-sdk-267958");
            }
        }

        static void OnFusionReimported(string packageName)
        {
            AssetDatabase.importPackageCompleted -= OnFusionReimported;
            SessionState.SetBool(SessionRepairFlag, false);
            SweepNumberedDuplicates();
            if (DetectState() == PhotonState.Conflict)
            {
                // Extremely defensive: if a conflicting file came back, run once more.
                Debug.LogWarning("[CMP Photon Repair] Conflict still present after re-import, repairing again.");
                RepairNow();
                return;
            }
            EditorUtility.DisplayDialog("CMP Photon Repair - Fusion restored",
                "Fusion's Photon Realtime 5 files were restored.\n\n" +
                "Last step: Fusion 2.1 needs the 'Unity Voice SDK Realtime5' package (the Asset Store " +
                "version of Photon Voice 2 is Realtime 4 and will corrupt the project again!).\n\n" +
                "1. Photon's download page will open - sign in and download 'Unity Voice SDK Realtime5'\n" +
                "2. Then use 'Import downloaded Voice package' in Tools > CMP > Setup Wizard",
                "Open download page");
            Application.OpenURL(VoiceRt5DownloadPage);
            SetupWizard.ShowWindow();
        }

        /// <summary>Locates the newest cached Photon Fusion .unitypackage downloaded by the Asset Store.</summary>
        public static string FindCachedFusionPackage()
        {
            foreach (var root in AssetStoreCacheRoots())
            {
                if (!Directory.Exists(root))
                    continue;
                try
                {
                    var candidates = Directory.GetFiles(root, "*.unitypackage", SearchOption.AllDirectories)
                        .Where(f => Path.GetFileNameWithoutExtension(f)
                            .IndexOf("Fusion", StringComparison.OrdinalIgnoreCase) >= 0)
                        .Where(f => f.IndexOf("Photon", StringComparison.OrdinalIgnoreCase) >= 0)
                        .OrderByDescending(File.GetLastWriteTimeUtc)
                        .ToArray();
                    if (candidates.Length > 0)
                        return candidates[0];
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[CMP Photon Repair] Could not scan " + root + ": " + e.Message);
                }
            }
            return null;
        }

        static string[] AssetStoreCacheRoots()
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return new[]
            {
                Path.Combine(home, "Library/Unity/Asset Store-5.x"),                                  // macOS
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Unity/Asset Store-5.x"),                                                          // Windows
                Path.Combine(home, ".local/share/unity3d/Asset Store-5.x"),                            // Linux
            };
        }

        /// <summary>Wizard entry point: pick the downloaded Voice RT5 .unitypackage and import it.</summary>
        public static void ImportDownloadedVoicePackage()
        {
            string downloads = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            string file = EditorUtility.OpenFilePanel(
                "Select the downloaded 'Unity Voice SDK Realtime5' package",
                Directory.Exists(downloads) ? downloads : "", "unitypackage");
            if (string.IsNullOrEmpty(file))
                return;

            string name = Path.GetFileName(file).ToLowerInvariant();
            bool looksRt5 = name.Contains("realtime5");
            bool looksRt4Voice = name.Contains("voice") && !looksRt5;
            if (IsFusion21OrNewer && looksRt4Voice)
            {
                EditorUtility.DisplayDialog("Wrong Voice package",
                    "'" + Path.GetFileName(file) + "' looks like the Realtime 4 version of Photon Voice, " +
                    "which is NOT compatible with Fusion 2.1 and would corrupt the project again.\n\n" +
                    "Please download 'Unity Voice SDK Realtime5' (photon-voice-sdk_realtime5_*.unitypackage) " +
                    "from Photon's download page.",
                    "Open download page");
                Application.OpenURL(VoiceRt5DownloadPage);
                return;
            }

            AssetDatabase.importPackageCompleted += OnVoiceImported;
            AssetDatabase.ImportPackage(file, false);
        }

        static void OnVoiceImported(string packageName)
        {
            AssetDatabase.importPackageCompleted -= OnVoiceImported;
            SweepNumberedDuplicates();
            if (DetectState() == PhotonState.Conflict)
            {
                Debug.LogWarning("[CMP Photon Repair] The imported Voice package re-introduced Realtime 4 " +
                                 "files. Repairing again - please use the Realtime5 Voice package.");
                PromptRepair();
                return;
            }
            EnsureVoiceFusionWeaved();
        }

        /// <summary>
        /// Adds "PhotonVoice.Fusion" to AssembliesToWeave in NetworkProjectConfig.fusion
        /// (required by Photon's Voice-for-Fusion integration). Edits the JSON file directly
        /// so it works regardless of Fusion's own compilation state.
        /// </summary>
        public static bool EnsureVoiceFusionWeaved()
        {
            try
            {
                if (!File.Exists(FusionConfigPath))
                    return false;
                string json = File.ReadAllText(FusionConfigPath);
                if (json.Contains("\"PhotonVoice.Fusion\""))
                    return true;
                const string key = "\"AssembliesToWeave\"";
                int keyIdx = json.IndexOf(key, StringComparison.Ordinal);
                if (keyIdx < 0)
                    return false;
                int bracket = json.IndexOf('[', keyIdx);
                if (bracket < 0)
                    return false;
                json = json.Insert(bracket + 1, "\n    \"PhotonVoice.Fusion\",");
                File.WriteAllText(FusionConfigPath, json);
                AssetDatabase.ImportAsset(FusionConfigPath);
                Debug.Log("[CMP Photon Repair] Added PhotonVoice.Fusion to AssembliesToWeave.");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[CMP Photon Repair] Could not update AssembliesToWeave: " + e.Message);
                return false;
            }
        }

        // The Fusion Physics addon for 2.1 merged/renamed its component classes, changing the
        // script GUIDs - prefabs authored against the 2.0.x addon end up with missing scripts,
        // which makes Fusion refuse to spawn them ("The object ... needs to be rebaked").
        // On top of that, the 2.1 addon's NetworkRigidbody DESPAWNS ITSELF in Shared Mode
        // (which CMP uses), so the correct migration target for CMP's character prefabs is
        // Fusion's built-in NetworkTransform (same NetworkTRSP base, shared-mode supported).
        const string FusionRuntimeDllGuid = "e725a070cec140c4caffb81624c8c787";
        const string NetworkTransformScriptRef =
            "m_Script: {fileID: 158639473, guid: " + FusionRuntimeDllGuid + ", type: 3}";

        static readonly string[] OldNetworkRigidbodyScriptRefs =
        {
            // NetworkRigidbody3D (physics addon 2.0)
            "m_Script: {fileID: 11500000, guid: 0a591d221a634417e9827eb58e17de84, type: 3}",
            // NetworkRigidbody2D (physics addon 2.0)
            "m_Script: {fileID: 11500000, guid: c5e690b5fb1084f5ab1c86457af401d4, type: 3}",
            // NetworkRigidbody (physics addon 2.1 - self-despawns in shared mode)
            "m_Script: {fileID: 11500000, guid: 5baa37e08a734f79b9f3720311e58752, type: 3}",
        };

        static readonly string[,] RunnerPhysicsGuidRemap =
        {
            // RunnerSimulatePhysics3D (2.0) -> RunnerSimulatePhysics (2.1)
            { "7b5a1ff9dee264bfd829a39e6542dcbd", "82522767432240b68cb8b70fe6240e86" },
            // RunnerSimulatePhysics2D (2.0) -> RunnerSimulatePhysics (2.1)
            { "a9fd79d79a0fb459ababab9a78f00caa", "82522767432240b68cb8b70fe6240e86" },
        };

        [MenuItem("Tools/CMP/Migrate Physics Addon References (2.0 to 2.1)")]
        public static void ApplyPhysicsAddonGuidRemap()
        {
            const string root = "Assets/Clean Multiplayer Pro";
            if (!Directory.Exists(root))
                return;
            int changedFiles = 0;
            foreach (var file in Directory.GetFiles(root, "*.*", SearchOption.AllDirectories))
            {
                if (!file.EndsWith(".prefab", StringComparison.Ordinal) &&
                    !file.EndsWith(".unity", StringComparison.Ordinal))
                    continue;
                string text = File.ReadAllText(file);
                string original = text;
                foreach (var oldRef in OldNetworkRigidbodyScriptRefs)
                    text = text.Replace(oldRef, NetworkTransformScriptRef);
                for (int i = 0; i < RunnerPhysicsGuidRemap.GetLength(0); i++)
                {
                    // Only remap the runner components when the 2.1 script exists and the
                    // 2.0 one is gone.
                    if (!string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(RunnerPhysicsGuidRemap[i, 1])) &&
                        string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(RunnerPhysicsGuidRemap[i, 0])))
                        text = text.Replace(RunnerPhysicsGuidRemap[i, 0], RunnerPhysicsGuidRemap[i, 1]);
                }
                if (text == original)
                    continue;
                File.WriteAllText(file, text);
                AssetDatabase.ImportAsset(file.Replace('\\', '/'));
                changedFiles++;
                Debug.Log("[CMP Photon Repair] Migrated physics components in " + file);
            }
            if (changedFiles > 0)
                AssetDatabase.Refresh();
        }

        /// <summary>
        /// Forces a reimport of CMP's prefabs so Fusion's NetworkObjectPostprocessor rebakes
        /// them for the installed Fusion version. Fusion 2.1 changed the bake format
        /// (NetworkObjectFlags V1 -> V2); prefabs baked by an older Fusion fail to attach at
        /// runtime ("Networked properties can only be accessed when Spawned() has been called").
        /// Scene objects rebake automatically on scene save / entering play mode.
        /// </summary>
        [MenuItem("Tools/CMP/Rebake CMP Network Prefabs")]
        public static void RebakeCmpNetworkObjects()
        {
            const string prefabRoot = "Assets/Clean Multiplayer Pro/Prefabs";
            if (!Directory.Exists(prefabRoot))
                return;
            AssetDatabase.ImportAsset(prefabRoot,
                ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
            Debug.Log("[CMP Photon Repair] Reimported " + prefabRoot + " (Fusion rebake).");
        }

        static void RebakeCmpNetworkObjectsOnce()
        {
            // One-time per project+Fusion-generation; EditorPrefs is machine-wide, so scope
            // the key to this project. (v2: also migrates physics components & rebakes scenes)
            string key = "CMP_Rebaked_v2_" + PlayerSettings.productGUID +
                         (IsFusion21OrNewer ? "_F21" : "_F20");
            if (EditorPrefs.GetBool(key, false))
                return;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return; // try again next domain reload
            ApplyPhysicsAddonGuidRemap();
            RebakeCmpNetworkObjects();
            // Only mark the one-time migration done when the scene rebake actually ran -
            // the user can defer it by cancelling the save-modified-scenes prompt.
            if (RebakeCmpScenes())
                EditorPrefs.SetBool(key, true);
        }

        /// <summary>
        /// Full Fusion 2.1 migration in one call (also usable headless via
        /// -executeMethod AvocadoShark.PhotonConflictRepairer.RunFullMigration).
        /// </summary>
        public static void RunFullMigration()
        {
            SweepNumberedDuplicates();
            ApplyPhysicsAddonGuidRemap();
            RebakeCmpNetworkObjects();
            RebakeCmpScenes();
            AssetDatabase.SaveAssets();
            Debug.Log("[CMP Photon Repair] Full migration finished.");
        }

        /// <summary>
        /// Opens and re-saves each CMP scene so Fusion's scene-save hook rebakes the scene
        /// NetworkObjects (e.g. the pickup spawner in Game.unity). Runtime-loaded scenes are
        /// never rebaked automatically, so a Fusion bake-format change (2.0 -> 2.1) leaves
        /// them stale until saved once in the editor.
        /// </summary>
        [MenuItem("Tools/CMP/Rebake CMP Scenes")]
        public static void RebakeCmpScenesMenu()
        {
            RebakeCmpScenes();
        }

        /// <summary>Returns true when the rebake ran (or there was nothing to do).</summary>
        public static bool RebakeCmpScenes()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[CMP Photon Repair] Cannot rebake scenes while in play mode.");
                return false;
            }
            const string sceneRoot = "Assets/Clean Multiplayer Pro/Scenes";
            if (!Directory.Exists(sceneRoot))
                return true; // nothing to rebake counts as success
            var setup = UnityEditor.SceneManagement.EditorSceneManager.GetSceneManagerSetup();
            if (!UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return false; // user deferred - retry on a later domain reload
            try
            {
                foreach (var scenePath in Directory.GetFiles(sceneRoot, "*.unity", SearchOption.AllDirectories))
                {
                    var path = scenePath.Replace('\\', '/');
                    var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                        path, UnityEditor.SceneManagement.OpenSceneMode.Single);
                    UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
                    UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
                    Debug.Log("[CMP Photon Repair] Re-saved (rebaked) scene " + path);
                }
            }
            finally
            {
                if (setup != null && setup.Length > 0)
                    UnityEditor.SceneManagement.EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
            return true;
        }

        public static bool IsVoiceFusionWeaved()
        {
            try
            {
                return File.Exists(FusionConfigPath) &&
                       File.ReadAllText(FusionConfigPath).Contains("\"PhotonVoice.Fusion\"");
            }
            catch
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Catches the corruption the moment it happens: if a Realtime 4 asmdef is imported
    /// while the Realtime 5 one exists, offer the repair immediately instead of letting the
    /// user face a wall of compile errors.
    /// </summary>
    internal class PhotonConflictWatcher : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(
            string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (!imported.Any(p => p.StartsWith("Assets/Photon/", StringComparison.Ordinal)))
                return;
            EditorApplication.delayCall += () =>
            {
                // Duplicate "Name 2.ext" artifacts (repeated imports, cloud-sync restores)
                // break compilation on their own - sweep them on every Photon import.
                if (PhotonConflictRepairer.SweepNumberedDuplicates() > 0)
                    AssetDatabase.Refresh();
                if (PhotonConflictRepairer.DetectState() == PhotonConflictRepairer.PhotonState.Conflict)
                    PhotonConflictRepairer.RepairFromMenu();
            };
        }
    }
}
#endif
