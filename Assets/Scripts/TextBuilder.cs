using System.Text;

public class TextBuilder
{
    private readonly StringBuilder sb = new();

    public void Append(string str)
    {
        if (sb.Length > 0)
            sb.Append(' ');
        sb.Append(str);
    }

    public void Clear()
    {
        sb.Clear();
    }

    public string Flush()
    {
        string str = sb.ToString();
        sb.Clear();
        return str;
    }

    public void Set(string str)
    {
        sb.Clear();
        sb.Append(str);
    }
}
