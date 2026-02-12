using Android.Content;
using Android.Util;
using IME.Shared.Utils.Enums;

namespace IME.Features.Keyboard;

public class KeyboardLayoutManager
{
    private readonly KeyboardXmlLayoutParser _layoutParser;
    private IKeyboardType _currentType = IKeyboardType.LowerCase;
    private KeyboardLayoutModel? _currentLayout;

    private static readonly Dictionary<IKeyboardType, int> KeyboardResourceMap = new()
    {
        { IKeyboardType.LowerCase, Resource.Xml.keyboard_layout_EnSmall },
        { IKeyboardType.UpperCase, Resource.Xml.keyboard_layout_Enbig },
        { IKeyboardType.Number, Resource.Xml.keyboard_layout_Number },
        { IKeyboardType.Symbol, Resource.Xml.keyboard_layout_Symbol },
        { IKeyboardType.SymbolCN, Resource.Xml.keyboard_layout_SymbolCN },
        { IKeyboardType.T9, Resource.Xml.keyboard_layout_T9 },
        { IKeyboardType.LayoutPicker, Resource.Xml.keyboard_layout_picker }
    };

    public KeyboardLayoutManager(Context context)
    {
        _layoutParser = new KeyboardXmlLayoutParser(context);
    }

    public IKeyboardType CurrentType => _currentType;

    public KeyboardLayoutModel? CurrentLayout => _currentLayout;

    public KeyboardLayoutModel LoadInitialKeyboard()
    {
        return SwitchKeyboard(IKeyboardType.LowerCase);
    }

    public KeyboardLayoutModel SwitchKeyboard(IKeyboardType type)
    {
        try
        {
            var resourceId = GetResourceId(type);
            var layout = _layoutParser.Parse(resourceId);
            layout.LayoutType = type;
            _currentType = type;
            _currentLayout = layout;
            return layout;
        }
        catch (Exception ex)
        {
            Log.Warn(nameof(KeyboardLayoutManager), $"Load keyboard failed: type={type}, error={ex.Message}");

            if (type == IKeyboardType.SymbolCN)
            {
                return SwitchKeyboard(IKeyboardType.Symbol);
            }

            if (_currentLayout != null)
            {
                return _currentLayout;
            }

            _currentType = IKeyboardType.LowerCase;
            _currentLayout = _layoutParser.Parse(Resource.Xml.keyboard_layout_EnSmall);
            _currentLayout.LayoutType = IKeyboardType.LowerCase;
            return _currentLayout;
        }
    }

    public KeyboardLayoutModel SwitchByAsciiMode(bool isAsciiMode)
    {
        var targetType = isAsciiMode ? IKeyboardType.UpperCase : IKeyboardType.LowerCase;
        return SwitchKeyboard(targetType);
    }

    private static int GetResourceId(IKeyboardType type)
    {
        if (KeyboardResourceMap.TryGetValue(type, out var resourceId))
        {
            return resourceId;
        }

        Log.Warn(nameof(KeyboardLayoutManager), $"Unknown keyboard type: {type}, fallback to lowercase.");
        return Resource.Xml.keyboard_layout_EnSmall;
    }

    public void Cleanup()
    {
        _currentLayout = null;
    }
}
