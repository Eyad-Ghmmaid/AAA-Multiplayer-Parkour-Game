#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace FS_Util
{
    /// <summary>
    /// Shared scene-gizmo helpers for "interaction points" — a world position + horizontal facing that
    /// a character walks to. Used by the carry / concealment / occupiable hotspots and the new
    /// Interaction System's Move Character node, so every one of them draws and edits points identically:
    ///
    ///   • a solid sphere marker + facing arrow, drawn with Handles (visible with the Gizmos toggle OFF),
    ///   • a Move handle and a Rotate disc,
    ///   • surface-snapping — the moved point snaps straight down onto the nearest surface on mouse
    ///     RELEASE (never mid-drag), across all layers, ignoring the owner's own colliders.
    ///
    /// The helpers are data-model agnostic: callers pass the point's world pose plus two write-back
    /// callbacks, so each system keeps its own offset/facing storage and pivot convention.
    /// </summary>
    public static class InteractionPointGizmo
    {
        const float CapScale = 0.12f;

        // Deferred surface-snap: only one handle drags at a time, so a single static slot suffices.
        static UnityEngine.Object s_owner;
        static Transform s_ignoreRoot;
        static Vector3 s_worldPos;
        static Action<Vector3> s_applyPos;
        static bool s_pending;

        /// <summary>
        /// Draws one interaction point (marker + Move handle + Rotate disc) at the given world pose.
        /// <paramref name="applyPos"/>/<paramref name="applyRot"/> write the new WORLD pose back — the
        /// caller converts to its own offset/facing space. When <paramref name="snapToSurface"/> is on,
        /// the moved point is snapped to the nearest surface on mouse release; call
        /// <see cref="ProcessDeferredSnap"/> once after drawing all points to apply it.
        /// </summary>
        public static void DrawPoint(UnityEngine.Object owner, Transform snapIgnoreRoot,
            Vector3 worldPos, Quaternion worldRot,
            Action<Vector3> applyPos, Action<Quaternion> applyRot,
            Color color, string label, bool editable, bool snapToSurface)
        {
            float hSize = HandleUtility.GetHandleSize(worldPos);
            DrawMarker(worldPos, worldRot, color, hSize * CapScale);

            if (editable)
            {
                // Move tool — a real manipulator, usable with Gizmos off. The surface snap is deferred
                // to mouse release (ProcessDeferredSnap) so the point follows the cursor freely mid-drag.
                EditorGUI.BeginChangeCheck();
                Vector3 newPos = Handles.PositionHandle(worldPos, worldRot);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(owner, "Move Interaction Point");
                    applyPos(newPos);
                    EditorUtility.SetDirty(owner);
                    if (snapToSurface)
                    {
                        s_pending = true;
                        s_owner = owner;
                        s_ignoreRoot = snapIgnoreRoot;
                        s_worldPos = newPos;
                        s_applyPos = applyPos;
                    }
                }

                // Rotate tool — also a manipulator → usable with Gizmos off.
                EditorGUI.BeginChangeCheck();
                Quaternion newRot = Handles.Disc(worldRot, worldPos, Vector3.up, hSize, false, 0f);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(owner, "Rotate Interaction Point");
                    applyRot(newRot);
                    EditorUtility.SetDirty(owner);
                }
            }

            if (Event.current.type == EventType.Repaint && !string.IsNullOrEmpty(label))
            {
                Handles.color = color;
                Handles.Label(worldPos + Vector3.up * (hSize * CapScale + 0.2f), label);
            }
        }

        /// <summary>
        /// Applies the deferred surface snap once the drag ends (hotControl returns to 0). Call once per
        /// scene-GUI pass, after drawing every point. Returns true if a snap was applied this call.
        /// </summary>
        public static bool ProcessDeferredSnap(SceneView sceneView)
        {
            // While the mouse button is held (a handle is being dragged) hotControl != 0, so the point
            // follows the cursor with NO snapping. The moment it is released the drag ends and hotControl
            // returns to 0 — only then snap the moved point to the nearest surface.
            if (!s_pending || GUIUtility.hotControl != 0) return false;

            if (s_owner != null && s_applyPos != null)
            {
                Undo.RecordObject(s_owner, "Snap Interaction Point");
                s_applyPos(SnapToSurface(s_ignoreRoot, s_worldPos));
                EditorUtility.SetDirty(s_owner);
            }

            s_pending = false;
            s_owner = null;
            s_ignoreRoot = null;
            s_applyPos = null;
            if (sceneView != null) sceneView.Repaint();
            return true;
        }

        /// <summary>Solid (closed) sphere marker plus a facing arrow, drawn with Handles.</summary>
        public static void DrawMarker(Vector3 pos, Quaternion rot, Color color, float radius)
        {
            if (Event.current.type != EventType.Repaint) return;

            Handles.color = color;
            Handles.SphereHandleCap(0, pos, Quaternion.identity, radius, EventType.Repaint);

            const float len = 0.6f, headLen = 0.15f, headAngle = 25f;
            Vector3 fwd = rot * Vector3.forward;
            Vector3 end = pos + fwd * len;
            Vector3 r = Quaternion.LookRotation(fwd) * Quaternion.Euler(0, 180 + headAngle, 0) * Vector3.forward;
            Vector3 l = Quaternion.LookRotation(fwd) * Quaternion.Euler(0, 180 - headAngle, 0) * Vector3.forward;
            Handles.DrawAAPolyLine(3f, pos, end);
            Handles.DrawAAPolyLine(3f, end, end + r * headLen);
            Handles.DrawAAPolyLine(3f, end, end + l * headLen);
        }

        /// <summary>
        /// Projects a position straight down onto the nearest surface below it across all layers — so it
        /// lands on top of a crate / platform / table, not just the ground — ignoring colliders under
        /// (or above) <paramref name="ignoreRoot"/>. Returns the original position if nothing is below.
        /// </summary>
        public static Vector3 SnapToSurface(Transform ignoreRoot, Vector3 pos)
        {
            var hits = Physics.RaycastAll(pos + Vector3.up * 2f, Vector3.down, 8f,
                                          Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            Vector3 result = pos;
            foreach (var h in hits)
            {
                // if (ignoreRoot != null &&
                //     (h.collider.transform.IsChildOf(ignoreRoot) || ignoreRoot.IsChildOf(h.collider.transform)))
                //     continue;
                if (h.distance < best) { best = h.distance; result = h.point; }
            }
            return result;
        }

        /// <summary>
        /// Draws a stopping-distance area as a flat disc using Handles.Disc — the same disc used for the
        /// destination-point facing handle. Its rotation return is ignored (visual only); the radius is
        /// edited via the owner's Stopping Distance field. The <paramref name="labelPrefix"/> is
        /// prepended to the "r = X.XX m" readout.
        /// </summary>
        public static void DrawStoppingDisc(Vector3 center, float radius, Color color, string labelPrefix)
        {
            Handles.color = color;
            Handles.Disc(Quaternion.identity, center, Vector3.up, radius, false, 0f);

            if (Event.current.type == EventType.Repaint)
            {
                Handles.color = color;
                Handles.Label(center + Vector3.up * 0.1f, $"{labelPrefix}r = {radius:F2} m");
            }
        }
    }
}
#endif
