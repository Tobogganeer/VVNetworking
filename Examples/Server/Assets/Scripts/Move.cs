using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Move : MonoBehaviour
{
    public float yRotationPerSecond = 180f;

    public float moveSpeed = 3f;
    public float moveIntensity = 3f;

    private void Update()
    {
        Vector3 euler = transform.eulerAngles;
        euler.y += yRotationPerSecond * Time.deltaTime;
        transform.eulerAngles = euler;

        transform.Translate(new Vector3((Mathf.Sin(Time.time * moveSpeed) * moveIntensity) * Time.deltaTime, 0, 0));
    }
}
