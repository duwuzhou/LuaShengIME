using File = Java.IO.File;

namespace IME.Shared.Utils.FileOperation.Interfaces;

public interface IFileReading
{
    //读取内部json文件
    string ReadJsonFile(Android.Content.Context context, int resourceId);

}