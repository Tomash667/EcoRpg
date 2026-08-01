using UnityEngine;
using UnityEngine.UI;

public class Arrow2 : MonoBehaviour
{
    public Sprite fireSprite;

    private Vector2 from, to;
    private float timer;

    public void Shoot(Vector2 from, Vector2 to)
    {
        this.from = from;
        this.to = to;
        Vector2 dir = (to - from).normalized;
        (transform as RectTransform).anchoredPosition = from;
        transform.rotation = Quaternion.LookRotation(Vector3.forward, dir);
    }

    public void SetFire()
    {
        GetComponent<Image>().sprite = fireSprite;
    }

    private void Update()
    {
        timer += Time.deltaTime;
        (transform as RectTransform).anchoredPosition = Vector2.Lerp(from, to, timer / 0.15f);
        if (timer >= 0.2f)
            Destroy(gameObject);
    }
}
