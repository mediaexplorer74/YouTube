﻿using System;
using Windows.Storage;

public static class Config
{
    private static string _apiBaseUrl = "https://yt.legacyprojects.ru/";
    private static string _defaultQuality = "360";
    private static string _usertoken = "";

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

    public static string UserToken
    {
        get { return _usertoken; }
    }

    // Video and Audio URL endpoints
    public static string DirectVideoUrlEndpoint => "direct_url";
    public static string DirectAudioUrlEndpoint => "direct_audio_url";
    
    // Quality settings
    public static string StandardQuality => null; // Standard quality (with audio) - no quality parameter
    public static bool UseClientSideAudioVideoMixing => false; // UWP limitation - not supported yet
    
    /*
     * VIDEO/AUDIO URL SYSTEM:
     * 
     * Standard Quality (no quality param): 
     *   - Video: https://yt.legacyprojects.ru/direct_url?video_id=ID
     *   - Contains audio track, works directly in UWP MediaPlayerElement
     * 
     * Quality with parameter (144, 360, 480, 720, 1080):
     *   - Video: https://yt.legacyprojects.ru/direct_url?video_id=ID&quality=720
     *   - May or may not contain embedded audio depending on backend implementation
     *   - If decode errors occur, investigate backend API behavior
     *   - Future: May implement audio/video mixing if needed
     */
    
    public static int MaxCardsPerPage { get; } = 10;
    public static int CardHeight { get; } = 400;
    public static int ThumbnailHeight { get; } = 280;
    public static string AuthImageUrl { get; internal set; }
    public static bool EnableChannelThumbnails = true;

    // Метод для установки токена
    public static void SetUserToken(string token)
    {
        _usertoken = token;
    }

    // ⚡️ НОВЫЙ МЕТОД: Загрузка токена из хранилища
    public static void LoadUserToken()
    {
        if (ApplicationData.Current.LocalSettings.Values.ContainsKey("AuthToken"))
        {
            _usertoken = ApplicationData.Current.LocalSettings.Values["AuthToken"] as string;
            System.Diagnostics.Debug.WriteLine($"Config: Loaded token from local settings: {(_usertoken.Length > 20 ? _usertoken.Substring(0, 20) + "..." : "Empty")}");
        }
    }
    
    // ⚡️ НОВЫЕ МЕТОДЫ: URL construction for video and audio
    public static string GetVideoUrl(string videoId, string quality = null)
    {
        string url = $"{ApiBaseUrl}{DirectVideoUrlEndpoint}?video_id={videoId}";
        if (!string.IsNullOrEmpty(quality))
        {
            url += $"&quality={quality}";
        }
        return url;
    }
    
    public static string GetAudioUrl(string videoId)
    {
        return $"{ApiBaseUrl}{DirectAudioUrlEndpoint}?video_id={videoId}";
    }
    
    // Check if quality requires separate audio track
    public static bool RequiresSeparateAudio(string quality)
    {
        // Only very high qualities (1080p+) might require separate audio in the future
        // Lower qualities (144, 360, 480, 720) should work with embedded audio
        if (string.IsNullOrEmpty(quality)) return false;
        
        return string.Equals(quality, "1080", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(quality, "1440", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(quality, "2160", StringComparison.OrdinalIgnoreCase);
    }
    
    // Check if client-side mixing is supported and enabled
    public static bool CanMixAudioVideo()
    {
        return UseClientSideAudioVideoMixing; // Currently false for UWP limitations
    }
}