namespace AutoOcrs.Core.Models;

public class OcrTextBox
{
    public string Text { get; set; } = string.Empty;
    public float[] Box { get; set; } = new float[4]; // [xmin, ymin, xmax, ymax]
    public float Confidence { get; set; }
}
