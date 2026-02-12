using Android.Content;
using Android.Util;
using System.Globalization;
using System.Xml;

namespace IME.Features.Keyboard;

public class KeyboardXmlLayoutParser
{
    private const string AndroidNamespace = "http://schemas.android.com/apk/res/android";
    private readonly Context _context;

    public KeyboardXmlLayoutParser(Context context)
    {
        _context = context;
    }

    public KeyboardLayoutModel Parse(int resourceId)
    {
        try
        {
            using var reader = _context.Resources?.GetXml(resourceId);
            if (reader == null)
            {
                throw new InvalidOperationException($"Cannot open keyboard resource: {resourceId}");
            }

            var model = new KeyboardLayoutModel();
            KeyboardRowModel? currentRow = null;

            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element)
                {
                    switch (reader.Name)
                    {
                        case "Keyboard":
                            model.KeyHeightDp = ParseDimension(GetAndroidAttribute(reader, "keyHeight"), 52f);
                            model.HorizontalGapDp = ParseDimension(GetAndroidAttribute(reader, "horizontalGap"), 0f);
                            model.VerticalGapDp = ParseDimension(GetAndroidAttribute(reader, "verticalGap"), 3f);
                            break;

                        case "Row":
                            currentRow = new KeyboardRowModel
                            {
                                DefaultKeyWidthPercent = ParsePercent(GetAndroidAttribute(reader, "keyWidth"), 10f)
                            };
                            model.Rows.Add(currentRow);
                            break;

                        case "Key":
                            currentRow ??= CreateFallbackRow(model);
                            currentRow.Keys.Add(ParseKey(reader, currentRow.DefaultKeyWidthPercent));
                            break;
                    }
                }
                else if (reader.NodeType == XmlNodeType.EndElement && reader.Name == "Row")
                {
                    currentRow = null;
                }
            }

            model.Rows.RemoveAll(row => row.Keys.Count == 0);
            if (model.Rows.Count == 0)
            {
                throw new InvalidOperationException($"No key rows in keyboard resource: {resourceId}");
            }

            return model;
        }
        catch (Exception ex)
        {
            Log.Error(nameof(KeyboardXmlLayoutParser), $"Parse keyboard xml failed: resource={resourceId}, error={ex.Message}");
            throw;
        }
    }

    private static KeyboardRowModel CreateFallbackRow(KeyboardLayoutModel model)
    {
        var row = new KeyboardRowModel();
        model.Rows.Add(row);
        return row;
    }

    private static KeyboardKeyModel ParseKey(XmlReader reader, float defaultWidthPercent)
    {
        var code = ParseKeyCode(GetAndroidAttribute(reader, "codes"));
        var keyLabel = GetAndroidAttribute(reader, "keyLabel") ?? string.Empty;
        var popupCharacters = GetAndroidAttribute(reader, "popupCharacters") ?? string.Empty;
        var widthPercent = ParsePercent(GetAndroidAttribute(reader, "keyWidth"), defaultWidthPercent);

        return new KeyboardKeyModel
        {
            Code = code,
            Label = keyLabel,
            WidthPercent = widthPercent,
            PopupCharacters = popupCharacters
        };
    }

    private static string? GetAndroidAttribute(XmlReader reader, string attributeName)
    {
        return reader.GetAttribute(attributeName, AndroidNamespace);
    }

    private static int ParseKeyCode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0;
        }

        var rawCode = value.Split(',')[0].Trim();
        return int.TryParse(rawCode, NumberStyles.Integer, CultureInfo.InvariantCulture, out var code)
            ? code
            : 0;
    }

    private static float ParsePercent(string? value, float fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var raw = value.Trim();
        if (!raw.EndsWith("%p", StringComparison.OrdinalIgnoreCase)
            && !raw.EndsWith("%", StringComparison.OrdinalIgnoreCase))
        {
            return fallback;
        }

        var number = raw.Replace("%p", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("%", string.Empty, StringComparison.OrdinalIgnoreCase);

        return float.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out var percent)
            ? percent
            : fallback;
    }

    private static float ParseDimension(string? value, float fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var raw = value.Trim();
        if (raw.EndsWith("%p", StringComparison.OrdinalIgnoreCase)
            || raw.EndsWith("%", StringComparison.OrdinalIgnoreCase))
        {
            return fallback;
        }

        var numeric = new string(raw.TakeWhile(ch => char.IsDigit(ch) || ch == '.' || ch == '-').ToArray());
        return float.TryParse(numeric, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;
    }
}
