using UnityEngine;
using UnityEngine.AI;

namespace FS_Util
{
    public static class NavMeshUtil
    {
        public static float GetNavMeshDistance(Vector3 from, Vector3 to)
        {
            NavMeshPath path = new NavMeshPath();
            if (NavMesh.CalculatePath(from, to, NavMesh.AllAreas, path))
            {
                float distance = 0f;
                for (int i = 0; i < path.corners.Length - 1; i++)
                {
                    distance += Vector3.Distance(path.corners[i], path.corners[i + 1]);
                }
                return distance;
            }

            return Mathf.Infinity;
        }

        /// <summary>
        /// Maps a world position (e.g. a projectile impact point on a wall) to the nearest
        /// navmesh point reachable from <paramref name="from"/>. Plain SamplePosition can pick
        /// a disconnected surface (top of a wall, far side of a fence), so the sampled point is
        /// path-checked and the closest reachable point along that path is returned instead.
        /// Returns the input unchanged if no navmesh is found within maxDistance.
        /// </summary>
        public static Vector3 GetNearestReachablePosition(Vector3 from, Vector3 target, float maxDistance = 4f)
        {
            if (NavMesh.SamplePosition(target, out NavMeshHit hit, maxDistance, NavMesh.AllAreas))
                target = hit.position;

            // A partial path ends at the closest reachable point (e.g. the wall base
            // when the sampled point landed on top of the wall)
            NavMeshPath path = new NavMeshPath();
            if (NavMesh.CalculatePath(from, target, NavMesh.AllAreas, path) && path.corners.Length > 0)
                return path.corners[path.corners.Length - 1];

            return target;
        }
    }
}
