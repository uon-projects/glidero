using UnityEngine;

public class ButtonSounds : MonoBehaviour
{
    // Start is called before the first frame update
    private SoundManager soundManager;

    private void Start()
    {
        soundManager = FindObjectOfType<SoundManager>();
    }

    public void OnHover()
    {
        soundManager.OnHover();
    }

    public void OnClick()
    {
        soundManager.OnClick();
    }
}