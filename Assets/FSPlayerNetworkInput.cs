using Fusion;
using UnityEngine;

/// <summary>
/// Input submitted by one player to Fusion. The Host will consume this data to
/// drive the FS controller; clients never submit a transform or race time.
/// </summary>
public struct FSPlayerNetworkInput : INetworkInput
{
    public Vector2 Move;
    public Vector3 CameraForward;

    public bool JumpPressed;
    public bool JumpHeld;
    public bool Drop;
    public bool ToggleRun;
    public bool Sprint;
    public bool Crouch;
    public bool QuickSwitchDown;
    public bool QuickSwitchUp;
    public bool PreviousItem;
    public bool NextItem;
    public bool ThrowDown;
    public bool ThrowHeld;
    public bool ThrowUp;
    public bool ThrowCancel;
    public bool JumpFromHang;

    public int GrapplePress;
    public NetworkButtons GrappleButtons;
    public Vector3 GrappleAimOffset;
    public Vector3 GrappleAimDirection;
}
