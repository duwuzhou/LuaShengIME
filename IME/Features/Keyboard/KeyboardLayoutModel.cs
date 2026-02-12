using System.Collections.Generic;
using IME.Shared.Utils.Enums;

namespace IME.Features.Keyboard;

public class KeyboardLayoutModel
{
    public IKeyboardType LayoutType { get; set; } = IKeyboardType.Other;
    public float KeyHeightDp { get; set; } = 52f;
    public float HorizontalGapDp { get; set; } = 0f;
    public float VerticalGapDp { get; set; } = 3f;
    public List<KeyboardRowModel> Rows { get; } = new();
}

public class KeyboardRowModel
{
    public float DefaultKeyWidthPercent { get; set; } = 10f;
    public List<KeyboardKeyModel> Keys { get; } = new();
}

public class KeyboardKeyModel
{
    public int Code { get; set; }
    public string Label { get; set; } = string.Empty;
    public float WidthPercent { get; set; }
    public string PopupCharacters { get; set; } = string.Empty;
}

