using System.Text;
using TXTextControl;

public static class Redaction
{
    static int removedCharacterCount = 0;

    public static int RedactSelection(int start, int length, ServerTextControl textControl)
    {
        int currentLine = -1;
        int processLength = 0;
        int processStart = start;
        removedCharacterCount = length;

        List<RectangleF> characterBounds = new List<RectangleF>();

        for (int i = start; i < start + length; i++)
        {
            textControl.Select(i, 1);
            var curLine = textControl.Lines.GetItem(i);

            if (currentLine != curLine.Number)
            {
                if (characterBounds.Count > 0)
                {
                    // Process the accumulated line when switching lines
                    ProcessLine(textControl, ref processStart, ref processLength, characterBounds);
                    removedCharacterCount--;

                    // Adjust index and length to prevent reprocessing of characters
                    i = i - characterBounds.Count + 1;
                    processStart = i;
                    length = length - characterBounds.Count + 1;

                    currentLine = curLine.Number;
                    characterBounds.Clear();
                    processLength = 0;
                }
            }

            currentLine = curLine.Number;

            float lineBaseline = textControl.Lines[currentLine].Baseline;

            RectangleF currentCharBounds = textControl.TextChars[i + 1].Bounds;
            currentCharBounds.Height -= (currentCharBounds.Bottom - lineBaseline);

            // Add the current character bounds
            characterBounds.Add(currentCharBounds);
            processLength++;
        }

        // Process any remaining characters on the last line
        ProcessLine(textControl, ref processStart, ref processLength, characterBounds);

        return removedCharacterCount - 1;
    }

    private static void ProcessLine(ServerTextControl textControl, ref int processStart, ref int processLength, List<RectangleF> characterBounds)
    {
        if (characterBounds.Count == 0) return;

        textControl.Select(processStart, processLength);

        if (textControl.Selection.Text.EndsWith(' ') || textControl.Selection.Text.EndsWith('\n'))
        {
            textControl.Selection.Length -= 1;
            characterBounds.RemoveAt(characterBounds.Count - 1);
            removedCharacterCount--;
        }

        textControl.Selection.Text = "";

        byte[] svgBytes = GenerateSVGForChars(characterBounds);

        if (svgBytes.Length == 0) return;

        using (MemoryStream ms = new MemoryStream(svgBytes, 0, svgBytes.Length, writable: false, publiclyVisible: true))
        {
            TXTextControl.Image img = new TXTextControl.Image(ms);
            textControl.Images.Add(img, -1);
        }            
    }

    private static byte[] GenerateSVGForChars(List<RectangleF> characterBounds)
    {
        if (characterBounds.Count == 0) return Array.Empty<byte>();

        var combinedBounds = GetBoundingRectangle(characterBounds);

        float baselineAdjustment = characterBounds[0].Bottom - combinedBounds.Bottom;
        combinedBounds.Height -= baselineAdjustment;

        return CreateRedactionSVG(combinedBounds.Size);
    }

    private static RectangleF GetBoundingRectangle(List<RectangleF> bounds)
    {
        float xMin = bounds.Min(b => b.Left);
        float yMin = bounds.Min(b => b.Top);
        float xMax = bounds.Max(b => b.Right);
        float yMax = bounds.Max(b => b.Bottom);
        return new RectangleF(xMin, yMin, xMax - xMin, yMax - yMin);
    }

    private static byte[] CreateRedactionSVG(SizeF size)
    {
        size.Width /= 20;
        size.Height /= 20;

        string svg = $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{size.Width}pt\" height=\"{size.Height}pt\"><rect width=\"100%\" height=\"100%\" fill=\"red\" /></svg>";
        return Encoding.UTF8.GetBytes(svg);
    }
}

