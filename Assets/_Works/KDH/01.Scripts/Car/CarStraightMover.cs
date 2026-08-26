using UnityEngine;

public class CarStraightMover : MonoBehaviour
{
    [SerializeField] private float speed = 5f;
    [SerializeField] private Transform[] wheels;
    [SerializeField] private float wheelRadius = 0.35f;

    private bool isMoving = true;

    private void Update()
    {
        if (!isMoving) return;

        transform.Translate(Vector3.forward * speed * Time.deltaTime, Space.Self);

        float rotationAngle = (speed * Time.deltaTime / wheelRadius) * Mathf.Rad2Deg;
        for (int i = 0; i < wheels.Length; i++)
        {
            if (wheels[i] != null)
            {
                wheels[i].Rotate(Vector3.right, rotationAngle, Space.Self);
            }
        }
    }

    public void Stop()
    {
        isMoving = false;
    }

    public void Resume()
    {
        isMoving = true;
    }
}
