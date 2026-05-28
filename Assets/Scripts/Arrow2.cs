using UnityEngine;

public class Arrow2 : MonoBehaviour
{
    private Vector2 from, to;
    private float timer;

    public void Shoot(Vector2 from, Vector2 to)
    {
        this.from = from;
        this.to = to;
        Vector2 dir = (to - from).normalized;
        transform.rotation = Quaternion.LookRotation(Vector3.forward, dir);
    }

    private void Update()
    {
        timer += Time.deltaTime;
        (transform as RectTransform).anchoredPosition = Vector2.Lerp(from, to, timer / 0.15f);
        if (timer >= 0.2f)
            Destroy(gameObject);
    }
}
