using System.Collections.Generic;
using FS_Util;
using UnityEditor;
using UnityEngine;

namespace FS_InteractionSystem
{
    /// <summary>
    /// Scene + inspector editor for <see cref="EnterExitHotspot"/> and (via the <c>true</c> flag)
    /// every subclass that doesn't provide its own.
    ///
    /// Scene: the enter points, the pull-out points and the authored occupant pose are drawn as solid
    /// markers with facing arrows and real Move / Rotate manipulators, through
    /// <see cref="SceneView.duringSceneGui"/> — so they stay visible and usable even when the Scene
    /// view's Gizmos toggle is OFF. Interaction points snap to the nearest surface on mouse release;
    /// the occupant pose deliberately does not (a pose inside a locker lives in geometry, not on it).
    ///
    /// Inspector: fields are grouped into collapsible, colour-accented sections. Occupant Placement
    /// carries the Copy From Occupant / Paste pair — the pose is tuned live in play mode, copied to
    /// EditorPrefs (which survive the play-mode exit that discards scene edits) and pasted back
    /// afterwards. Anything a section didn't claim is collected under "Advanced" so nothing is ever
    /// hidden by accident. The one
    /// deliberate exception is Align To Hotspot: this spot forces it off in Awake, so exposing it
    /// would only offer a setting that never takes effect.
    ///
    /// Subclasses extend both halves — <see cref="DrawExtraSets"/> (scene),
    /// <see cref="DrawExtraInspector"/> (inspector) and <see cref="DrawRuntimeInfo"/> (play-mode
    /// readout) — and override <see cref="Subtitle"/> to describe themselves.
    /// </summary>
    [CustomEditor(typeof(EnterExitHotspot), true)]
    public class EnterExitHotspotEditor : Editor
    {
        // ── Accent colours ───────────────────────────────────────────────────────────
        // Shared by the scene gizmos and the inspector section bars, so a colour means the same
        // thing in both places.
        protected static readonly Color EnterAccent    = new Color(0.35f, 0.82f, 0.92f);   // cyan
        protected static readonly Color PullOutAccent  = new Color(1.00f, 0.55f, 0.25f);   // orange
        protected static readonly Color OccupantAccent = new Color(0.62f, 0.85f, 0.45f);   // green
        protected static readonly Color NeutralAccent  = new Color(0.62f, 0.67f, 0.76f);   // grey-blue

        // Forced to false in EnterExitHotspot.Awake — claimed so it never surfaces in "Advanced",
        // and never drawn.
        static readonly string[] HiddenProps = { "alignToHotspot" };

        // Inspector state.
        readonly HashSet<string> drawn = new HashSet<string>();
        readonly Dictionary<string, bool> sectionOpen = new Dictionary<string, bool>();
        GUIStyle titleStyle, boldFoldout;

        // Tracks the placement toggle across inspector passes so the pose can be seeded the moment it
        // is switched on (see SeedOccupantPoseOnEnable).
        bool lastPlacementEnabled;

        // Registered only while a spot is selected. Drawing through duringSceneGui rather than the
        // gated OnSceneGUI is what keeps the markers and handles alive with Gizmos switched off.
        protected virtual void OnEnable()  { SceneView.duringSceneGui += DuringSceneGui; }
        protected virtual void OnDisable() { SceneView.duringSceneGui -= DuringSceneGui; }

        // ══ Scene ════════════════════════════════════════════════════════════════════

        void DuringSceneGui(SceneView sceneView)
        {
            var comp = target as EnterExitHotspot;
            if (comp == null) return;

            if (Application.isPlaying) sceneView.Repaint();   // track live pivots in play mode

            if (comp.useStoppingDistance)
            {
                InteractionPointGizmo.DrawStoppingDisc(comp.GetPivotPosition(), comp.stoppingDistance, EnterAccent, "");
            }
            else
            {
                DrawSet(comp, comp.interactionPoints, EnterAccent, "Enter");
                if (comp.pullOutEnabled)
                    DrawSet(comp, comp.pullOutInteractionPoints, PullOutAccent, "Pull Out");
                DrawExtraSets(comp);
            }

            DrawOccupantPose(comp);

            // Snap the moved point to the nearest surface once the drag is released.
            InteractionPointGizmo.ProcessDeferredSnap(sceneView);
        }

        /// <summary>Subclasses draw their extra interaction-point sets here.</summary>
        protected virtual void DrawExtraSets(EnterExitHotspot comp) { }

        /// <summary>
        /// Draws one named set of interaction points with Move / Rotate handles. Edits are written
        /// back through the hotspot's own offset space, so the authored value round-trips exactly.
        /// </summary>
        protected void DrawSet(InteractionHotspot comp, List<InteractionPoint> points, Color color, string label)
        {
            if (points == null) return;

            bool editable = !Application.isPlaying;
            for (int i = 0; i < points.Count; i++)
            {
                var point = points[i];
                GetPose(comp, point, out Vector3 pos, out Quaternion rot);

                InteractionPointGizmo.DrawPoint(
                    comp, comp.transform,
                    pos, rot,
                    w => point.positionOffset = comp.WorldToOffsetSpace(w),
                    r => point.facingDirection = comp.WorldToFacingDirection(r),
                    color, $"{label} [{i}]", editable, snapToSurface: true);
            }
        }

        protected static void GetPose(InteractionHotspot comp, InteractionPoint point, out Vector3 pos, out Quaternion rot)
        {
            // The same frame while editing and while playing, so a point authored against a spot
            // inside a moving body doesn't draw in one place and resolve somewhere else on entering
            // play. Play mode additionally shows the ground-snapped result.
            pos = Application.isPlaying ? comp.GetInteractionPosition(point) : comp.GetInteractionPositionRaw(point);
            rot = comp.GetInteractionRotation(point);
        }

        /// <summary>
        /// Draws the authored occupant pose — where the character ends up once inside. Deliberately
        /// NOT surface-snapped: this pose sits inside a locker or a bin, so snapping it down onto the
        /// nearest collider would drop the occupant to the floor.
        ///
        /// Only a marker in play mode: edits made then are discarded on exit, and the inspector's
        /// Copy From Occupant / Paste pair is the supported way to capture a pose you tuned live.
        /// </summary>
        void DrawOccupantPose(EnterExitHotspot comp)
        {
            if (!comp.useCustomOccupantPlacement) return;

            comp.GetOccupantPose(out Vector3 pos, out Quaternion rot);

            InteractionPointGizmo.DrawMarker(pos, rot, OccupantAccent,
                HandleUtility.GetHandleSize(pos) * 0.12f);

            if (Event.current.type == EventType.Repaint)
            {
                Handles.color = OccupantAccent;
                Handles.Label(pos + Vector3.up * 0.35f, "Occupant");
            }

            if (Application.isPlaying) return;

            // One manipulator at a time, following the active tool — a full rotation gizmo drawn on
            // top of the position arrows at the same point is unpickable in practice.
            EditorGUI.BeginChangeCheck();
            Vector3 newPos = pos;
            Quaternion newRot = rot;

            if (Tools.current == Tool.Rotate)
                newRot = Handles.RotationHandle(rot, pos);
            else
                newPos = Handles.PositionHandle(
                    pos, Tools.pivotRotation == PivotRotation.Local ? rot : Quaternion.identity);

            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(comp, "Move Occupant Pose");
                comp.SetOccupantPoseFromWorld(newPos, newRot);
                EditorUtility.SetDirty(comp);
            }
        }

        // ══ Inspector ════════════════════════════════════════════════════════════════

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            drawn.Clear();
            drawn.Add("m_Script");   // hidden — the title card names the component instead
            Claim(HiddenProps);      // claimed but never drawn

            float prevLabelWidth = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = Mathf.Max(prevLabelWidth, 168f);   // consistent label column

            DrawTitleCard();

            Section("Enter / Exit", EnterAccent, "enterClip", "idleClip","interactionLabel", "snapToPosition",
                    "useStoppingDistance", "stoppingDistance", "interactionPoints", "exitClip", "exitLabel");

            DrawOccupantPlacementSection();

            FeatureSection("Pull Out", PullOutAccent, "pullOutEnabled",
                "Another character can forcibly pull this spot's occupant out.",
                "pullOutWhenEnter", "pullerClip", "pulledClip", "playOpenAnimationOnPullOut",
                "pullOutOpenTarget", "pullOutOpenClip", "pullOutInteractionPoints", "pullOutLabel");

            DrawExtraInspector();

            // Section("Interaction", NeutralAccent);

            Section("Events", NeutralAccent, "OnStartInteractionEvent", "OnEndInteractionEvent");

            DrawAdvanced();
            DrawRuntimeStatus();

            EditorGUIUtility.labelWidth = prevLabelWidth;
            serializedObject.ApplyModifiedProperties();

            // After the apply, so the component's fields already reflect this pass's edits.
            SeedOccupantPoseOnEnable(target as EnterExitHotspot);
        }

        /// <summary>Subclasses add their own sections here (drawn just before "Interaction").</summary>
        protected virtual void DrawExtraInspector() { }

        /// <summary>Subclasses append extra live info to the play-mode status box.</summary>
        protected virtual void DrawRuntimeInfo(EnterExitHotspot comp) { }

        /// <summary>One-line description shown under the title. Subclasses override.</summary>
        protected virtual string Subtitle => "A spot a character can enter, occupy and exit.";

        // ── Occupant placement ────────────────────────────────────────────────────────

        /// <summary>
        /// The optional authored pose for the character once it is inside, plus a hint naming the
        /// frame the numbers are relative to.
        /// </summary>
        void DrawOccupantPlacementSection()
        {
            var comp = target as EnterExitHotspot;
            var enableProp = serializedObject.FindProperty("useCustomOccupantPlacement");
            if (comp == null || enableProp == null) return;

            string[] props = { "occupantLocalPosition", "occupantLocalRotation" };
            drawn.Add("useCustomOccupantPlacement");
            Claim(props);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (Header("Occupant Placement", OccupantAccent, enableProp, true))
                {
                    EditorGUILayout.Space(2);
                    if (enableProp.boolValue)
                    {
                        var frame = comp.OccupantPlacementFrame;
                        EditorGUILayout.LabelField(
                            $"Pose the occupant is placed at, local to {(frame != null ? frame.name : "this spot")}. " +
                            "Drag the green handle in the Scene view, or tune it live: enter play mode, " +
                            "move the occupying character into place, press Copy, exit play mode, then Paste.",
                            EditorStyles.wordWrappedMiniLabel);
                        DrawProps(props);
                        EditorGUILayout.Space(4);
                        DrawCopyPasteRow(comp);
                    }
                    else
                    {
                        EditorGUILayout.LabelField(
                            "Enable to place the occupant at an exact pose instead of leaving it where " +
                            "the enter animation's root motion landed.",
                            EditorStyles.wordWrappedMiniLabel);
                    }
                }
            }
            EditorGUILayout.Space(2);
        }

        // ── Copy / Paste ──────────────────────────────────────────────────────────────
        // The whole point is to survive the play-mode exit that discards scene edits, so the copied
        // pose goes to EditorPrefs rather than to any object in the scene.
        //
        // The keys deliberately match the ones OccupancyHotspotEditor uses: a pose copied from an
        // occupancy spot (a vehicle seat) can be pasted straight into one of these, and vice versa.
        const string CopyKeyHas = "FS.OccupantPose.Has";
        const string CopyKeyPos = "FS.OccupantPose.Pos";
        const string CopyKeyRot = "FS.OccupantPose.Rot";
        const string CopyKeySrc = "FS.OccupantPose.Source";

        static bool HasCopiedPose => EditorPrefs.GetBool(CopyKeyHas, false);

        static Vector3 ReadPrefVector(string key)
        {
            var parts = EditorPrefs.GetString(key, "0,0,0").Split(',');
            if (parts.Length != 3) return Vector3.zero;
            float.TryParse(parts[0], out float x);
            float.TryParse(parts[1], out float y);
            float.TryParse(parts[2], out float z);
            return new Vector3(x, y, z);
        }

        static void WritePrefVector(string key, Vector3 v) =>
            EditorPrefs.SetString(key, $"{v.x:R},{v.y:R},{v.z:R}");

        void DrawCopyPasteRow(EnterExitHotspot comp)
        {
            var occupant = Application.isPlaying ? comp.Occupant : null;
            bool canCopy = occupant != null;

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(14f);

                using (new EditorGUI.DisabledScope(!canCopy))
                {
                    if (GUILayout.Button(new GUIContent("Copy From Occupant",
                            "Play mode only: reads the occupying character's current pose, relative to " +
                            "this spot's placement frame, and remembers it across exiting play mode.")))
                        CopyFromOccupant(comp, occupant);
                }

                using (new EditorGUI.DisabledScope(!HasCopiedPose || Application.isPlaying))
                {
                    if (GUILayout.Button(new GUIContent("Paste",
                            "Applies the copied pose to this spot. Available after leaving play mode, " +
                            "where the change sticks — anything edited during play is discarded.")))
                        PasteCopiedPose();
                }
            }

            DrawCopyStatus(canCopy);
        }

        void DrawCopyStatus(bool canCopy)
        {
            if (Application.isPlaying && !canCopy)
            {
                EditorGUILayout.LabelField("Nobody is occupying this spot yet — enter it to copy a pose.",
                    EditorStyles.wordWrappedMiniLabel);
                Repaint();   // the button must light up the moment the character settles in
                return;
            }

            if (!HasCopiedPose)
            {
                if (!Application.isPlaying)
                    EditorGUILayout.LabelField("Nothing copied yet. Enter play mode, put the character in place, then Copy.",
                        EditorStyles.wordWrappedMiniLabel);
                return;
            }

            Vector3 pos = ReadPrefVector(CopyKeyPos);
            Vector3 rot = ReadPrefVector(CopyKeyRot);
            string src = EditorPrefs.GetString(CopyKeySrc, "");
            EditorGUILayout.LabelField(
                $"Copied{(string.IsNullOrEmpty(src) ? "" : $" from {src}")}:  " +
                $"pos ({pos.x:0.###}, {pos.y:0.###}, {pos.z:0.###})   rot ({rot.x:0.#}, {rot.y:0.#}, {rot.z:0.#})",
                EditorStyles.wordWrappedMiniLabel);

            if (Application.isPlaying)
                EditorGUILayout.LabelField("Exit play mode to paste it.", EditorStyles.wordWrappedMiniLabel);
        }

        void CopyFromOccupant(EnterExitHotspot comp, Transform occupant)
        {
            var frame = comp.OccupantPlacementFrame;
            if (frame == null) return;

            WritePrefVector(CopyKeyPos, frame.InverseTransformPoint(occupant.position));
            WritePrefVector(CopyKeyRot, (Quaternion.Inverse(frame.rotation) * occupant.rotation).eulerAngles);
            EditorPrefs.SetString(CopyKeySrc, occupant.name);
            EditorPrefs.SetBool(CopyKeyHas, true);

            Debug.Log($"[Occupant Placement] Copied {occupant.name}'s pose relative to '{frame.name}'. " +
                      "Exit play mode and press Paste to apply it.", comp);
        }

        void PasteCopiedPose()
        {
            serializedObject.FindProperty("occupantLocalPosition").vector3Value = ReadPrefVector(CopyKeyPos);
            serializedObject.FindProperty("occupantLocalRotation").vector3Value = ReadPrefVector(CopyKeyRot);
        }

        /// <summary>
        /// Seeds the pose the first time placement is switched on, so enabling the option never moves
        /// anyone by itself. Left at zero it would mean the placement frame's ORIGIN — for a spot
        /// whose frame is a larger moving object that is the ground under it, which would drop the
        /// occupant through the floor.
        /// </summary>
        void SeedOccupantPoseOnEnable(EnterExitHotspot comp)
        {
            if (comp == null || !comp.useCustomOccupantPlacement)
            {
                lastPlacementEnabled = false;
                return;
            }

            if (!lastPlacementEnabled
                && comp.occupantLocalPosition == Vector3.zero
                && comp.occupantLocalRotation == Vector3.zero)
            {
                Undo.RecordObject(comp, "Enable Occupant Placement");
                comp.SetOccupantPoseFromWorld(comp.transform.position, comp.transform.rotation);
                EditorUtility.SetDirty(comp);
            }
            lastPlacementEnabled = true;
        }

        // ── Layout building blocks ────────────────────────────────────────────────────

        void DrawTitleCard()
        {
            titleStyle ??= new GUIStyle(EditorStyles.boldLabel) { fontSize = 13 };
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField(ObjectNames.NicifyVariableName(target.GetType().Name), titleStyle);
                EditorGUILayout.LabelField(Subtitle, EditorStyles.wordWrappedMiniLabel);
            }
            EditorGUILayout.Space(2);
        }

        /// <summary>
        /// A collapsible, boxed section listing serialized fields. The field names are CLAIMED
        /// immediately (so they can never fall through to "Advanced"), but only DRAWN when the
        /// section is expanded — this is what keeps each foldout showing strictly its own fields.
        /// </summary>
        protected void Section(string title, Color accent, params string[] propNames)
        {
            Claim(propNames);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (Header(title, accent, null, true))
                {
                    EditorGUILayout.Space(2);
                    DrawProps(propNames);
                }
            }
            EditorGUILayout.Space(2);
        }

        /// <summary>
        /// A collapsible section whose header carries the feature's enable toggle on the right. Its
        /// fields are claimed up-front (so they never leak into "Advanced") but only shown when the
        /// feature is enabled AND the section is expanded; otherwise a short hint is shown.
        /// </summary>
        protected void FeatureSection(string title, Color accent, string enabledProp, string help, params string[] propNames)
        {
            var prop = serializedObject.FindProperty(enabledProp);
            if (prop == null) return;

            drawn.Add(enabledProp);
            Claim(propNames);

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (Header(title, accent, prop, true))
                {
                    EditorGUILayout.Space(2);
                    if (prop.boolValue)
                    {
                        if (!string.IsNullOrEmpty(help))
                            EditorGUILayout.LabelField(help, EditorStyles.wordWrappedMiniLabel);
                        DrawProps(propNames);
                    }
                    else
                    {
                        EditorGUILayout.LabelField($"Enable to configure {title}.", EditorStyles.wordWrappedMiniLabel);
                    }
                }
            }
            EditorGUILayout.Space(2);
        }

        /// <summary>A collapsible, boxed section with custom body content (claims no fields).</summary>
        protected void SectionBody(string title, Color accent, bool defaultOpen, System.Action body)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                if (Header(title, accent, null, defaultOpen))
                {
                    EditorGUILayout.Space(2);
                    body();
                }
            }
            EditorGUILayout.Space(2);
        }

        // Header row: accent bar + bold foldout on the left, optional enable toggle on the right.
        bool Header(string title, Color accent, SerializedProperty enableProp, bool defaultOpen)
        {
            boldFoldout ??= new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold };

            Rect r = EditorGUILayout.GetControlRect(false, 20f);
            EditorGUI.DrawRect(new Rect(r.x, r.y + 3f, 3f, r.height - 5f), accent);

            if (!sectionOpen.TryGetValue(title, out bool open)) open = defaultOpen;
            float toggleW = enableProp != null ? 20f : 0f;
            open = EditorGUI.Foldout(new Rect(r.x + 9f, r.y, r.width - 9f - toggleW, r.height),
                                     open, title, true, boldFoldout);
            sectionOpen[title] = open;

            if (enableProp != null)
            {
                var tr = new Rect(r.xMax - 16f, r.y + 1f, 16f, r.height);
                enableProp.boolValue = EditorGUI.Toggle(tr, enableProp.boolValue);
            }
            return open;
        }

        /// <summary>Marks property names as owned by a section so "Advanced" never re-lists them.</summary>
        protected void Claim(string[] propNames)
        {
            foreach (var n in propNames) drawn.Add(n);
        }

        /// <summary>Draws the named properties (missing ones skipped); claiming happens separately.</summary>
        protected void DrawProps(string[] propNames)
        {
            EditorGUI.indentLevel++;
            foreach (var n in propNames)
            {
                var p = serializedObject.FindProperty(n);
                if (p != null) EditorGUILayout.PropertyField(p, true);
            }
            EditorGUI.indentLevel--;
        }

        // Collects every serialized field not claimed by a section, under a collapsed "Advanced" box.
        // Subclass fields land here until that subclass's editor claims them, so nothing an author
        // needs is ever silently missing from the inspector.
        void DrawAdvanced()
        {
            var rest = new List<SerializedProperty>();
            var it = serializedObject.GetIterator();
            bool enterChildren = true;
            while (it.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (it.name == "m_Script" || drawn.Contains(it.name)) continue;
                rest.Add(it.Copy());
            }
            if (rest.Count == 0) return;

            SectionBody("Advanced", NeutralAccent, false, () =>
            {
                EditorGUI.indentLevel++;
                foreach (var p in rest) EditorGUILayout.PropertyField(p, true);
                EditorGUI.indentLevel--;
            });
        }

        // Live occupancy readout. These are the states that explain what a spot is doing at any
        // moment — entering, occupied but emptying, or mid pull-out — which is otherwise invisible.
        void DrawRuntimeStatus()
        {
            if (!Application.isPlaying) return;
            var comp = target as EnterExitHotspot;
            if (comp == null) return;

            SectionBody("Runtime", NeutralAccent, true, () =>
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.Toggle("Occupied", comp.IsOccupied);
                    EditorGUILayout.Toggle("Enter Running", comp.IsEnterRunning);
                    EditorGUILayout.Toggle("Occupant Exiting", comp.IsOccupantExiting);
                    EditorGUILayout.Toggle("Pull Out Running", comp.IsPullOutRunning);

                    DrawRuntimeInfo(comp);
                }
            });
            Repaint();
        }
    }
}
