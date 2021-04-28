using UnityEngine;

public class WindStart : MonoBehaviour
{
    public float Strength;
    public int timeUntilStrength = 5;
    public int curveSteepness = 4;
    public float timeInWind = 0;

    void OnTriggerStay(Collider col)
    {
        if (col.CompareTag("Player"))
        {
            if (timeInWind < timeUntilStrength) timeInWind += Time.deltaTime;
            AircraftPhysics aircraft = col.GetComponentInParent<AircraftPhysics>();
            float coefficient = Mathf.Pow(timeInWind / timeUntilStrength, curveSteepness);
            Vector3 wind = Vector3.up * Strength * coefficient;
            aircraft.SetWind(wind);
            Debug.Log($"in wind: {Time.time}");
        }
    }

    private void OnTriggerExit(Collider col)
    {
        if (col.CompareTag("Player"))
        {
            AircraftPhysics aircraft = col.GetComponentInParent<AircraftPhysics>();
            aircraft.SetWind(Vector3.zero);
        }
    }

    public void Reset()
    {
        timeInWind = 0;
    }
}