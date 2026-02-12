using Newtonsoft.Json;

namespace IME.Shared.Models
{
    public class SdDataModel
    {
        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("data")]
        public List<string> Data { get; set; } = new List<string>();
    }

    // 如果要包含整个数组结构，可以添加根模型
    public class RootModel
    {
        public List<SdDataModel> Weapons { get; set; }
    }
}