using UnityEngine;
public class AutoRotate : MonoBehaviour
{
    public float speed = 0.5f;
    void Update() { transform.Rotate(0, speed * Time.deltaTime, 0); }
}