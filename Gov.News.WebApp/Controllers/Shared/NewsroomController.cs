using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Encodings.Web;
using System.Net.Http;
using System.Threading.Tasks;
using Gov.News.Website.Helpers;
using Gov.News.Website.Middleware;
using Gov.News.Website.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

namespace Gov.News.Website.Controllers.Shared
{
    public class NewsroomController : BaseController
    {
        public NewsroomController(Repository repository, IConfiguration configuration) : base(repository, configuration)
        {
        }

        protected async Task<ActionResult> SearchNotFound()
        {
            //#if DEBUG
            //            return await Task.FromResult(HttpNotFound());
            //#else
            string path = Request.Path;

            string query = path.Contains('/') ? path.Substring(path.LastIndexOf('/') + 1) : path;
            query = query.Replace("-", " ");

            ViewBag.Status404NotFound = true;

            return await SearchNotFound(query);
            //#endif
        }

        [Noindex]
        private async Task<ActionResult> SearchNotFound(string query)
        {
            var model = await Search(new SearchViewModel.SearchQuery(query));

            Response.StatusCode = 404;

            return View("~/Views/Default/SearchView.cshtml", model);
        }

        protected async Task<SearchViewModel> Search(SearchViewModel.SearchQuery query, string page = null)
        {
            query ??= new SearchViewModel.SearchQuery(null);
            var model = new SearchViewModel();
            int ResultsPerPage = 10;
            model.ResultsPerPage = ResultsPerPage;
            model.Query = query;

            await LoadAsync(model);

#if !DEBUG
            try
            {
#endif
            model.Title = "Search";

            bool isTranslationsSearch = string.Equals(query.Text, "translation", StringComparison.OrdinalIgnoreCase)
                || string.Equals(query.Text, "translations", StringComparison.OrdinalIgnoreCase);
            string requestPath = Properties.Settings.Default.AzureSearchUri.ToString();
            if (!string.IsNullOrEmpty(query.Text))
            {
                if (!isTranslationsSearch)
                {
                    requestPath += string.Format("&{0}={1}", "search", UrlEncoder.Default.Encode(query.Text));
                }
                requestPath += string.Format("&{0}={1}", "searchMode", "all");
            }
            //if (!string.IsNullOrEmpty(query.DateRange))
            //requestPath += string.Format("+{0}:{1}", "daterange", query.DateRange);

            requestPath += string.Format("&{0}={1}", "$top", Convert.ToString(ResultsPerPage));

            requestPath += string.Format("&{0}={1}", "$select", "key,releaseType,documentsHeadline,documentsSubheadline,summary,publishDateTime,hasMediaAssets,assetUrl,translations,hasTranslations, socialMediaHeadline");

            requestPath += string.Format("&{0}={1}", "$orderby", "publishDateTime desc");

            var facets = new Dictionary<string, string> { { "languages", "Language" }, { "collection", "Date" }, { "ministries", "Ministry" }, { "sectors", "Sector" }, { "location", "City" }, { "releaseType", "Content" } };

            bool useCustomRange = query.UseCustomRange();
            foreach (var facet in facets)
            {
                if (facet.Value == "Date" && useCustomRange) continue;
                if (query.Filters?.ContainsKey(facet.Value) != true)
                {
                    if (facet.Value.ToLower() == "language" || facet.Value.ToLower() == "ministry") 
                    {
                        requestPath += string.Format("&{0}={1},count:0", "facet", facet.Key);
                    }else
                    {
                        requestPath += string.Format("&{0}={1}", "facet", facet.Key);
                    }
                }
            }

            var filters = new List<string>();
            if (isTranslationsSearch)
            {
                filters.Add("hasTranslations eq true");
                filters.Add("translations ne null");
                filters.Add("translations ne ''");
            }
            if (useCustomRange)
            {
                filters.Add("publishDateTime le " + query.ToDate.ToUniversalTime().AddDays(1).ToString("s") + "Z"); // include the selected day too
                filters.Add("publishDateTime ge " + query.FromDate.ToUniversalTime().ToString("s") + "Z");
            }
            if (query.Filters != null)
            {
                foreach (var filter in query.Filters)
                {
                    string facetKey = facets.SingleOrDefault(f => f.Value == filter.Key).Key;
                    filters.Add(string.Format(facetKey.EndsWith('s') ? "{0}/any(t: t eq '{1}')" : "{0} eq '{1}'", facetKey, filter.Value.Replace("'", "''")));
                }
            }
            if (filters.Count != 0)
            {
                requestPath += string.Format("&{0}={1}", "$filter", string.Join(" and ", filters));
            }

            //This should match page meta data date format, which is now "yyyy-mm"
            //if (query.YearMonth != null)
            //    requestPath += String.Format("&{0}={1}", "partialfields", "DC%252Edate%252Eissued:" + query.YearMonth);

            requestPath += string.Format("&{0}={1}", "$count", "true");

            int skip = (int.Parse(page ?? "1") - 1) * ResultsPerPage;
            if (skip > 0)
            {
                requestPath += string.Format("&{0}={1}", "$skip", skip);
            }

            dynamic searchServiceResult = null;
            using (Profiler.StepStatic("Calling search.gov.bc.ca"))
            {
                using var client = new HttpClient();
                using var request = new HttpRequestMessage(HttpMethod.Get, requestPath);
                request.Headers.Add("api-key", Properties.Settings.Default.AzureSearchKey);

                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                var res = await response.Content.ReadAsStringAsync();
                searchServiceResult = JsonConvert.DeserializeObject<dynamic>(res);
            }

            model.Count = searchServiceResult["@odata.count"];

            model.FirstResult = Math.Min(Math.Max(skip + 1, 1), (model.Count / ResultsPerPage) * ResultsPerPage + 1);

            model.LastResult = Math.Min(model.FirstResult + ResultsPerPage - 1, model.Count);

            model.Query = query;

            var searchFacets = searchServiceResult["@search.facets"];
            if (searchFacets != null)
            {
                foreach (var facet in facets) // iterate in the order we asked for
                {
                    var facetHits = new List<SearchViewModel.FacetHit>();
                    string filteredFacet;
                    var searchFacet = searchFacets[facet.Key];
                    if (searchFacet != null)
                    {
                        foreach (var facetHit in searchFacet)
                        {
                            string facetHitValue = facetHit["value"];
                            if (string.IsNullOrEmpty(facetHitValue)) continue;
                            facetHits.Add(new SearchViewModel.FacetHit
                            {
                                Value = facetHitValue,
                                Count = facetHit["count"]
                            });
                        }
                    }
                    else if (query.Filters != null && query.Filters.TryGetValue(facet.Value, out filteredFacet))
                    {
                        facetHits.Add(new SearchViewModel.FacetHit
                        {
                            Value = filteredFacet,
                            Count = model.Count
                        });
                    }
                    bool isDateCollection = facet.Key == "collection";
                    model.FacetResults.Add(facet.Value, isDateCollection ? facetHits.OrderByDescending(f => f.Value) : facetHits.OrderByDescending(f => f.Count));
                }
            }

            if (searchServiceResult.value != null)
            {
                foreach (var result in searchServiceResult.value)
                {
                    string key = result["key"];

                    string postKind = result["releaseType"];
                    postKind = postKind.EndsWith("y") ? postKind.Substring(0, postKind.Length - 1) + "ies" : postKind + "s";

                    IEnumerable<object> titles = result["documentsHeadline"];
                    IEnumerable<object> headlines = result["documentsSubheadline"];
                    string translations = result["translations"];
                    bool hasTranslations = result["hasTranslations"];

                    string assetUrl = result["assetUrl"];
                    var date = (DateTimeOffset)DateTimeOffset.Parse(Convert.ToString(result["publishDateTime"]));

                    model.Results.Add(new SearchViewModel.Result()
                    {
                        Title = System.Net.WebUtility.HtmlDecode(titles.FirstOrDefault().ToString()),
                        Headline = System.Net.WebUtility.HtmlDecode(headlines?.FirstOrDefault().ToString()),
                        Uri = NewsroomExtensions.GetPostUri(postKind.ToLower(), key),
                        Description = result["summary"],
                        HasMediaAssets = result["hasMediaAssets"],
                        HasTranslations = translations != null && hasTranslations,
                        PublishDate = DateTime.Parse(date.FormatDateLong()),
                        ThumbnailUri = NewsroomExtensions.GetThumbnailUri(assetUrl),
                        AssetUrl = result["assetUrl"],
                        SocialMediaHeadline = result["socialMediaHeadline"]
                    });
                }
            }


            model.LastPage = Math.Min(Convert.ToInt32(Math.Ceiling(model.Count / (decimal)ResultsPerPage)), 100);
#if !DEBUG
            }
            catch
            {
                model.Success = false;

                //TODO: Report exception message
            }
#endif

            return model;
        }
        public async Task<System.IO.Stream> GetAzureStream(string blobName)
        {
            var client = new System.Net.Http.HttpClient();

            return await client.GetStreamAsync(new Uri(Repository.ContentDeliveryUri, blobName));
        }

        protected bool NotModifiedSince(DateTimeOffset? timestamp)
        {
            var modifiedSpan = timestamp - Request.GetTypedHeaders().IfModifiedSince;

            // Ignore milliseconds because browsers are not supposed to store them
            if (modifiedSpan.HasValue && modifiedSpan.Value.TotalMilliseconds < 1000)
            {
                return true;
            }
            Response.GetTypedHeaders().LastModified = timestamp;
            return false;
        }
    }
}
