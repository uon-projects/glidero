using UnityEngine;

public class CollisionLogic : MonoBehaviour
{
    public GliderController player;

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Player")) player.Kill();
    }
}