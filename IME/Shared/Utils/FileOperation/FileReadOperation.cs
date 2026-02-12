using System.Text;
using IME.Shared.Utils.FileOperation.Interfaces;
namespace IME.Shared.Utils.FileOperation;

public class FileReadOperation : IFileReading
{

    public string ReadJsonFile(Android.Content.Context context, int resourceId)
    {
        try
        {
            using var stream = context.Resources.OpenRawResource(resourceId);
            using var reader = new StreamReader(stream, Encoding.UTF8, true);
            //返回字符串
            return reader.ReadToEnd();
        }
        catch (Java.IO.FileNotFoundException e)
        {
            Android.Util.Log.Error("FileReadOperation", "File not found: " + e.Message);
            return string.Empty;
        }

    }
}