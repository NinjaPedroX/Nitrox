﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Cache;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using LitJson;
using Nitrox.Launcher.Models.Design;
using NitroxModel.Logger;

namespace Nitrox.Launcher.Models.Utils;

public class Downloader
{
    public const string BLOGS_URL = "https://nitroxblog.rux.gg/wp-json/wp/v2/posts?per_page=8&page=1";
    public const string LATEST_VERSION_URL = "https://nitrox.rux.gg/api/version/latest";
    public const string CHANGELOGS_URL = "https://nitrox.rux.gg/api/changelog/releases";
    public const string RELEASES_URL = "https://nitrox.rux.gg/api/version/releases";

    // Create a policy that allows any cache to supply requested resources if the resource on the server is not newer than the cached copy
    private static readonly HttpRequestCachePolicy cachePolicy = new(
        HttpCacheAgeControl.MaxAge,
        TimeSpan.FromDays(1)
    );

    public static async Task<IList<NitroxBlog>> GetBlogs()
    {
        IList<NitroxBlog> blogs = new List<NitroxBlog>();

        try
        {
            using HttpResponseMessage response = await GetResponseFromCache(BLOGS_URL);

#if DEBUG
            if (response == null)
            {
                Log.Error($"{nameof(Downloader)} : Error while fetching Nitrox blogs from {BLOGS_URL}");
                LauncherNotifier.Error("Unable to fetch Nitrox blogs");
                return blogs;
            }
#endif

            JsonData data = JsonMapper.ToObject(await response.Content.ReadAsStringAsync());

            // TODO : Add a json schema validator
            for (int i = 0; i < data.Count; i++)
            {
                string released = (string)data[i]["date"];
                string url = (string)data[i]["link"];
                string title = (string)data[i]["title"]["rendered"];
                string imageUrl = (string)data[i]["jetpack_featured_media_url"];

                // Get image bitmap from image URL
                HttpResponseMessage imageResponse = await new HttpClient().GetAsync(imageUrl);
                imageResponse.EnsureSuccessStatusCode();
                byte[] imageData = await imageResponse.Content.ReadAsByteArrayAsync();
                Bitmap image = new(new MemoryStream(imageData));

                if (!DateOnly.TryParse(released, out DateOnly dateTime))
                {
                    dateTime = DateOnly.FromDateTime(DateTime.UtcNow);
                    Log.Error($"Error while trying to parse release time ({released}) of blog {url}");
                }

                blogs.Add(new NitroxBlog(WebUtility.HtmlDecode(title), dateTime, url, image));
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"{nameof(Downloader)} : Error while fetching Nitrox blogs from {BLOGS_URL}");
            LauncherNotifier.Error("Unable to fetch Nitrox blogs");
        }

        return blogs;
    }

    public static async Task<IList<NitroxChangelog>> GetChangeLogs()
    {
        IList<NitroxChangelog> changelogs = new List<NitroxChangelog>();

        try
        {
            //https://developer.wordpress.org/rest-api/reference/posts/#arguments
            using HttpResponseMessage response = await GetResponseFromCache(CHANGELOGS_URL);

            StringBuilder builder = new();
            JsonData data = JsonMapper.ToObject(await response.Content.ReadAsStringAsync());

            // TODO : Add a json schema validator
            for (int i = 0; i < data.Count; i++)
            {
                string version = (string)data[i]["version"];
                string released = (string)data[i]["released"];
                JsonData patchnotes = data[i]["patchnotes"];

                if (!DateTime.TryParse(released, out DateTime dateTime))
                {
                    dateTime = DateTime.UtcNow;
                    Log.Error($"Error while trying to parse release time ({released}) of Nitrox v{version}");
                }

                builder.Clear();
                for (int j = 0; j < patchnotes.Count; j++)
                {
                    builder.AppendLine($"• {(string)patchnotes[j]}");
                }

                changelogs.Add(new NitroxChangelog(version, dateTime, builder.ToString()));
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"{nameof(Downloader)} : Error while fetching Nitrox changelogs from {CHANGELOGS_URL}");
            LauncherNotifier.Error("Unable to fetch Nitrox changelogs");
        }

        return changelogs;
    }

    public static async Task<Version> GetNitroxLatestVersion()
    {
        try
        {
            using HttpResponseMessage response = await GetResponseFromCache(LATEST_VERSION_URL);

            Regex rx = new(@"""version"":""([^""]*)""");
            Match match = rx.Match(await response.Content.ReadAsStringAsync());

            if (match.Success && match.Groups.Count > 1)
            {
                return new Version(match.Groups[1].Value);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"{nameof(Downloader)} : Error while fetching Nitrox version from {LATEST_VERSION_URL}");
            LauncherNotifier.Error("Unable to check for Nitrox updates");
            throw;
        }

        return new Version();
    }

    private static async Task<HttpResponseMessage> GetResponseFromCache(string url)
    {
        Log.Info($"Trying to request data from {url}");

        using HttpClient client = new();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Nitrox.Launcher");
        client.DefaultRequestHeaders.CacheControl = new CacheControlHeaderValue { MaxAge = TimeSpan.FromDays(1) };
        client.Timeout = TimeSpan.FromSeconds(5);

        try
        {
            return await client.GetAsync(url);
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Error while requesting data from {url}");
        }

        return null;
    }
}