using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace YouTube.Models
{
    public class ShortsVideo
    {
        [JsonProperty("video_id")]
        public string video_id { get; set; }

        [JsonProperty("title")]
        public string title { get; set; }

        [JsonProperty("thumbnail_url")]
        public string thumbnail_url { get; set; }

        [JsonProperty("views")]
        public string views { get; set; }
    }
}
