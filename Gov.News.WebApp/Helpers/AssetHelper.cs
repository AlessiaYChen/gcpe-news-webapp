using Gov.News.Api.Models;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.WebUtilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;

namespace Gov.News.Website.Helpers
{
    public static class AssetHelper
    {
        public static readonly Regex AssetRegex = new Regex("<asset>(?<url>[^<]+)</asset>");

        private static string ReturnMediaAssetWrapper(string mediaProvider, string mediaId, string mediaUrl = "", string altText = "")
        {
            System.Text.StringBuilder wrapper = new System.Text.StringBuilder();
            Uri youtubeImageUri = null;
            string mediaProviderUrl = "";
            string privacyUrl = "http://news.gov.bc.ca/privacy";
            string placeholderThumbnailUrl = "/Content/Images/Gov/BC_Gov_News_1280x720.png";
            string mediaType = "";

            switch (mediaProvider)
            {
                case "youtube":
                    mediaProviderUrl = "youtube.com";
                    mediaType = "video";
                    break;
            }

            wrapper.AppendFormat("<div class=\"{0}-wrapper asset {0} {1}\" data-media-type=\"{0}\" data-media-id=\"{2}\">", mediaProvider, mediaType, mediaId);
            wrapper.Append("<div class=\"media-player-container\">");
            wrapper.Append("</div>");
            wrapper.Append("<div class=\"placeholder-container\">");

            if (mediaProvider == "youtube")
            {
                youtubeImageUri = new Uri(string.Format("https://img.youtube.com/vi/{0}/maxresdefault.jpg", mediaId));
                wrapper.AppendFormat("<img alt=\"{2}\" title=\"{2}\" src=\"{0}\" onError=\"this.onerror=null; this.src='{1}';\"/>", youtubeImageUri.ToProxyUrl(), placeholderThumbnailUrl, altText);
            }

            wrapper.Append("<div class=\"overlay-container\">");
            wrapper.Append("<div class=\"outer\">");
            wrapper.Append("<div class=\"inner not-expanded\">");
            wrapper.Append("<div class=\"play-button\">");
            wrapper.Append("<a href=\"javascript:void(0);\" title=\"Play\"></a>");
            wrapper.Append("</div>");
            wrapper.AppendFormat("<div class=\"play-instructions\" id=\"play-instructions-{0}\">", mediaId);
            wrapper.Append("<div class=\"preface\">");
            wrapper.AppendFormat("Press play again to access content from <strong>{0}</strong>. For more information, please read our <a href=\"{1}\">Privacy</a> statement.", mediaProviderUrl, privacyUrl);
            wrapper.Append("</div>");
            wrapper.Append("<div class=\"controls\">");
            wrapper.Append("<div>");
            wrapper.Append("<label>");
            wrapper.Append("<span>");
            wrapper.Append("<input type=\"checkbox\" value=\"1\" class=\"save-preference\" />");
            wrapper.Append("</span>");
            wrapper.AppendFormat("Always allow content from <strong>{0}</strong>", mediaProviderUrl);
            wrapper.Append("</label>");
            wrapper.Append("</div>");
            wrapper.Append("<div>");
            wrapper.Append("<span>Your preference will be saved using cookies.</span>");
            wrapper.Append("</div>");
            wrapper.Append("</div>");
            wrapper.Append("<div class=\"play-close\">");
            wrapper.Append("<a href=\"javascript:void(0);\" title=\"Close\"></a>");
            wrapper.Append("</div>");
            wrapper.Append("</div>");
            wrapper.Append("</div>");
            wrapper.Append("</div>");
            wrapper.Append("</div>");
            wrapper.Append("<div class=\"clear\"></div>");
            wrapper.Append("</div>");
            wrapper.Append("<div class=\"clear\"></div>");
            wrapper.Append("</div>");

            return wrapper.ToString();
        }

        public static HtmlString RenderAssetsInHtml(string bodyHtml, int? maxWidth = null)
        {
            var newhtml = AssetRegex.Replace(bodyHtml, new MatchEvaluator(match =>
            {
                string result;

                string url = match.Groups["url"].Value;

                try
                {
                    Uri uri = new Uri(url);
                    var width = maxWidth ?? 304;

                    if (uri.Host == "www.youtube.com")
                    {
                        var height = maxWidth * 9 / 15;
                        var query = QueryHelpers.ParseQuery(uri.Query);

                        if (query.ContainsKey("v"))
                        {
                            var videoId = query["v"];

                            result = ReturnMediaAssetWrapper("youtube", videoId);
                        }
                        else
                        {
                            result = "";
                        }
                    }
                    else if (uri.Host.EndsWith("staticflickr.com"))
                    {

                        var flickrRegex = new Regex(@"https?:\/\/(farm([0-9]+)|live)\.staticflickr\.com\/([0-9]+)\/([0-9]+)_([0-9a-f]+)(_[a-z])?\.jpg");
                        var flickrMatch = flickrRegex.Match(url);

                        if (flickrMatch.Success)
                        {
                            var flickrUrl = string.Format("https://www.flickr.com/photos/{0}/{1}/", "bcgovphotos", flickrMatch.Groups[4].Value);
                            result = string.Format(
                                         "<div>" +
                                              "<a href='{0}'>" +
                                                  "<img src='{1}'/>" +
                                              "</a>" +
                                         "</div>"
                                         , flickrUrl, uri.ToProxyUrl());
                        }
                        else
                        {
                            result = string.Format(
                                         "<div>" +
                                         "<img src='{0}'/>" +
                                         "</div>"
                                         , uri.ToProxyUrl());
                        }
                    }
                    else
                    {
                        result = "<a href='" + url + "'>" + url + "</a>";
                    }
                }
                catch (UriFormatException)
                {
                    result = "<!--" + url + "-->";
                }

                return "<!--googleoff: all-->" + result + "<!--googleon: all-->";
            }));

            return new HtmlString(newhtml);
        }

        public static HtmlString RenderPostAssetThumbnail(Uri uri, bool renderFlickrAsBackground = false, string altText = "")
        {
            string assetHtml = "";

            try
            {
                if (uri == null)
                    return HtmlString.Empty;

                if (uri.Host == "www.youtube.com")
                {

                    var query = QueryHelpers.ParseQuery(uri.Query);

                    if (!query.ContainsKey("v"))
                        return HtmlString.Empty;

                    var videoId = query["v"];

                    var youtubeIframeUrl = string.Format("//www.youtube.com/embed/{0}?rel=0&amp;modestbranding=1&amp;wmode=transparent", videoId);
                    var imgUri = new Uri(string.Format("https://img.youtube.com/vi/{0}/0.jpg", videoId));

                    assetHtml = string.Format(
                                        "<div class='asset youtube'>" +
                                            "<a href='https://www.youtube.com/watch?v={0}'>" +
                                                "<img src='{1}' alt='{2}' title='{2}' />" +
                                            "</a>" +
                                        "</div>"
                                        , videoId, imgUri.ToProxyUrl(), altText);
                }
                else if (uri.Host.EndsWith("staticflickr.com"))
                {
                    var flickrRegex = new Regex(@"https?:\/\/(farm([0-9]+)|live)\.staticflickr\.com\/([0-9]+)\/([0-9]+)_([0-9a-f]+)(_[a-z])?\.jpg");
                    var flickrMatch = flickrRegex.Match(uri.ToString());

                    if (flickrMatch.Success)
                    {
                        // Get the "n" size flickr image (320x320)
                        //assetUrl = assetUrl.Replace(flickrMatch.Groups[5].Value, "n.jpg");
                        var flickrUrl = string.Format("https://www.flickr.com/photos/{0}/{1}/", "bcgovphotos", flickrMatch.Groups[4].Value);
                        if (renderFlickrAsBackground)
                        {
                            assetHtml = string.Format(
                                            "<div class='asset flickr background'>" +
                                                    // "<a href='{0}'>" +
                                                    "<div class='image-div' style='background-image: url({1})'></div>" +
                                            //  "</a>" +
                                            "</div>"
                                            , flickrUrl, uri.ToProxyUrl());
                        }
                        else
                        {
                            assetHtml = string.Format(
                                            "<div class='asset flickr'>" +
                                                   // "<a href='{0}'>" +
                                                   "<img src='{1}' alt=\"{2}\" title=\"{2}\"/>" +
                                            // "</a>" +
                                            "</div>"
                                            , flickrUrl, uri.ToProxyUrl(), altText);
                        }
                    }
                    else
                    {
                        assetHtml = string.Format(
                                        "<div>" +
                                        "<img src='{0}' alt='{1}' title='{1}'/>" +
                                        "</div>"
                                        , uri.ToProxyUrl(), altText);
                    }
                }
            }
            catch (UriFormatException)
            {
                assetHtml = "<!--" + uri.ToString() + "-->";
            }

            return new HtmlString(assetHtml);
        }

        public static HtmlString RenderPostAsset(Uri uri, int? maxWidth = null, string altText = "")
        {
            string assetHtml = "";

            try
            {
                if (uri == null)
                    return HtmlString.Empty;

                if (uri.Host == "www.youtube.com")
                {
                    var width = maxWidth ?? 304;
                    var height = width * 9 / 16; // 16:9 aspect ratio
                    var query = QueryHelpers.ParseQuery(uri.Query);

                    if (!query.ContainsKey("v"))
                        return HtmlString.Empty;

                    var videoId = query["v"];

                    assetHtml = ReturnMediaAssetWrapper("youtube", videoId, "", altText);

                }
                else if (uri.Host.EndsWith("staticflickr.com"))
                {

                    var flickrRegex = new Regex(@"https?:\/\/(farm([0-9]+)|live)\.staticflickr\.com\/([0-9]+)\/([0-9]+)_([0-9a-f]+)(_[a-z])?\.jpg");
                    var flickrMatch = flickrRegex.Match(uri.ToString());

                    if (flickrMatch.Success)
                    {
                        var flickrUrl = string.Format("https://www.flickr.com/photos/{0}/{1}/", "bcgovphotos", flickrMatch.Groups[4].Value);
                        assetHtml = string.Format(
                                        "<div class='asset flickr'>" +
                                            "<a href='{0}'>" +
                                                "<img src='{1}' alt=\"{2}\" title=\"{2}\"/>" +
                                            "</a>" +
                                        "</div>"
                                        , flickrUrl, uri.ToProxyUrl(), @altText);
                    }
                    else
                    {

                        assetHtml = string.Format(
                                       "<div>" +
                                       "<img src='{0}' alt=\"{1}\" title=\"{1}\" />" +
                                       "</div>"
                                       , uri.ToProxyUrl(), altText);
                    }
                }
            }
            catch (UriFormatException)
            {
                assetHtml = "<!--" + uri.ToString() + "-->";
            }

            return new HtmlString(assetHtml);
        }

        public static string ConvertUrlsToLinks(string txt)
        {
            //Regex regx = new Regex("(https?://|www\\.)([\\w+?\\.\\w+])+([a-zA-Z0-9\\~\\!\\@\\#\\$\\%\\^\\&amp;\\*\\(\\)_\\-\\=\\+\\/\\?\\.\\:\\;\\'\\,]*)?", RegexOptions.IgnoreCase);

            string pattern = @"((https?:\/\/\w+)|www)" //Matches "http://host", "https://host" or "www"
                           + @"(\.[\w\-]+)*"           //Matches zero or more occurances of ".intermediate-host"
                           + @"\.\w+"                  //Matches ".tld"
                           + @"(\/[\w\-]*)*";          //Matches zero or more occurances of "/" or "/path-segment"

            Regex regx = new Regex(pattern, RegexOptions.IgnoreCase);
            MatchCollection mactches = regx.Matches(txt);

            foreach (Match match in mactches)
            {
                Regex facebookLink = new Regex("https://www.facebook.com", RegexOptions.IgnoreCase);
                MatchCollection facebookMatches = facebookLink.Matches(match.Value);
                Regex brokenLinkWithEllipsis = new Regex(".*\\.\\.\\.$");
                MatchCollection brokenLinkMatches = brokenLinkWithEllipsis.Matches(match.Value);
                if (facebookMatches.Count > 0 || (brokenLinkMatches.Count > 0))
                {
                    continue;
                }
                txt = txt.Replace(match.Value, "<a href='" + new UriBuilder(match.Value).Uri.ToString() + "'>" + match.Value + "</a>");
            }
            return txt;
        }
    }
}
