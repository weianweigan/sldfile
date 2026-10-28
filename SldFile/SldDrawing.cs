namespace SldFile;

public class SldDrawing : SwFile
{
    public SldDrawing(string filePath, bool readOnly = true) : base(filePath, readOnly)
    {
    }
}
