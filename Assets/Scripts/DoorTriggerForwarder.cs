using UnityEngine;

public class DoorTriggerForwarder : MonoBehaviour
{
    private Door parentDoor;

    public void Initialize(Door door)
    {
        parentDoor = door;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (parentDoor != null)
        {
            parentDoor.OnChildTriggerEnter2D(other);
        }
    }
}
