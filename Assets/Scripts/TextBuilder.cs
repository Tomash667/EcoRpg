public class TextBuilder
{
    public string text;

    public void Append(string str)
    {
        if (text.Length > 0)
            text += ' ';
        text += str;
    }
}
