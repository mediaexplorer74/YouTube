public static class Config
{
    private static string _apiBaseUrl = "https://qqq.bccst.ru/youtube/";
    private static string _defaultQuality = "360";

    public static string ApiBaseUrl
    {
        get { return _apiBaseUrl; }
        set { _apiBaseUrl = value.EndsWith("/") ? value : value + "/"; }
    }

    public static string DefaultQuality
    {
        get { return _defaultQuality; }
        set { _defaultQuality = value; }
    }

    public static int MaxCardsPerPage { get; } = 10;
    public static int CardHeight { get; } = 400;
    public static int ThumbnailHeight { get; } = 280;
}