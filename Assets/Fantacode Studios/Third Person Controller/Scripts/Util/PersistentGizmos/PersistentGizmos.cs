using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FS_Util
{

    public static class PersistentGizmos
    {
        struct RayGizmo
        {
            public Vector3 origin;
            public Vector3 direction;
            public float length;
            public Color color;
        }

        struct SphereGizmo
        {
            public Vector3 position;
            public float radius;
            public Color color;
        }

        struct SphereCastGizmo
        {
            public Vector3 origin;
            public Vector3 direction;
            public float radius;
            public float distance;
            public Color color;
        }

        struct BoxCastGizmo
        {
            public Vector3 origin;
            public Vector3 halfExtents;
            public Vector3 direction;
            public Quaternion orientation;
            public float distance;
            public Color color;
        }

        // Persistent (cleared only by explicit Clear() call)
        static readonly List<RayGizmo> rays = new();
        static readonly List<SphereGizmo> spheres = new();
        static readonly List<SphereCastGizmo> sphereCasts = new();
        static readonly List<BoxCastGizmo> boxCasts = new();

        // Once (cleared every frame after drawing)
        static readonly List<RayGizmo> raysOnce = new();
        static readonly List<SphereGizmo> spheresOnce = new();
        static readonly List<SphereCastGizmo> sphereCastsOnce = new();
        static readonly List<BoxCastGizmo> boxCastsOnce = new();

        // -------- PUBLIC API --------

        public static void DrawRay(Vector3 origin, Vector3 direction, float length, Color color)
        {
            rays.Add(new RayGizmo
            {
                origin = origin,
                direction = direction.normalized,
                length = length,
                color = color
            });
        }

        public static void DrawSphere(Vector3 position, float radius, Color color)
        {
            spheres.Add(new SphereGizmo
            {
                position = position,
                radius = radius,
                color = color
            });
        }

        public static void DrawSphereCast(Vector3 origin, float radius, Vector3 direction, float distance, Color color)
        {
            sphereCasts.Add(new SphereCastGizmo
            {
                origin = origin,
                direction = direction.normalized,
                radius = radius,
                distance = distance,
                color = color
            });
        }

        public static void DrawBoxCast(Vector3 origin, Vector3 halfExtents, Vector3 direction, Quaternion orientation, float distance, Color color)
        {
            boxCasts.Add(new BoxCastGizmo
            {
                origin = origin,
                halfExtents = halfExtents,
                direction = direction.normalized,
                orientation = orientation,
                distance = distance,
                color = color
            });
        }

        // -------- ONCE API (auto-cleared each frame) --------

        public static void DrawRayOnce(Vector3 origin, Vector3 direction, float length, Color color)
        {
            raysOnce.Add(new RayGizmo
            {
                origin = origin,
                direction = direction.normalized,
                length = length,
                color = color
            });
        }

        public static void DrawSphereOnce(Vector3 position, float radius, Color color)
        {
            spheresOnce.Add(new SphereGizmo
            {
                position = position,
                radius = radius,
                color = color
            });
        }

        public static void DrawSphereCastOnce(Vector3 origin, float radius, Vector3 direction, float distance, Color color)
        {
            sphereCastsOnce.Add(new SphereCastGizmo
            {
                origin = origin,
                direction = direction.normalized,
                radius = radius,
                distance = distance,
                color = color
            });
        }

        public static void DrawBoxCastOnce(Vector3 origin, Vector3 halfExtents, Vector3 direction, Quaternion orientation, float distance, Color color)
        {
            boxCastsOnce.Add(new BoxCastGizmo
            {
                origin = origin,
                halfExtents = halfExtents,
                direction = direction.normalized,
                orientation = orientation,
                distance = distance,
                color = color
            });
        }

        public static void Clear()
        {
            rays.Clear();
            spheres.Clear();
            sphereCasts.Clear();
            boxCasts.Clear();
        }

        public static void DrawGizmos()
        {
            DrawRayList(rays);
            DrawRayList(raysOnce);

            DrawSphereList(spheres);
            DrawSphereList(spheresOnce);

            DrawSphereCastList(sphereCasts);
            DrawSphereCastList(sphereCastsOnce);

            DrawBoxCastList(boxCasts);
            DrawBoxCastList(boxCastsOnce);

            // Clear transient gizmos after drawing
            raysOnce.Clear();
            spheresOnce.Clear();
            sphereCastsOnce.Clear();
            boxCastsOnce.Clear();
        }

        // -------- DRAW HELPERS --------

        static void DrawRayList(List<RayGizmo> list)
        {
            foreach (var ray in list)
            {
                Gizmos.color = ray.color;
                Gizmos.DrawRay(ray.origin, ray.direction * ray.length);
            }
        }

        static void DrawSphereList(List<SphereGizmo> list)
        {
            foreach (var sphere in list)
            {
                Gizmos.color = sphere.color;
                Gizmos.DrawWireSphere(sphere.position, sphere.radius);
            }
        }

        static void DrawSphereCastList(List<SphereCastGizmo> list)
        {
            foreach (var sc in list)
            {
                Gizmos.color = sc.color;
                var end = sc.origin + sc.direction * sc.distance;

                Gizmos.DrawWireSphere(sc.origin, sc.radius);
                Gizmos.DrawWireSphere(end, sc.radius);

                var perp1 = Vector3.Cross(sc.direction, Vector3.up).normalized;
                if (perp1.sqrMagnitude < 0.001f)
                    perp1 = Vector3.Cross(sc.direction, Vector3.right).normalized;
                var perp2 = Vector3.Cross(sc.direction, perp1).normalized;

                Gizmos.DrawLine(sc.origin + perp1 * sc.radius, end + perp1 * sc.radius);
                Gizmos.DrawLine(sc.origin - perp1 * sc.radius, end - perp1 * sc.radius);
                Gizmos.DrawLine(sc.origin + perp2 * sc.radius, end + perp2 * sc.radius);
                Gizmos.DrawLine(sc.origin - perp2 * sc.radius, end - perp2 * sc.radius);
            }
        }

        static void DrawBoxCastList(List<BoxCastGizmo> list)
        {
            foreach (var bc in list)
            {
                Gizmos.color = bc.color;
                var end = bc.origin + bc.direction * bc.distance;
                var size = bc.halfExtents * 2f;

                Gizmos.matrix = Matrix4x4.TRS(bc.origin, bc.orientation, Vector3.one);
                Gizmos.DrawWireCube(Vector3.zero, size);
                Gizmos.matrix = Matrix4x4.TRS(end, bc.orientation, Vector3.one);
                Gizmos.DrawWireCube(Vector3.zero, size);
                Gizmos.matrix = Matrix4x4.identity;

                var r = bc.orientation * new Vector3(bc.halfExtents.x, bc.halfExtents.y, bc.halfExtents.z);
                var u = bc.orientation * new Vector3(bc.halfExtents.x, bc.halfExtents.y, -bc.halfExtents.z);
                var v = bc.orientation * new Vector3(-bc.halfExtents.x, bc.halfExtents.y, bc.halfExtents.z);
                var w = bc.orientation * new Vector3(-bc.halfExtents.x, bc.halfExtents.y, -bc.halfExtents.z);

                Gizmos.DrawLine(bc.origin + r, end + r);
                Gizmos.DrawLine(bc.origin + u, end + u);
                Gizmos.DrawLine(bc.origin + v, end + v);
                Gizmos.DrawLine(bc.origin + w, end + w);
            }
        }
    }
}
