using Newtonsoft.Json;

namespace YouTube.Models
{
    public class VideoInfo
    {
        [JsonProperty("video_id")]
        public string video_id { get; set; }

        [JsonProperty("title")]
        public string title { get; set; }

        [JsonProperty("author")]
        public string author { get; set; }

        [JsonProperty("thumbnail")]
        public string thumbnail { get; set; }

        [JsonProperty("channel_thumbnail")]
        public string channel_thumbnail { get; set; }

        [JsonProperty("views")]
        public string Views { get; set; }

        [JsonProperty("published_at")]
        public string PublishedAt { get; set; }

        [JsonProperty("views_text")]
        public string ViewsText { get; set; }
    }
} 