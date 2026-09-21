using UnityEngine;

public class FeedbackEffect : MonoBehaviour
{
    public bool wrong;
    public bool final;
    public float lifetime = 0.7f;

    private void Start()
    {
        var manager = FindFirstObjectByType<StemGameManager>();
        if (manager)
        {
            if (wrong) manager.WrongAnswer();
            else manager.AddUst();
        }
        Destroy(gameObject, lifetime);
    }
}
