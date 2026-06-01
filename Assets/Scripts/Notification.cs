using System;

[Serializable]
public class Notification
{
    public enum Status
    {
        Waiting,
        Available,
        Read
    }

    public string text;
    public Status status;
    public int day;
}
