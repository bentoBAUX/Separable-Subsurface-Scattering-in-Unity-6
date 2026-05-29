using UnityEngine;

public class DeterministicPointLightPath : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;

    [Header("Orbit")]
    [SerializeField] private float orbitRadius = 3.0f;
    [SerializeField] private float innerRadius = 0.35f;
    [SerializeField] private float height = 1.2f;
    [SerializeField] private float orbitSpeed = 0.7f;
    [SerializeField] private float pathTiltDegrees = 35.0f;

    [Header("Inward Motion")]
    [SerializeField] private float inwardFrequency = 1.0f;
    [SerializeField] private float inwardSharpness = 2.0f;

    private void Update()
    {
        if (target == null)
            return;

        float t = Time.time;
        float angle = t * orbitSpeed;

        Vector3 orbitDirection = new Vector3(
            Mathf.Cos(angle),
            0.0f,
            Mathf.Sin(angle)
        );

        float inward = (Mathf.Sin(t * inwardFrequency) + 1.0f) * 0.5f;
        inward = Mathf.Pow(inward, inwardSharpness);

        float currentRadius = Mathf.Lerp(orbitRadius, innerRadius, inward);

        Quaternion pathTilt = Quaternion.Euler(pathTiltDegrees, 0.0f, 0.0f);

        Vector3 offset =
            pathTilt * (orbitDirection * currentRadius) +
            Vector3.up * height;

        transform.position = target.position + offset;
    }
}