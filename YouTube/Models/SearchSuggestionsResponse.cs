using System.Collections.Generic;

namespace YouTube.Models
{
    public class SearchSuggestionsResponse
    {
        public string query { get; set; }
        public List<List<object>> suggestions { get; set; }
    }
} 