using System.Collections.Generic;

namespace SldFile.Mfc;

internal sealed class MfcTag
{
    public string ClassName { get; set; } = string.Empty;
    public byte TagByte { get; set; }
    public int TagOffset { get; set; }
    public int DataStart { get; set; }
    public int DataEnd { get; set; }
    public int DataSize => DataEnd - DataStart;
    public int Depth { get; set; }
    public int Index { get; set; }
    public MfcTag? Parent { get; set; }
    public List<MfcTag> Children { get; } = new List<MfcTag>();
}
