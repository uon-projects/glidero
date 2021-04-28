using System.Collections;
using UnityEngine;

public class StartPad : MonoBehaviour
{
    public GliderController player;
    public float startStrengthUp;
    public float startStrengthForward;
    public float throttling = 0.1f;
    public float maxSpeed = 80;

    public void LaunchPlayer()
    {
        player.GetRB().isKinematic = false;
        var transform1 = player.transform;
        var finalForce =
            transform1.forward * startStrengthForward + transform1.up * startStrengthUp;
        var currentForce = Vector3.zero;
        for (var i = 0; i < 1 / throttling; i++)
        {
            currentForce = Vector3.Lerp(currentForce, finalForce, throttling);
            StartCoroutine(Launch(currentForce, throttling * i));

            if (!(i + 1 < 1 / throttling))
            {
                StartCoroutine(SetLaunched());
            }
        }
    }


    public IEnumerator Launch(Vector3 thrust, float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        if (player.GetRB().velocity.magnitude < maxSpeed)
        {
            player.GetRB().AddForce(thrust);
        }
        else
        {
            StopCoroutine(nameof(Launch));
        }
    }

    private IEnumerator SetLaunched()
    {
        yield return new WaitForSeconds(1f);
        player.SetLaunched(true);
    }

    private void OnValidate()
    {
        startStrengthUp = Mathf.Clamp(startStrengthUp, 0, Mathf.Infinity);
        startStrengthForward = Mathf.Clamp(startStrengthForward, 0, Mathf.Infinity);
    }
}