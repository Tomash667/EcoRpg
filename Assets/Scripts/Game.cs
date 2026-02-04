using TMPro;
using UnityEditor;
using UnityEngine;

public class Game : MonoBehaviour
{
    private TMP_Text text;
    private string lastAction;
    private int day, hour, energy, gold;

    private void Awake()
    {
        text = transform.Find("Text").GetComponent<TMP_Text>();
        day = 1;
        hour = 8;
        energy = 100;
        gold = 50;
        UpdateText();
    }

    private void Update()
    {
#if UNITY_EDITOR
        if (Input.GetKeyDown(KeyCode.F4))
            EditorApplication.isPlaying = false;
#endif
    }

    public void Explore()
    {
        if (energy < 25)
        {
            lastAction = "You are too tired to explore.";
            UpdateText();
            return;
        }

        ++hour;
        energy -= 10;
        if (Random.Range(0, 10) > 3)
        {
            energy -= 10;
            gold += 15;
            lastAction = "You explore city and fight with bandits (15 gold found).";
        }
        else
            lastAction = "You explore city but find nothing interesting.";
        if (hour == 24)
        {
            ++day;
            hour = 8;
            energy = 100;
            lastAction += " It's a new day.";
        }
        UpdateText();
    }

    public void Rest()
    {
        ++day;
        hour = 8;
        energy = 100;
        lastAction = "You rest. It's a new day.";
        UpdateText();
    }

    public void Work()
    {
        if (hour > 16)
            lastAction = "It's too late to work.";
        else if (energy < 50)
            lastAction = "You are too tired to work.";
        else
        {
            ++day;
            hour = 8;
            energy = 100;
            gold += 20;
            lastAction = "You earned 20 gold from working. It's a new day.";
        }
        UpdateText();
    }

    private void UpdateText()
    {
        string str = $"Day: {day} {hour}:00\nEnergy: {energy}/100\nGold: {gold}";
        if (lastAction != null)
        {
            str += "\n\n";
            str += lastAction;
            lastAction = null;
        }

        text.text = str;
    }
}
