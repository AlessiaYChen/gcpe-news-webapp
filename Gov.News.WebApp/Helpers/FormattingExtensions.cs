using System;
using System.Globalization;
using System.Linq;
using System.Text.Encodings.Web;
using AngleSharp.Dom;
using AngleSharp.Parser.Html;
using Microsoft.AspNetCore.Html;
using Gov.News.Api.Models;
using TimeSpan = System.TimeSpan;

namespace Gov.News.Website.Helpers
{
    public static class FormattingExtensions
    {
        public static string FormatDate(this DateTime date)
        {
            return date.ToString("MMMM d, yyyy h:mm tt");
        }

        public static string ToTitleCase(this string text)
        {
            //TODO: Implement System.Globalization.CultureInfo.InvariantCulture.TextInfo.ToTitleCase

            if (string.IsNullOrEmpty(text))
                return text;

            return char.ToUpper(text[0]) + text.Substring(1);
        }

        static TimeSpan midnight = new TimeSpan(0, 0, 0);
        public static string FormatDateLong(this DateTime date, string locale = "en-CA")
        {
            bool hasTime = date.TimeOfDay != midnight;
            if (locale != "en-CA")
                return date.ToString(hasTime ? "dddd d MMMM yyyy, hh:mm" : "dddd d MMMM yyyy", new CultureInfo(locale));
            return date.ToString(hasTime ? "dddd, MMMM d, yyyy h:mm tt" : "dddd, MMMM d, yyyy");
        }

        public static string FormatDateLong(this DateTimeOffset dateOffset, string locale = "en-CA")
        {
            TimeZoneInfo tzi = null;

            // try the Unix timezone first, as we are deploying to a container.
            try
            {
                tzi = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");
            }
            catch (TimeZoneNotFoundException)
            {
                tzi = TimeZoneInfo.FindSystemTimeZoneById("PST8PDT");
            }

            var date = TimeZoneInfo.ConvertTime(dateOffset.ToUniversalTime().DateTime, TimeZoneInfo.Utc, tzi);

            if (locale != "en-CA")
                return date.ToString("dddd d MMMM yyyy, hh:mm", new CultureInfo(locale));

            return date.ToString("dddd, MMMM d, yyyy h:mm tt");
        }

        public static HtmlString AsHtmlParagraphs(this string excerpt)
        {
            if (string.IsNullOrWhiteSpace(excerpt))
                return new HtmlString(string.Empty);

            var parts = excerpt.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
            return new HtmlString(string.Join(string.Empty, parts.Select(p => string.Format("<p>{0}</p>", p))));
        }

        public static HtmlString WithHtmlBreaks(this string excerpt)
        {
            if (string.IsNullOrWhiteSpace(excerpt))
                return new HtmlString(string.Empty);

            var excerptHtml = HtmlEncoder.Default.Encode(excerpt.Replace("\r\n", "\n")).Replace("&#xA;", "<br />"); // \n gets encoded as &#xA;
            return new HtmlString(excerptHtml);
        }

        public static HtmlString ContactsDetails(this Document doc)
        {
            // chuck 
            string contactsHtml = doc.LanguageId != 3084 ? "<h3>Media Contacts</h3>\n" : "<h3>Renseignements additionnels</h5>\n";
            foreach (var contact in doc.Contacts)
            {
                contactsHtml += string.Format(
                                        "<div class=\"comm-contact\">" +
                                        "<h4>{0}</h4>" +
                                        "{1}</div>"
                                        , contact.Title, contact.Details.WithHtmlBreaks());
            }
            return new HtmlString("<div class=\"comm-contacts\">" + contactsHtml + "</div>");
        }

        public static HtmlString ShowLinks(this HtmlString bodyHtmlString, bool addSubscript = true)
        {
            try
            {
                if (bodyHtmlString == null || !addSubscript)
                    return bodyHtmlString;

                string bodyHtml = bodyHtmlString.ToString();
                var parser = new HtmlParser();
                var doc = parser.Parse(bodyHtml);

                foreach (var link in doc.QuerySelectorAll("a"))
                {
                    string href = link.GetAttribute("href");

                    Uri uri;
                    if (Uri.TryCreate(href, UriKind.Absolute, out uri))
                    {
                        string host = uri.Host;

                        if (host.StartsWith("www."))
                            host = host.Substring("www.".Length);

                        if (!host.Contains("gov.bc.ca") && (link.LastChild != null && link.LastChild.NodeName == "img"))
                        {
                            var node = doc.CreateElement("div");
                            node.SetAttribute("class", "subscript");
                            node.InnerHtml = " (" + host + ") ";

                            link.Insert(AdjacentPosition.AfterEnd, node.OuterHtml);

                            bodyHtml = doc.DocumentElement.OuterHtml;
                        }
                        else if (!host.Contains("gov.bc.ca") && !link.TextContent.ToLower().Contains(host.ToLower()))
                        {
                            var node = doc.CreateElement("span");
                            node.SetAttribute("class", "subscript");
                            node.InnerHtml = " (" + host + ") ";

                            link.Insert(AdjacentPosition.AfterEnd, node.OuterHtml);

                            bodyHtml = doc.DocumentElement.OuterHtml;
                        }
                    }
                }

                return new HtmlString(bodyHtml);
            }
            catch (Exception)
            {
                //Program.ReportException(null, ex);
                return bodyHtmlString;
            }
        }

        public static string ToProxyUrl(this Uri uri)
        {
            //TODO: Toggle this feature a better way during application development
            if (System.Diagnostics.Debugger.IsAttached)
                return uri.ToString();

            if (uri == null) return null;
            var proxyUri = Properties.Settings.Default.NewsMediaHostUri;

            var resultUrl = uri.ToString();

            if (proxyUri != null && !uri.Host.Contains("gov.bc.ca"))
            {
                resultUrl = proxyUri.ToString() + "proxy?url=" + UrlEncoder.Default.Encode(uri.ToString());

                if (!string.IsNullOrEmpty(Properties.Settings.Default.NewsMediaProxyKey))
                    resultUrl = resultUrl + "&token=" + UrlEncoder.Default.Encode(SecurityHelper.GetHMAC_SHA256(uri.ToString(), Properties.Settings.Default.NewsMediaProxyKey));
            }
            return resultUrl;
        }

        public static string ToBytesReadable(this long length)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };

            double len = length;

            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }

            if (order == 0)
            {
                return "1 KB";
            }
            if (order == 1)
            {
                return $"{len:#,##0} {sizes[order]}";
            }
            else
            {
                order--;
                len *= 1024;

                return $"{len:#,##0} {sizes[order]}";
            }
        }
    }
}
