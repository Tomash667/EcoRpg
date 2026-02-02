using TMPro;
using UnityEditor;
using UnityEngine;

public class Game : MonoBehaviour
{
    private TMP_Text text;
    private int day, gold;

    private void Awake()
    {
        text = transform.Find("Text").GetComponent<TMP_Text>();
        day = 1;
        gold = 50;
    }

    private void Update()
    {
#if UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.F4))
            EditorApplication.isPlaying = false;
#endif
    }

    public void Work()
    {
        ++day;
        gold += 20;
        text.text = $"Day: {day}\nGold: {gold}";
    }
}
