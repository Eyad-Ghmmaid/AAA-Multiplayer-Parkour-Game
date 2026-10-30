#if UNITY_EDITOR
using FS_Core;
using FS_Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Networking;

namespace FS_Core
{
    public class FSSystemsSetupEditorWindow : EditorWindow
    {

        private static bool isEditorLoading = true;

        // "Don't Show on Start" preference, stored in PlayerPrefs (0 = show on project open,
        // 1 = don't). Only gates the project-open popup; installs always open the window.
        const string DontShowOnStartKey = "FS_DontShowOnStart";
        static bool GetDontShowOnStart() => PlayerPrefs.GetInt(DontShowOnStartKey, 0) == 1;
        static void SetDontShowOnStart(bool value)
        {
            PlayerPrefs.SetInt(DontShowOnStartKey, value ? 1 : 0);
            PlayerPrefs.Save();
        }

        // Marks that this editor session has started. SessionState is cleared on editor restart
        // but survives domain reloads, so the first load of a session = the project being opened,
        // and every later load is a plain refresh/reload.
        const string SessionStartedKey = "FS_SessionStarted";

        // Set when a .unitypackage finishes importing, so the window still opens after the
        // recompile/domain-reload the import triggers (which would otherwise wipe the scheduled
        // open). Cleared once the window actually opens.
        const string ShowOnImportKey = "FS_ShowOnImport";

        // Runs on every domain reload. Two triggers open the window: (1) a new package install,
        // and (2) the project being opened, unless "Don't Show on Start" is set. Plain refreshes
        // never open it. A pending install (survives multi-reload imports) is re-scheduled here.
        [InitializeOnLoadMethod]
        public static void OnProjectLoad()
        {
            // Open the window every time a .unitypackage finishes importing.
            AssetDatabase.importPackageCompleted -= OnImportPackageCompleted;
            AssetDatabase.importPackageCompleted += OnImportPackageCompleted;

            // A package import that completed just before this reload asked for the window.
            if (SessionState.GetBool(ShowOnImportKey, false))
                ScheduleShow();

            // Re-schedule any install popup left pending from a multi-reload import.
            if (FSSystemsSetup.GetPendingWelcomeKeys().Count > 0)
                ScheduleShow();

            // First load of the session = the project being opened. Show once unless opted out.
            bool firstLoadThisSession = !SessionState.GetBool(SessionStartedKey, false);
            if (firstLoadThisSession)
            {
                SessionState.SetBool(SessionStartedKey, true);
                if (!GetDontShowOnStart())
                    ScheduleShow();
            }
        }

        // Called by FSSystemsSetup.NotifyNewInstall the first time a system is installed.
        public static void RequestInstallPopup() => ScheduleShow();

        // Fires whenever any .unitypackage finishes importing. Shows the window every time,
        // regardless of the per-system "already shown" flags. Persists the intent across the
        // recompile the import usually triggers (OnProjectLoad re-schedules from ShowOnImportKey).
        static void OnImportPackageCompleted(string packageName)
        {
            SessionState.SetBool(ShowOnImportKey, true);
            ScheduleShow();
        }

        static void ScheduleShow()
        {
            // Re-arm isEditorLoading in case the initial editor load already consumed it (e.g. a
            // mid-session import), then (re)subscribe the deferred open.
            isEditorLoading = true;
            EditorApplication.update -= OnFirstUpdate; // avoid double subscription
            EditorApplication.update += OnFirstUpdate;
        }

        private static void OnFirstUpdate()
        {
            if (!isEditorLoading)
                return;

            if (EditorWindow.focusedWindow == null)
                return;

            isEditorLoading = false;
            EditorApplication.update -= OnFirstUpdate;

            // Show the window FIRST, then commit the pending "shown" flags. Committing only after
            // the window is actually open means an interrupted import (extra domain reloads)
            // cannot permanently suppress a popup that was never seen.
            ShowWindow();
            SessionState.SetBool(ShowOnImportKey, false); // consumed — the window is open now
            FSSystemsSetup.CommitPendingWelcomeKeys();

            // ImportProjectSettings touches TagManager / ProjectSettings serialized properties
            // that can be null or throw on some Unity versions; if it threw before ShowWindow the
            // window would silently never appear, which is why the open happens first.
            try
            {
                CreateSetupScript();
                setupScript.ImportProjectSettings();
                setupScript.FindSystem();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FS Systems] Setup window opened, but project-settings import failed: {ex}");
            }
        }



        public static FSSystemsSetup setupScript;
        public Vector2 scrolPosition = Vector2.zero;

        private float dividerPosition = 150f;

        static int selectedTab = 0;
        private string[] toolbarOptions = { "Welcome", "Setup", "Packages", "Dependencies" };

        static Rect tabRect;
        static Rect windowRect;

        static FSSystemsSetupEditorWindow window;

        private GUIStyle hintStyle;
        public static CharacterType characterType = CharacterType.Player; // Set a default value


        // Clears every installed system's per-project "already shown" flag and re-opens the
        // setup window. Use this to verify auto-popup, or to make the window appear again after
        // it was dismissed / suppressed. Also the reliable way to test first-install behaviour
        // without needing to actually re-import a package.
        // [MenuItem("Tools/FS System/Reset Setup Popup", false, 1)]
        public static void ResetSetupPopup()
        {
            CreateSetupScript();
            setupScript.FindSystem();
            foreach (var system in setupScript.AllInstalledSystems.Values)
                FSSystemsSetup.ClearWelcomeWindowShown(system.welcomeEditorShowKey);

            // Drop any half-finished pending state so the cleared flags are the source of truth.
            FSSystemsSetup.ClearPendingWelcomeKeys();

            // Open immediately (editor is already loaded when a menu item runs, so the
            // OnFirstUpdate polling path would early-out on its stale isEditorLoading flag).
            ShowWindow();
        }

        [MenuItem("Tools/FS System/ FS Window", false, 0)]
        public static void ShowWindow()
        {
            if (!HasOpenInstances<FSSystemsSetupEditorWindow>())
            {
                window = GetWindow<FSSystemsSetupEditorWindow>("FS Systems");
                SetWindowHeight(697);
                CreateSetupScript();
                setupScript.FindSystem();
                if (characterType == CharacterType.Player) setupScript.EnableSystems(FSSystemsSetup.ThirdPersonControllerSystemSetup.systemName);
                InitializeCharacterTypeOptions();
            }
        }

        public static void ChangeCharacterType(CharacterType newType)
        {
            characterType = newType;
            SetTab(1);
            setupScript.FindSystem();
            setupScript.EnableSystems();
        }

        private void OnEnable()
        {
            setupScript = FindAnyObjectByType<FSSystemsSetup>();
            if (characterModel == null)
            {
                characterModel = Resources.Load<GameObject>("Default Character");
            }
        }

        private void OnDisable()
        {
            characterType = CharacterType.Player;

            if (setupScript != null)
                GameObject.DestroyImmediate(setupScript.gameObject);

            DisposeSlides();
        }

        private void OnDestroy()
        {
            characterType = CharacterType.Player;

            if (setupScript != null)
                GameObject.DestroyImmediate(setupScript.gameObject);
        }

        private void OnGUI()
        {
            DrawTitle();
            DrawSlider();
            DrawTabs();



            if (!Event.current.Equals(EventType.Repaint))
                Repaint();
        }

        #region Window

        private void DrawTitle()
        {
            GUI.backgroundColor = Color.blue;
            GUILayout.BeginHorizontal(EditorStyles.helpBox);
            GUILayout.BeginVertical();
            GUILayout.Space(10);

            GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 25,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.031f, 0.506f, 0.992f) },
                hover = { textColor = new Color(0.031f, 0.506f, 0.992f) }
            };
            GUILayout.Label("Fantacode Studios", titleStyle);

            GUILayout.Space(10);
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            GUI.backgroundColor = Color.white;
        }
        private void DrawSlider()
        {
            var y = 59;
            tabRect = new Rect(0, y, dividerPosition, position.height - y);
            windowRect = new Rect(tabRect.width, y, position.width - tabRect.width, position.height - y);
            GUI.Box(tabRect, "");
        }
        private void DrawTabs()
        {
            var tRect = tabRect;
            tRect.x = 2;
            GUILayout.BeginArea(tRect);

            for (int i = 0; i < toolbarOptions.Length; i++)
            {
                GUI.backgroundColor = i == selectedTab ? Color.cyan : Color.white;

                if (GUILayout.Button(toolbarOptions[i], GUILayout.Height(25), GUILayout.Width(tRect.width - 4)))
                {
                    selectedTab = i;
                }
            }
            GUI.backgroundColor = Color.white;
            GUILayout.EndArea();

            GUILayout.BeginArea(windowRect);

            GUILayout.BeginVertical(EditorStyles.helpBox);


            CreateSetupScript();
            switch (selectedTab)
            {
                case 0:
                    WelcomeWindow();
                    break;
                case 1:
                    SetupWindow();
                    break;
                case 2:
                    PackageWindow();
                    break;
                case 3:
                    DependenciesWindow();
                    break;
            }
            GUILayout.EndVertical();


            GUILayout.EndArea();
        }

        #endregion

        #region welcome

        void WelcomeWindow()
        {
            GUILayout.BeginHorizontal(EditorStyles.helpBox);
            GUILayout.Label("Welcome to Fantacode Studios!");

            if (GUILayout.Button("Import Project Settings"))
            {
                setupScript.ImportProjectSettings();
            }

            GUILayout.EndHorizontal();

            var iconStyle = new GUIStyle()
            {
                fixedHeight = 500,
                fixedWidth = windowRect.width - 8,
                padding = { left = 10, right = 10, top = 10, bottom = 10 }

            };
            EnsureSlidesRequested();
            DrawSlides(iconStyle);

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();

            DrawSocialMedia("Icons/youtube_icon", "Fanatcode Studios", ref youtubeIcon, "https://youtube.com/@fantacodestudios?si=zUXiCBf0tqW1vcGo");
            DrawSocialMedia("Icons/discord_icon", "Fanatcode Studios", ref discordicon, "https://discord.com/invite/9UP9uH6mKZ");
            DrawSocialMedia("Icons/youtube_icon", "GameDev Experiments", ref youtubeIcon, "https://youtube.com/@gamedevexperiments?si=kZVaykdzCbxpDbBx");

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(5);

            // Only gates the project-open popup — installing a new package always opens the window.
            bool dontShow = GetDontShowOnStart();
            bool newDontShow = EditorGUILayout.ToggleLeft(
                new GUIContent("Don't Show on Start", "When enabled, the window won't auto-open when you open the project. It still opens when you install a new FS package."),
                dontShow);
            if (newDontShow != dontShow)
                SetDontShowOnStart(newDontShow);
        }

        private void DrawSocialMedia(string iconPath, string label, ref Texture2D icon, string link)
        {

            GUILayout.BeginVertical();
            if (icon == null)
                icon = Resources.Load<Texture2D>(iconPath);


            GUILayout.BeginHorizontal();
            GUILayout.Space(windowRect.width / 6 - 20);
            if (GUILayout.Button(new GUIContent("", icon), GUILayout.Width(40), GUILayout.Height(40)))
            {
                Application.OpenURL(link);
            }
            GUILayout.EndHorizontal();


            var fsYoutubeTextLableStyle = new GUIStyle()
            {
                fixedHeight = 35,
                fixedWidth = windowRect.width / 3,
                alignment = TextAnchor.UpperCenter,
                fontStyle = FontStyle.Bold
            };
            GUILayout.Label(label, fsYoutubeTextLableStyle);
            GUILayout.EndVertical();
        }

        #endregion

        #region Slideshow

        // Remote slideshow shown at the top of the Welcome tab. Images are fetched from the
        // Fantacode Studios server. The API (api.php) returns
        // { status, directory, images:[ "images/1.png", ... ] } with paths relative to /slides/.
        // If the server can't be reached (or returns no images) we silently fall back to the
        // bundled default image (Resources/Icons/Main_img).
        const string slidesApiUrl = "https://www.fantacodestudios.com/slides/api.php";
        const string slidesBaseUrl = "https://www.fantacodestudios.com/slides/";
        const double slideDurationSeconds = 5.0;

        // Instance state (deliberately not cached across window opens): the slides are re-fetched
        // from the server every time the window is opened, and the textures are freed on close,
        // so what's shown always reflects the latest images on the server.
        readonly List<Texture2D> slideTextures = new List<Texture2D>();
        bool slidesRequested;
        bool slidesFailed;
        int currentSlideIndex;
        double lastSlideSwitchTime;

        [Serializable]
        private class SlidesApiResponse
        {
            public string status;
            public string directory;
            public string[] images;
        }

        // Kicks off the slides download once per session (retried on reopen after a failure).
        private void EnsureSlidesRequested()
        {
            if (slidesRequested) return;
            slidesRequested = true;
            slidesFailed = false;
            FetchSlides();
        }

        // CACHING: the images sit behind Cloudflare (Cache-Control max-age = 30 days). The API
        // returns image URLs already stamped with a ?v=<mtime> cache-buster, so a re-uploaded image
        // becomes a new URL and bypasses the CDN cache on its own — we therefore use those image
        // URLs verbatim. The API's *list* response, however, is served from the origin proxy cache,
        // so we append a unique param to THIS call to always pull a fresh list (and thus the latest
        // ?v= values). api.php accepts query strings; the static image paths are used as-is.
        private void FetchSlides()
        {
            string apiUrl = $"{slidesApiUrl}?cb={DateTime.UtcNow.Ticks}";
            var request = UnityWebRequest.Get(apiUrl);
            var operation = request.SendWebRequest();
            operation.completed += _ =>
            {
                try
                {
                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        OnSlidesFailed(request.error);
                        return;
                    }

                    var response = JsonUtility.FromJson<SlidesApiResponse>(request.downloadHandler.text);
                    if (response == null || response.status != "success" || response.images == null || response.images.Length == 0)
                    {
                        OnSlidesFailed("API returned no images.");
                        return;
                    }

                    DownloadSlideImages(response.images);
                }
                catch (Exception e)
                {
                    OnSlidesFailed(e.Message);
                }
                finally
                {
                    request.Dispose();
                }
            };
        }

        private void DownloadSlideImages(string[] relativePaths)
        {
            var downloaded = new Texture2D[relativePaths.Length];
            int pending = relativePaths.Length;

            for (int i = 0; i < relativePaths.Length; i++)
            {
                int index = i;
                string url = slidesBaseUrl + relativePaths[i].TrimStart('/');
                // Fetch raw bytes (not UnityWebRequestTexture) so we can build a mipmapped texture
                // ourselves -> clean, high-quality downscaling when the slide is shrunk to fit.
                var request = UnityWebRequest.Get(url);
                var operation = request.SendWebRequest();
                operation.completed += _ =>
                {
                    if (request.result == UnityWebRequest.Result.Success)
                        downloaded[index] = CreateSlideTexture(request.downloadHandler.data);
                    else
                        Debug.LogWarning($"[FS Welcome] Failed to download slide '{url}': {request.error}");

                    request.Dispose();

                    pending--;
                    if (pending == 0)
                        OnSlidesDownloaded(downloaded);
                };
            }
        }

        // Decodes JPG/PNG bytes into a full-resolution, mipmapped, trilinear-filtered texture.
        // DownloadHandlerTexture yields mip-less textures that alias and lose clarity when the
        // editor shrinks a ~1024px slide to fit the window. Copying the decoded pixels into a
        // texture created WITH a mip chain guarantees Unity builds mipmaps (LoadImage alone does
        // not reliably do so), giving a clean downscale that matches the source image.
        private static Texture2D CreateSlideTexture(byte[] data)
        {
            if (data == null || data.Length == 0)
                return null;

            // Decode at native resolution (readable, single mip).
            var decoded = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false);
            if (!decoded.LoadImage(data, markNonReadable: false))
            {
                DestroyImmediate(decoded);
                return null;
            }

            var texture = new Texture2D(decoded.width, decoded.height, TextureFormat.RGBA32, mipChain: true);
            texture.SetPixels32(decoded.GetPixels32());
            texture.filterMode = FilterMode.Trilinear;
            texture.anisoLevel = 4;
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.Apply(updateMipmaps: true, makeNoLongerReadable: true);

            DestroyImmediate(decoded);
            return texture;
        }

        private void OnSlidesDownloaded(Texture2D[] downloaded)
        {
            slideTextures.Clear();
            foreach (var texture in downloaded)
            {
                if (texture != null)
                    slideTextures.Add(texture);
            }

            if (slideTextures.Count == 0)
            {
                OnSlidesFailed("None of the slide images could be downloaded.");
                return;
            }

            currentSlideIndex = 0;
            lastSlideSwitchTime = EditorApplication.timeSinceStartup;
            SafeRepaint();
        }

        private void OnSlidesFailed(string error)
        {
            slidesFailed = true;
            slideTextures.Clear();
            Debug.LogWarning($"[FS Welcome] Could not load slides from server; showing default image. {error}");
            SafeRepaint();
        }

        // The download callbacks can fire after the window is closed; avoid repainting a
        // destroyed EditorWindow (Unity's overloaded == treats a destroyed object as null).
        private void SafeRepaint()
        {
            if (this != null)
                Repaint();
        }

        // Frees the downloaded slide textures when the window closes so nothing is cached between
        // opens — the next open fetches a fresh set from the server.
        private void DisposeSlides()
        {
            foreach (var texture in slideTextures)
            {
                if (texture != null)
                    DestroyImmediate(texture);
            }
            slideTextures.Clear();
            slidesRequested = false;
            slidesFailed = false;
            currentSlideIndex = 0;
        }

        // Draws the current slide (auto-advancing) or the default image while loading / on failure.
        private void DrawSlides(GUIStyle imageStyle)
        {
            if (welcomeWindowImg == null)
                welcomeWindowImg = Resources.Load<Texture2D>("Icons/Main_img");

            // Pick the texture to show: the current slide, or the default image while the slides
            // are still loading / the server was unreachable.
            Texture texture = welcomeWindowImg;
            if (slideTextures.Count > 0)
            {
                AdvanceSlide();
                if (currentSlideIndex >= slideTextures.Count)
                    currentSlideIndex = 0;
                if (slideTextures[currentSlideIndex] != null)
                    texture = slideTextures[currentSlideIndex];
            }

            // Reserve the same footprint the label used, then draw the image centered within it.
            // Drawing it ourselves gives us the exact on-screen image rect, so the overlaid nav
            // controls line up with the image regardless of its aspect ratio.
            Rect boxRect = GUILayoutUtility.GetRect(imageStyle.fixedWidth, imageStyle.fixedHeight);
            Rect contentRect = imageStyle.padding.Remove(boxRect);
            Rect imageRect = FitTextureRect(contentRect, texture);

            // Snap to whole pixels so the texture isn't sampled at a sub-pixel offset (which blurs
            // it under bilinear/trilinear filtering).
            imageRect = new Rect(
                Mathf.Round(imageRect.x), Mathf.Round(imageRect.y),
                Mathf.Round(imageRect.width), Mathf.Round(imageRect.height));

            if (Event.current.type == EventType.Repaint && texture != null)
                GUI.DrawTexture(imageRect, texture, ScaleMode.StretchToFill);

            // Navigation controls only make sense once several real slides are available.
            if (slideTextures.Count <= 1)
                return;

            DrawSlideArrows(imageRect);
            DrawSlideDots(imageRect);
        }

        // Returns the largest rect with the texture's aspect ratio that fits inside `container`,
        // centered — matching how the slide is drawn so the overlays can align to it.
        private static Rect FitTextureRect(Rect container, Texture texture)
        {
            if (texture == null || texture.width == 0 || texture.height == 0)
                return container;

            float scale = Mathf.Min(container.width / texture.width, container.height / texture.height);
            float width = texture.width * scale;
            float height = texture.height * scale;
            return new Rect(
                container.x + (container.width - width) * 0.5f,
                container.y + (container.height - height) * 0.5f,
                width, height);
        }

        // Jumps to a slide (wrapping around) and restarts the auto-advance timer.
        private void ShowSlide(int index)
        {
            int count = slideTextures.Count;
            if (count == 0) return;
            currentSlideIndex = ((index % count) + count) % count;
            lastSlideSwitchTime = EditorApplication.timeSinceStartup;
        }

        private void AdvanceSlide()
        {
            if (slideTextures.Count <= 1) return;

            double now = EditorApplication.timeSinceStartup;
            if (now - lastSlideSwitchTime >= slideDurationSeconds)
            {
                currentSlideIndex = (currentSlideIndex + 1) % slideTextures.Count;
                lastSlideSwitchTime = now;
            }
        }

        // Small clickable indicator dots overlaid on the bottom of the slideshow.
        private void DrawSlideDots(Rect imageRect)
        {
            int count = slideTextures.Count;
            if (count <= 1) return;

            const float dotSize = 10f;
            const float spacing = 6f;
            float totalWidth = count * dotSize + (count - 1) * spacing;
            float startX = imageRect.x + (imageRect.width - totalWidth) * 0.5f;
            float y = imageRect.yMax - dotSize - 12f;

            for (int i = 0; i < count; i++)
            {
                var dotRect = new Rect(startX + i * (dotSize + spacing), y, dotSize, dotSize);
                var prevColor = GUI.backgroundColor;
                GUI.backgroundColor = i == currentSlideIndex ? Color.cyan : new Color(1f, 1f, 1f, 0.4f);
                if (GUI.Button(dotRect, GUIContent.none))
                    ShowSlide(i);
                GUI.backgroundColor = prevColor;
            }
        }

        GUIStyle slideArrowStyle;

        // Previous / next scroll buttons overlaid on the left and right edges of the slideshow.
        private void DrawSlideArrows(Rect imageRect)
        {
            if (slideTextures.Count <= 1) return;

            if (slideArrowStyle == null)
            {
                slideArrowStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = 22,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
            }

            const float width = 30f;
            const float height = 44f;
            const float margin = 12f;
            float y = imageRect.y + (imageRect.height - height) * 0.5f;

            var leftRect = new Rect(imageRect.x + margin, y, width, height);
            var rightRect = new Rect(imageRect.xMax - width - margin, y, width, height);

            var prevColor = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0f, 0f, 0f, 0.5f);
            if (GUI.Button(leftRect, "<", slideArrowStyle))
                ShowSlide(currentSlideIndex - 1);
            if (GUI.Button(rightRect, ">", slideArrowStyle))
                ShowSlide(currentSlideIndex + 1);
            GUI.backgroundColor = prevColor;
        }

        #endregion

        #region Setup

        Editor characterPreview;
        public static GameObject characterModel;
        static GameObject currentPreviewObj;


        string playerAnimatorName = "Combined Controller";
        string aiAnimatorName = "Combined Controller AI";
        string animatorName => characterType == CharacterType.Player ? playerAnimatorName : aiAnimatorName;


        static CharacterType[] visibleValues;
        static string[] displayedNames;

        static void InitializeCharacterTypeOptions()
        {
            if (visibleValues != null) return; // Already initialized

            visibleValues = Enum.GetValues(typeof(CharacterType))
                .Cast<CharacterType>()
                .Where(v => v != CharacterType.None)
                .ToArray();

            displayedNames = visibleValues.Select(v => v.ToString()).ToArray();
        }

        private void SetupWindow()
        {
            GUI.backgroundColor = Color.black;
            GUILayout.BeginVertical(EditorStyles.helpBox);
            GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 18,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.3f, 0.6f, 0.8f) },
                hover = { textColor = new Color(0.3f, 0.6f, 0.8f) }
            };
            GUILayout.Space(10);
            GUILayout.Label("FS Systems Setup", headerStyle);
            GUILayout.Space(10);
            GUI.backgroundColor = Color.white;
            GUIStyle findButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12,
                fontStyle = FontStyle.BoldAndItalic,
                fixedHeight = 20,
                fixedWidth = 150,
                normal = { textColor = Color.black },
                hover = { textColor = Color.black }
            };


            Rect characterTypeRect = new Rect(8, 35, 150, 20);
            InitializeCharacterTypeOptions();
            EditorGUI.BeginChangeCheck();
            characterType = visibleValues[EditorGUI.Popup(characterTypeRect, "", (int)characterType, displayedNames)];
            if (EditorGUI.EndChangeCheck())
            {
                setupScript.FindSystem();
                if (characterType == CharacterType.Player) setupScript.EnableSystems(FSSystemsSetup.ThirdPersonControllerSystemSetup.systemName);
                else if(characterType == CharacterType.AI) setupScript.EnableSystems();
            }

            Rect buttonRect = new Rect(windowRect.width - 158, 35, 150, 20);
            //GUI.backgroundColor = new Color(0.2f, 0.4f, 0.8f, 0.8f);
            if (setupScript.CurrentFSSystemsForSetup.Count == 0 || GUI.Button(buttonRect, "Load Systems", findButtonStyle)) 
            {
                setupScript.FindSystem();
            }

            GUI.backgroundColor = Color.white;
            GUILayout.Space(10);
            GUILayout.EndVertical();

            GUILayout.BeginVertical(EditorStyles.helpBox);
            var systems = setupScript.CurrentFSSystemsForSetup.Where(s => s.Value.characterType == characterType).ToList();
            scrolPosition = GUILayout.BeginScrollView(scrolPosition, GUILayout.Height(225));

            foreach (var item in systems)
            {
                DrawFancyToggle(item.Value.displayName, ref item.Value.selected, item.Value.OnSelectionChanged);
            }

            GUILayout.EndScrollView();
            GUILayout.Space(10);

            GUILayout.EndVertical();
            GUILayout.Space(5);
            GUIContent textFieldLabel = new GUIContent("Animator Name", "The name for the new Animator Controller being created.");
            if(characterType == CharacterType.Player)
                playerAnimatorName = EditorGUILayout.TextField(textFieldLabel, playerAnimatorName);
            else if(characterType ==CharacterType.AI)
                aiAnimatorName = EditorGUILayout.TextField(textFieldLabel, aiAnimatorName);
            GUILayout.Space(5);
            DrawPreviewArea();

            // Button Container
            GUILayout.Space(10);
            EditorGUILayout.BeginHorizontal();

            // Reset Button 
            GUIStyle buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                fixedHeight = 30,
                normal = { textColor = Color.white },
                hover = { textColor = Color.yellow }
            };

            GUI.backgroundColor = new Color(0.6f, 0.2f, 0.2f, 0.8f); // Soft Red
            if (GUILayout.Button("Reset", buttonStyle, GUILayout.Width(position.width * 0.3f)))
            {
                if (setupScript == null) return;

                setupScript.EnableSystems(FSSystemsSetup.ThirdPersonControllerSystemSetup.systemName);
                HandleReset();
            }

            GUILayout.Space(10);


            GUI.backgroundColor = new Color(0.2f, 0.6f, 0.2f, 0.8f); // Soft Green
            if (GUILayout.Button("Setup", buttonStyle, GUILayout.ExpandWidth(true)))
            {
                HandleReset();
                HandleSetup();
            }


            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();

        }

        private void DrawPreviewArea()
        {
            SetWarningAndErrors();

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            var labelRect = GUILayoutUtility.GetRect(150, 20);
            var previewRect = GUILayoutUtility.GetRect(150, 230);

            // Draw background
            EditorGUI.DrawRect(previewRect, new Color(0.1f, 0.1f, 0.1f, 1f));

            // Draw preview or message
            if (characterModel != null)
            {
                // Draw preview
                if (characterPreview == null || currentPreviewObj != characterModel)
                {
                    characterPreview = Editor.CreateEditor(characterModel);
                    currentPreviewObj = characterModel;
                }
                if (!Application.isPlaying)
                    characterPreview.OnInteractivePreviewGUI(previewRect, EditorStyles.whiteLabel);

                // Draw validation overlays
                DrawValidationOverlays(previewRect);
            }
            else
            {
                // Draw empty state message
                DrawEmptyPreviewMessage(previewRect);
            }

            if (hintStyle == null)
            {
                hintStyle = new GUIStyle(GUI.skin.label);
                hintStyle.fontStyle = FontStyle.Italic;
                hintStyle.normal.textColor = Color.white;
            }
            EditorGUI.LabelField(labelRect, "To change the character model, drag and drop them in the window below.", hintStyle);

            // Handle drag & drop events
            HandlePreviewDragAndDrop(previewRect);

            EditorGUILayout.EndVertical();
        }

        private void DrawValidationOverlays(Rect previewRect)
        {
            if (!hasAnimator || !isHumanoid || !validAvathar)
            {
                // Semi-transparent dark background
                EditorGUI.DrawRect(previewRect, new Color(0, 0, 0, 0.7f));

                GUIStyle overlayStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    wordWrap = true
                };

                float yOffset = 0;
                if (!hasAnimator)
                {
                    overlayStyle.normal.textColor = Color.red;
                    EditorGUI.LabelField(new Rect(previewRect.x, previewRect.y + yOffset, previewRect.width, 30),
                        "Missing Animator Component", overlayStyle);
                    yOffset += 30;
                }
                else if (!validAvathar)
                {
                    overlayStyle.normal.textColor = Color.yellow;
                    EditorGUI.LabelField(new Rect(previewRect.x, previewRect.y + yOffset, previewRect.width, 30),
                        "Invalid Humanoid Avatar", overlayStyle);
                }
                else if (!isHumanoid)
                {
                    overlayStyle.normal.textColor = new Color(1f, 0.7f, 0f);
                    EditorGUI.LabelField(new Rect(previewRect.x, previewRect.y + yOffset, previewRect.width, 30),
                        "Set your model animation type to Humanoid", overlayStyle);
                    yOffset += 30;
                }
            }
        }

        private void DrawEmptyPreviewMessage(Rect previewRect)
        {
            GUIStyle messageStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                fontSize = 12,
                normal = { textColor = new Color(0.7f, 0.7f, 0.7f) }
            };

            if (Application.isPlaying)
            {
                EditorGUI.LabelField(previewRect, "Preview not available\nduring Play Mode", messageStyle);
            }
            else
            {
                // Draw drag-drop prompt
                EditorGUI.LabelField(previewRect, "Drag & Drop\nCharacter Model Here", messageStyle);

                // Draw icon
                float iconSize = 32f;
                var iconRect = new Rect(
                    previewRect.x + (previewRect.width - iconSize) * 0.5f,
                    previewRect.y + (previewRect.height - iconSize) * 0.3f,
                    iconSize, iconSize);
                GUI.DrawTexture(iconRect, EditorGUIUtility.IconContent("d_GameObject Icon").image);
            }
        }

        private void HandlePreviewDragAndDrop(Rect previewRect)
        {
            Event currentEvent = Event.current;

            switch (currentEvent.type)
            {
                case EventType.DragUpdated:
                case EventType.DragPerform:
                    if (!previewRect.Contains(currentEvent.mousePosition))
                        return;

                    // Check if the drag data contains a valid GameObject
                    bool isValid = DragAndDrop.objectReferences
                        .Any(obj => obj is GameObject go && go.GetComponent<Animator>() != null);

                    DragAndDrop.visualMode = isValid ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;

                    if (currentEvent.type == EventType.DragPerform && isValid)
                    {
                        DragAndDrop.AcceptDrag();

                        foreach (var obj in DragAndDrop.objectReferences)
                        {
                            if (obj is GameObject go)
                            {
                                characterModel = go;
                            }
                            Repaint();
                            break;
                        }
                    }

                    currentEvent.Use();
                    break;
            }
        }


        private void DrawFancyToggle(string label, ref bool value, Action<bool> onSelectionChanged)
        {
            Rect rect = GUILayoutUtility.GetRect(200, 40, GUILayout.ExpandWidth(true));
            bool isMouseOver = rect.Contains(Event.current.mousePosition);

            Color bgColor = value ? new Color(0.3f, 0.6f, 0.3f, 0.4f) : new Color(0.6f, 0.3f, 0.3f, 0.4f);
            Color hoverColor = new Color(1f, 1f, 1f, 0.1f);
            EditorGUI.DrawRect(rect, bgColor);
            if (isMouseOver)
            {
                EditorGUI.DrawRect(rect, hoverColor);
            }

            // Toggle box
            Rect toggleRect = new Rect(rect.x + 10, rect.y + 10, 20, 20);
            EditorGUI.BeginChangeCheck();
            value = GUI.Toggle(toggleRect, value, GUIContent.none);
            if(EditorGUI.EndChangeCheck())
            {
                onSelectionChanged?.Invoke(value);
            }

            // Label
            GUIStyle labelStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.9f, 0.9f, 0.9f) } // Light Gray
            };
            GUI.Label(new Rect(rect.x + 40, rect.y + 10, rect.width - 50, rect.height - 20), label, labelStyle);

            if (Event.current.type == EventType.MouseDown && isMouseOver)
            {
                value = !value;
                onSelectionChanged?.Invoke(value);
                Event.current.Use();
            }
        }
        int counter = 1;
        private void HandleSetup()
        {
            if (!ValidForCreateCharacter || setupScript == null) return;

            string sourcePath = AssetDatabase.GetAssetPath(Resources.Load("AnimatorControllers/Third Person Controller"));
            string destinationPath = $"{Path.GetDirectoryName(sourcePath)}/{animatorName}.controller";
            string newPath = destinationPath;
            int counter = 1;
            while (AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(newPath) != null)
            {
                var newName = $"{animatorName} {counter}";
                newPath = $"{Path.GetDirectoryName(sourcePath)}/{newName}.controller";
                counter++;
            }
            destinationPath = newPath; 
            Debug.Log(destinationPath);
            //string destinationPath = "Assets/Combined Controller.controller";
            AssetDatabase.CopyAsset(sourcePath, destinationPath);
            AssetDatabase.Refresh();

            UnityEditor.Animations.AnimatorController CombinedController = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(destinationPath);
            AnimatorMergerUtility animatorMergerUtility = new AnimatorMergerUtility(CombinedController, CombinedController);

            GameObject characterObject = null;
            if (characterType == CharacterType.Player)
            {
                (var player, var model, var newFSSystemObject) = SetupPlayer();
                characterObject = player;
                characterObject.GetComponent<Animator>().avatar = characterModel.GetComponent<Animator>().avatar;
                characterObject.GetComponent<Animator>().runtimeAnimatorController = CombinedController;
                foreach (var item in setupScript.CurrentFSSystemsForSetup)
                {
                    if (item.Value.selected)
                    {
                        // Parent object of the controller
                        var FSSystemObjectPrefab = setupScript.CopyComponentsAndAnimControllerFromPrefab(item.Value.prefabName, animatorMergerUtility, player);


                        if (item.Value.characterType == CharacterType.Player && item.Value.uiPrefabNames != null && item.Value.uiPrefabNames.Length > 0)
                        {
                            var gameUI = newFSSystemObject.transform.GetComponentInChildren<FS_GameUI>()?.transform;
                            foreach (var uiPrefabName in item.Value.uiPrefabNames)
                            {
                                if (string.IsNullOrEmpty(uiPrefabName)) continue;
                                GameObject uiPrefab = Resources.Load<GameObject>(uiPrefabName);
                                if (uiPrefab == null) continue;

                                if (gameUI.Find(uiPrefab.name) != null)
                                    continue;

                                var mc = PrefabUtility.InstantiatePrefab(uiPrefab, gameUI);
                                mc.name = uiPrefab.name;
                            }
                        }

                        item.Value.extraSetupActionPlayer?.Invoke(player, FSSystemObjectPrefab, newFSSystemObject);

                    }
                }

                // Place the Locomotion Mobile Controller at the top of the UI hierarchy
                // (first sibling). Done after all systems have added their UI so it stays
                // first regardless of system processing order. Note: a first sibling is
                // drawn first, i.e. behind later siblings — which is what a full-screen
                // camera-swipe area wants, so the on-screen buttons above it stay tappable.
                var gameUIComponent = newFSSystemObject.transform.GetComponentInChildren<FS_GameUI>();
                if (gameUIComponent != null)
                {
                    var locomotionMobileController = gameUIComponent.transform.Find("Locomotion Mobile Controller");
                    if (locomotionMobileController != null)
                        locomotionMobileController.SetAsFirstSibling();
                }

                var playerController = player.GetComponent<PlayerController>();
                playerController.managedScripts = player.GetComponents<SystemBase>().ToList().OrderByDescending(x => x.Priority).ToList();
            }
            else if (characterType == CharacterType.AI)
            {
                characterObject = SetupAI();
                characterObject.GetComponent<Animator>().avatar = characterModel.GetComponent<Animator>().avatar;
                characterObject.GetComponent<Animator>().runtimeAnimatorController = CombinedController;
                foreach (var item in setupScript.CurrentFSSystemsForSetup)
                {
                    if (item.Value.selected)
                    {
                        var prefabObject = setupScript.CopyComponentsAndAnimControllerFromPrefab(item.Value.prefabName, animatorMergerUtility, characterObject);
                        item.Value.extraSetupActionAI?.Invoke(characterObject, prefabObject);
                    }
                }

                var selectedAISystemNames = setupScript.CurrentFSSystemsForSetup
                    .Where(kv => kv.Value.selected && kv.Value.characterType == CharacterType.AI)
                    .Select(kv => kv.Value.systemName)
                    .ToList();
                RunAICharacterSetupHandlers(characterObject, selectedAISystemNames);
            }

            Debug.Log($"A new Animator Controller named {animatorName} has been created and stored in the {destinationPath} directory.");
            

            Selection.activeObject = characterObject;
            SceneView sceneView = SceneView.lastActiveSceneView;
            sceneView.Focus();
            sceneView.LookAt(characterObject.transform.position);
        }

        // Discovers all IAICharacterSetupHandler implementations and invokes them. Lets modules
        // outside Third Person Controller (e.g. the AI System) hook into AI character setup
        // without this window taking a hard reference to them.
        private static void RunAICharacterSetupHandlers(GameObject aiObject, List<string> selectedSystemNames)
        {
            var handlerTypes = TypeCache.GetTypesDerivedFrom<IAICharacterSetupHandler>();
            foreach (var type in handlerTypes)
            {
                if (type.IsAbstract || type.IsInterface) continue;
                try
                {
                    var handler = (IAICharacterSetupHandler)Activator.CreateInstance(type);
                    handler.OnAICharacterSetup(aiObject, selectedSystemNames);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"IAICharacterSetupHandler '{type.FullName}' threw: {ex}");
                }
            }
        }

        private void HandleReset()
        {
            if (setupScript == null) return;

            Component[] components = setupScript.GetComponents<Component>();

            foreach (Component component in components)
            {
                // Skip the component we want to keep and the Transform component
                if (component is FSSystemsSetup || component is Transform)
                    continue;

                // Destroy the component
                DestroyImmediate(component);
            }

            Transform parentTransform = setupScript.transform;

            for (int i = parentTransform.childCount - 1; i >= 0; i--)
            {
                Transform child = parentTransform.GetChild(i);
                DestroyImmediate(child.gameObject);
            }
        }

        public static void CopyComponents(GameObject sourcePrefab, GameObject targetPrefab)
        {
            Component[] components = sourcePrefab.GetComponents<Component>();

            foreach (Component sourceComp in components)
            {
                if (sourceComp == null) continue; // Skip missing components

                Type componentType = sourceComp.GetType();
                if (targetPrefab.GetComponent(componentType) != null) continue;

                System.Type type = sourceComp.GetType();
                Component targetComp = targetPrefab.GetComponent(type);

                if (targetComp == null)
                {
                    targetComp = targetPrefab.AddComponent(type);
                    ComponentUtility.CopyComponent(sourceComp);
                    ComponentUtility.PasteComponentValues(targetComp);
                }

            }
        }

        static void CreateSetupScript()
        {
            if (setupScript == null)
            {
                GameObject go = new GameObject("FS System");
                setupScript = go.AddComponent<FSSystemsSetup>();
                setupScript.CurrentFSSystemsForSetup = new Dictionary<string, FSSystemInfo>();

                setupScript.gameObject.hideFlags = HideFlags.HideInHierarchy;
            }
        }

        private (GameObject, GameObject, GameObject) SetupPlayer()
        {
            GameObject prefab = Resources.Load<GameObject>("Locomotion Controller");
            GameObject instance = Instantiate(prefab);
            instance.name = "FS Player";

            var player = instance.GetComponentInChildren<PlayerController>().gameObject;
            var camera = instance.GetComponentInChildren<CameraController>();

            camera.followTarget = player.transform;
            camera.firstPersonCamera.defaultSettings.overridedFollowTarget = player.transform;
            camera.thirdPersonCamera.defaultSettings.overridedFollowTarget = player.transform;
            GameObject model = Instantiate(characterModel, Vector3.zero, Quaternion.identity);
            model.transform.SetParent(player.transform);

            var animator = model.GetComponent<Animator>();

            string headTargetName = "Head Follow Target";
            var headTransform = animator.GetBoneTransform(HumanBodyBones.Head);
            var headTarget = new GameObject(headTargetName);
            headTarget.transform.SetParent(headTransform);
            headTarget.transform.SetLocalPositionAndRotation(new Vector3(0, .18f, .08f), Quaternion.identity);

            camera.firstPersonCamera.defaultSettings.overridedFollowTarget = headTarget.transform;
            camera.thirdPersonCamera.defaultSettings.overridedFollowTarget = player.transform;

            AddFootTrigger(animator);
            SetItemHandler(animator);

            if (player.layer != LayerMask.NameToLayer("Player"))
                player.layer = LayerMask.NameToLayer("Player");

            player.AddComponent<CharacterID>();

            return (player, model, instance);
        }

        private GameObject SetupAI()
        {
            GameObject prefab = Resources.Load<GameObject>("AI Controller");
            GameObject aiObject = Instantiate(prefab, Vector3.zero, Quaternion.identity);
            aiObject.name = "FS AI";

            GameObject model = Instantiate(characterModel, Vector3.zero, Quaternion.identity);
            model.transform.SetParent(aiObject.transform);

            var animator = model.GetComponent<Animator>();

            AddFootTrigger(animator);
            SetItemHandler(animator);

            if (aiObject.layer != LayerMask.NameToLayer("Enemy"))
                aiObject.layer = LayerMask.NameToLayer("Enemy");

            aiObject.AddComponent<CharacterID>();

            return aiObject;
        }

        private void AddFootTrigger(Animator animator)
        {
            if (!animator.isHuman) return;

            var footTriggerPrefab = (GameObject)Resources.Load("FootTrigger");
            var rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot).transform;
            var leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot).transform;
            var rightCollider = PrefabUtility.InstantiatePrefab(footTriggerPrefab, rightFoot) as GameObject;
            var leftCollider = PrefabUtility.InstantiatePrefab(footTriggerPrefab, leftFoot) as GameObject;
            rightCollider.transform.localPosition = Vector3.zero;
            leftCollider.transform.localPosition = Vector3.zero;
            if ((rightCollider.layer != LayerMask.NameToLayer("FootTrigger")))
                rightCollider.layer = LayerMask.NameToLayer("FootTrigger");
            if ((leftCollider.layer != LayerMask.NameToLayer("FootTrigger")))
                leftCollider.layer = LayerMask.NameToLayer("FootTrigger");

        }

        public static void SetItemHandler(Animator animator)
        {
            // Check for right hand item handler
            Transform rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            Transform rightHandHandler = rightHand.Find("Right Hand Item Handler");

            if (rightHandHandler == null)
            {
                var rh = new GameObject("Right Hand Item Handler");
                rh.transform.parent = rightHand;
                rh.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                rh.AddComponent<EquippableItemHolder>();
            }

            // Check for left hand item handler
            Transform leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            Transform leftHandHandler = leftHand.Find("Left Hand Item Handler");

            if (leftHandHandler == null)
            {
                var lh = new GameObject("Left Hand Item Handler");
                lh.transform.parent = leftHand;
                lh.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                lh.AddComponent<EquippableItemHolder>();
            }
        }


        #region warning

        bool isHumanoid;
        bool validAvathar;
        bool hasAnimator;
        bool ValidForCreateCharacter => hasAnimator && isHumanoid && validAvathar;
        void SetWarningAndErrors()
        {
            if (characterModel != null)
            {
                var animator = characterModel.GetComponent<Animator>();
                if (animator != null)
                {
                    hasAnimator = true;
                    isHumanoid = animator.isHuman;
                    validAvathar = animator.avatar != null && animator.avatar.isValid;
                }
                else
                    hasAnimator = isHumanoid = validAvathar = false;
            }
        }

        #endregion

        #endregion

        #region Packages

        private void PackageWindow()
        {
            if (setupScript.CurrentFSSystemsForSetup.Count == 0)
            {
                setupScript.FindSystem();
            }

            GUI.backgroundColor = Color.black;
            GUILayout.BeginVertical(EditorStyles.helpBox);
            GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 18,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.3f, 0.6f, 0.8f) },
                hover = { textColor = new Color(0.3f, 0.6f, 0.8f) }
            };
            GUILayout.Space(10);
            GUILayout.Label("FS Systems", headerStyle);
            GUILayout.Space(10);
            GUI.backgroundColor = Color.white;
            GUILayout.EndVertical();


            foreach (var package in FSPackageData.packages)
            {
                GUILayout.Space(5);
                ShowPackages(package);
            }
        }

        Texture2D youtubeIcon;
        Texture2D documentIcon;
        Texture2D welcomeWindowImg;
        Texture2D discordicon;

        private void ShowPackages(FSPackageInfo info, float leftPadding = 0f)
        {
            Rect rect = GUILayoutUtility.GetRect(200, 40, GUILayout.ExpandWidth(true));
            rect.x += leftPadding;
            rect.width -= leftPadding;

            var iconRect = new Rect(rect.x + 5, rect.y + 5, rect.height - 10, rect.height - 10);
            var youtubeButtonRect = new Rect(rect.x + rect.width - 95, rect.y + 5, 30, 30);
            var docButtonRect = new Rect(youtubeButtonRect.x + 35, rect.y + 5, 30, 30);
            var storeButtonRect = new Rect(rect.x + rect.width - 25, rect.y + 5, 20, 30);
            bool isMouseOver = rect.Contains(Event.current.mousePosition) && !youtubeButtonRect.Contains(Event.current.mousePosition)
                && !docButtonRect.Contains(Event.current.mousePosition) && !storeButtonRect.Contains(Event.current.mousePosition);


            bool systemIsInstalled = false;
            foreach (var system in setupScript.AllInstalledSystems.Values)
            {
                if (system.systemName == info.packageName)
                {
                    systemIsInstalled = true;
                    break;
                }

            }

            Color bgColor = systemIsInstalled ? new Color(0.3f, 0.6f, 0.3f, 0.4f) : new Color(0.6f, 0.3f, 0.3f, 0.4f);
            Color hoverColor = new Color(1f, 1f, 1f, 0.1f);
            EditorGUI.DrawRect(rect, bgColor);
            if (isMouseOver)
            {
                EditorGUI.DrawRect(rect, hoverColor);
            }

            //if (Event.current.type == EventType.MouseDown && isMouseOver)
            //{
            //    FSPackageData.UnSelectAll();
            //    info.opened = !info.opened;
            //    Event.current.Use();
            //}



            GUIStyle labelStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                padding = { left = (int)(iconRect.x + iconRect.width) + 5 - (int)leftPadding },
                normal = { textColor = new Color(0.9f, 0.9f, 0.9f) },
            };
            if (info.icon == null)
                info.icon = Resources.Load<Texture2D>(info.iconPath);
            GUI.Label(iconRect, info.icon);
            GUI.Label(rect, info.packageName, labelStyle);



            //GUIStyle installedTextstyle = new GUIStyle(EditorStyles.label)
            //{
            //    fontSize = 10,
            //    fontStyle = FontStyle.Italic,
            //    alignment = TextAnchor.MiddleCenter,
            //    normal = { textColor = systemIsInstalled ? Color.green : Color.red },
            //    hover = { textColor = systemIsInstalled ? Color.green : Color.red }
            //};
            //var r = new Rect(rect.width - 200, rect.y, 100, rect.height);
            //GUI.Label(r, systemIsInstalled?"Installed":"Not Installed", installedTextstyle);

            if (youtubeIcon == null)
                youtubeIcon = Resources.Load<Texture2D>("Icons/youtube_icon");

            if (documentIcon == null)
                documentIcon = Resources.Load<Texture2D>("Icons/document_icon");


            if (GUI.Button(youtubeButtonRect, new GUIContent("", youtubeIcon)))
            {
                Application.OpenURL(info.youtubeLink);
            }
            if (GUI.Button(docButtonRect, new GUIContent("", documentIcon)))
            {
                Application.OpenURL(info.documentationLink);
            }

            if (GUI.Button(storeButtonRect, new GUIContent(">")))
            {
                Application.OpenURL(info.assetLink);
            }

            foreach (var addOn in info.addOns)
            {
                ShowPackages(addOn, rect.x + 20);
            }
        }

        #endregion

        #region Dependencies
        void DependenciesWindow()
        {
            GUI.backgroundColor = Color.black;
            GUILayout.BeginVertical(EditorStyles.helpBox);
            GUIStyle headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 18,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.3f, 0.6f, 0.8f) },
                hover = { textColor = new Color(0.3f, 0.6f, 0.8f) }
            };
            GUILayout.Space(10);
            GUILayout.Label("Dependencies", headerStyle);
            GUILayout.Space(10);
            GUI.backgroundColor = Color.white;
            GUILayout.EndVertical();
            AddOnModules();

        }
        public const string inputsystem = "inputsystem";
        private void AddOnModules()
        {
            var _inputsystem = false;

            var sybmols = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup)).Split(';');
            for (int i = 0; i < sybmols.Length; i++)
            {
                if (string.Equals(inputsystem, sybmols[i].Trim()))
                    _inputsystem = true;
            }

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.LabelField(new GUIContent("Addon Module :"), EditorStyles.boldLabel);
            GUILayout.Space(4);

            GUILayout.BeginHorizontal();
            var _input = EditorGUILayout.Toggle("", _inputsystem, GUILayout.Width(17), GUILayout.Height(17));
            EditorGUILayout.LabelField(new GUIContent("New Input System", "Enabling this feature allows support for the New Input System. Ensure that you have installed the New InputSystem package before enabling this feature"), GUILayout.Width(110));
            GUILayout.EndHorizontal();

            var sybmolValueChanged = EditorGUI.EndChangeCheck();

            if (_input != _inputsystem)
            {
                if (_input)
                {
                    if (EditorUtility.DisplayDialog("New Input System", "Enabling this feature allows support for the New Input System. Ensure that you have installed the New InputSystem package before enabling this feature", "OK", "Cancel"))
                    {
                        ScriptingDefineSymbolController.ToggleScriptingDefineSymbol(inputsystem, _input);
                    }
                    else
                        sybmolValueChanged = false;
                }
                else
                    ScriptingDefineSymbolController.ToggleScriptingDefineSymbol(inputsystem, _input);
            }


            if (sybmolValueChanged)
                ScriptingDefineSymbolController.ReimportScripts();

        }
        #endregion

        static void SetWindowHeight(float height)
        {
            //if(selectedTab == 1 && setupScript != null && setupScript.FSSystems.Count > 0)
            //    height += (setupScript.FSSystems.Count - 1) * 40;
            if (window == null)
                window = GetWindow<FSSystemsSetupEditorWindow>("FS Systems");
            window.minSize = new Vector2(696, height);
            window.maxSize = new Vector2(696, height);
        }

        public static void SetTab(int tab)
        {
            selectedTab = tab;
            if (window != null)
            {
                window.Repaint();
            }
        }
    }
}
#endif
