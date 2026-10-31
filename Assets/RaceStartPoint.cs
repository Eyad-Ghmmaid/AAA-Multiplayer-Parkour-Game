using UnityEngine;

/// <summary>
/// Map-specific lineup marker. Its position is the floor center of the front row;
/// its forward direction points down the course.
/// </summary>
public sealed class RaceStartPoint : MonoBehaviour
{
    [SerializeField, Min(0.5f)] private float playerSpacing = 1.5f;
    [SerializeField, Min(0.5f)] private float rowSpacing = 2f;
    [SerializeField, Min(1)] private int playersPerRow = 5;

    public Vector3[] CalculatePlayerSlots(int playerCount)
    {
        if (playerCount < 1)
            return System.Array.Empty<Vector3>();

        var firstRowCount = Mathf.Min(playerCount, playersPerRow);
        var positions = new Vector3[playerCount];

        for (var i = 0; i < playerCount; i++)
        {
            var row = i < firstRowCount ? 0 : 1;
            var rowIndex = row == 0 ? i : i - firstRowCount;
            var rowCount = row == 0 ? firstRowCount : playerCount - firstRowCount;
            var sideOffset = (rowIndex - (rowCount - 1) * 0.5f) * playerSpacing;
            positions[i] = transform.position
                + transform.right * sideOffset
                - transform.forward * (row * rowSpacing);
        }

        return positions;
    }

    private void OnValidate()
    {
        playerSpacing = Mathf.Max(0.5f, playerSpacing);
        rowSpacing = Mathf.Max(0.5f, rowSpacing);
        playersPerRow = Mathf.Max(1, playersPerRow);
    }
}
