using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DoorController : MonoBehaviour
{
    public Transform door;
    public Vector3 openRotation;
    public Vector3 closedRotation;
    public float speed = 2f;
    public bool isOpen = false;
    public bool inverted = false;

    // Track every player inside the trigger - a single bool closes the door on everyone
    // while a second player is still standing in it.
    private readonly HashSet<Collider> _occupants = new HashSet<Collider>();

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;
        _occupants.RemoveWhere(c => c == null); // players destroyed while inside
        _occupants.Add(other);
        if (isOpen)
            return;
        isOpen = true;
        StopAllCoroutines();
        var directionToPlayer = other.transform.position - door.position;
        var rotation = CalculateRotation(directionToPlayer);
        StartCoroutine(RotateDoor(door.localRotation, Quaternion.Euler(rotation)));
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;
        _occupants.Remove(other);
        _occupants.RemoveWhere(c => c == null);
        if (_occupants.Count > 0 || !isOpen)
            return;
        isOpen = false;
        StopAllCoroutines();
        StartCoroutine(RotateDoor(door.localRotation, Quaternion.Euler(closedRotation)));
    }

    private IEnumerator RotateDoor(Quaternion startRotation, Quaternion endRotation)
    {
        float elapsedTime = 0;

        while (elapsedTime < 1f)
        {
            door.localRotation = Quaternion.Slerp(startRotation, endRotation, elapsedTime);
            elapsedTime += Time.deltaTime * speed;
            yield return null;
        }

        door.localRotation = endRotation;  
    }
    private Vector3 CalculateRotation(Vector3 direction)
    {
        var angle = Mathf.Atan2(direction.x, direction.z);
        angle = inverted ? (angle > 1 ? -90 : 90) : (angle > 1 ? 90 : -90);
        return new Vector3(0, angle, 0);
    }
}