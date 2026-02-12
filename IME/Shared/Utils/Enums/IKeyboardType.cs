using Java.Math;

namespace IME.Shared.Utils.Enums
{
    public enum IKeyboardType
    {
        //小写字母
        LowerCase = Resource.Xml.keyboard_layout_EnSmall,
        //大写字母
        UpperCase = Resource.Xml.keyboard_layout_Enbig,
        //数字
        Number = Resource.Xml.keyboard_layout_Number,
        //符号（英文）
        Symbol = Resource.Xml.keyboard_layout_Symbol,
        //中文符号
        SymbolCN = 100,  // 将在资源编译后更新
        //其他
        Other = 4,
        //中文
        Chinese = 5,
        //九宫格T9
        T9 = 6,
        //键盘布局选择器
        LayoutPicker = 7,
    }
}