using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Gov.News.Api;
using Gov.News.Api.Models;
using Gov.News.Website.Middleware;
using Gov.News.Website.Models;
using Gov.News.Website.Providers;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Link = Gov.News.Website.Models.ConnectViewModel.ExternalConnectLink;

namespace Gov.News.Website.Controllers
{
    public class DefaultController : Shared.IndexController<Home>
    {
        private readonly IWebHostEnvironment _env;

        public DefaultController(Repository repository, IConfiguration configuration, IWebHostEnvironment env) : base(repository, configuration)
        {
            _env = env;
        }

        [ResponseCache(CacheProfileName = "Default"), Noarchive]
        public async Task<ActionResult> Reference(string reference)
        {
            if (!Regex.IsMatch(reference, @"\d+"))
                throw new ArgumentException();

            var pair = await Repository.ApiClient.Posts.GetKeyFromReferenceAsync(string.Format("NEWS-{0}", reference), Repository.APIVersion);
            if (pair == null)
                return await NotFound();

            return Redirect(NewsroomExtensions.GetPostUri(pair.Value, pair.Key).ToString());
        }

        [ResponseCache(CacheProfileName = "Default"), Noarchive]
        public async Task<ActionResult> Index(string postKind)
        {
            var model = await GetHomePosts(postKind);

            if (model == null)
                return await SearchNotFound();
            ViewBag.GoogleSiteVerification = Properties.Settings.Default.GoogleSiteVerification;
            ViewBag.BingSiteVerification = Properties.Settings.Default.BingSiteVerification;
            if (Properties.Settings.Default.NewsMediaHostUri != null)
            {
                ViewBag.ProxyUrl = Properties.Settings.Default.NewsMediaHostUri.ToString() + "embed/";
            }
            else
            {
                ViewBag.ProxyUrl = new Uri("https://media.news.gov.bc.ca/embed/").ToString();
            }

            return View("HomeView", model);
        }

        [Route("robots.txt")]
        public ContentResult DynamicRobotsFile()
        {
            StringBuilder content = new StringBuilder();

            if (!_env.IsProduction())
            {
                content.AppendLine("user-agent: *");
                content.AppendLine("Disallow: /");
            }
            else
            {
                content.AppendLine("user-agent: *");
                content.AppendLine("Disallow: /assets");
                content.AppendLine("Disallow: /error");
                content.AppendLine("Disallow: /files");

                // search pages (all case variants)
                content.AppendLine("Disallow: /search");
                content.AppendLine("Disallow: /Search");
                content.AppendLine("Disallow: /SEARCH");

                content.AppendLine("Disallow: /subscribe");
            }

            return this.Content(content.ToString(), "text/plain", Encoding.UTF8);
        }

        [ResponseCache(CacheProfileName = "Feed"), Noindex]
        public async Task<ActionResult> Top(string type, string format)
        {
            if (!string.Equals(type, "feed", StringComparison.OrdinalIgnoreCase))
                throw new NotImplementedException();

            var topKeys = IndexModel.GetTopPostKeys(await GetAllCategories());

            var model = await GetSyndicationFeedViewModel("Top Stories", topKeys);
            return await GetNewsFeedContent(format, model, false, false);
        }

        [ResponseCache(CacheProfileName = "Feed"), Noindex]
        public async Task<ActionResult> CategoryTop(string key, string type, string format)
        {
            if (!string.Equals(type, "feed", StringComparison.OrdinalIgnoreCase))
                throw new NotImplementedException();

            var categories = await GetAllCategories();
            var category = categories.Where(c => c.Key == key).ToList();
            var topKeys = IndexModel.GetTopPostKeys(category);

            var model = await GetSyndicationFeedViewModel("Top Stories", topKeys);
            return await GetNewsFeedContent(format, model, false, false);
        }

        [ResponseCache(CacheProfileName = "Feed"), Noindex]
        public async Task<ActionResult> Feature(string type, string format)
        {
            if (!string.Equals(type, "feed", StringComparison.OrdinalIgnoreCase))
                throw new NotImplementedException();

            var featureKeys = IndexModel.GetFeaturePostKeys(await GetAllCategories());

            var model = await GetSyndicationFeedViewModel("Featured Stories", featureKeys);
            return await GetNewsFeedContent(format, model, false, false);
        }

        [ResponseCache(CacheProfileName = "Feed"), Noindex]
        public async Task<ActionResult> CategoryFeature(string key, string type, string format)
        {
            if (!string.Equals(type, "feed", StringComparison.OrdinalIgnoreCase))
                throw new NotImplementedException();

            var categories = await GetAllCategories();
            var category = categories.Where(c => c.Key == key).ToList();
            var topKeys = IndexModel.GetFeaturePostKeys(category);

            var model = await GetSyndicationFeedViewModel("Top Stories", topKeys);
            return await GetNewsFeedContent(format, model, false, false);
        }

        [ResponseCache(CacheProfileName = "Page")]
        public async Task<ActionResult> Connect()
        {
            var model = await GetConnectModel();
            return View("ConnectView", model);
        }

        [Noindex]
        public async Task<ActionResult> Search(string q = null, string date = null, string ministry = null, string sector = null, string city = null, string content = null, string language = null,
            DateTime? fromDate = null, DateTime? toDate = null, string page = null)
        {
            var filters = new Dictionary<string, string>();
            if (!string.IsNullOrEmpty(date))
            {
                filters.Add("Date", date);
            }
            if (ministry != null)
            {
                var ministries = await Repository.GetMinistriesAsync();
                var ministryName = ministries.FirstOrDefault(m => m.Key == ministry)?.Name;
                filters.Add("Ministry", ministryName ?? ministry);
            }
            if (sector != null)
            {
                var sectors = await Repository.GetSectorsAsync();
                var sectorName = sectors.FirstOrDefault(s => s.Key == sector)?.Name;
                filters.Add("Sector", sectorName ?? sector);
            }
            if (!string.IsNullOrEmpty(city))
            {
                filters.Add("City", city);
            }
            if (content != null)
            {
                filters.Add("Content", content);
            }
            if (!string.IsNullOrEmpty(language))
            {
                filters.Add("Language", language);
            }

            var queryModel = new SearchViewModel.SearchQuery(q, fromDate, toDate, filters);
            if (Properties.Settings.Default.NewsMediaHostUri != null)
            {
                ViewBag.ProxyUrl = Properties.Settings.Default.NewsMediaHostUri.ToString() + "embed/";
            }
            else
            {
                ViewBag.ProxyUrl = new Uri("https://media.news.gov.bc.ca/embed/").ToString();
            }

            return View("SearchView", await Search(queryModel, page));
        }

        public new Task<ActionResult> NotFound()
        {
            return SearchNotFound();
        }

        [ResponseCache(CacheProfileName = "Default")]
        public async Task<ActionResult> Sitemap(int? count=null, int skip=0)
        {
            List<Uri> model = new List<Uri>();

            var defaultPostKeys = await Repository.ApiClient.Posts.GetAllKeysAsync("home", "default", "default", count, skip, Repository.APIVersion);
            
            foreach (var pair in defaultPostKeys)
            {
                model.Add(NewsroomExtensions.GetPostUri(pair.Value, pair.Key));
            }

            return View("SitemapView", model);
        }

        public async Task<ActionResult> CarouselImage(string slideId)
        {
            var slide = await Repository.GetSlideAsync(slideId);

            return GetCachedImage(slide?.Image, slide?.ImageType, slide?.Timestamp, null);
        }

        [ResponseCache(CacheProfileName = "Page")]
        public async Task<ActionResult> Privacy()
        {
            var model = await GetBaseModel();
            model.Title = "Privacy";
            return View("PrivacyView", model);
        }

        [ResponseCache(CacheProfileName = "Page")]
        public async Task<ActionResult> Live()
        {
            var model = await GetBaseModel();
            model.Title = "Live";
            return View("LiveView", model);
        }

        [ResponseCache(CacheProfileName = "Page")]
        public ActionResult SiteStatus(bool? showErrors)
        {
            List<string> model = new List<string>();

            model.Add(SiteStatusString("Subscribe API call: ", showErrors, () =>
            {
                IList<KeyValuePair2> tags = Repository.ApiClient.Subscribe.SubscriptionItemsAsync("tags", Repository.APIVersion).Result;
                return tags.Count() > 0 ? "OK" : "Failed";
            }));

            model.Add(SiteStatusString("Newsletters count:  ", showErrors, () =>
            {
                IEnumerable<Newsletter> newsletters = Repository.GetNewslettersAsync().Result;
                return newsletters.Count().ToString();
            }));

            Post post = null;
            model.Add(SiteStatusString("Hub DB access, post key: ", showErrors, () =>
            {
                post = Repository.GetPostAsync("2017FLNR0208-001391").Result;
                return post.Key;
            }));

            model.Add(SiteStatusString("Media proxy url: ", showErrors, () =>
            {
                var client = new System.Net.Http.HttpClient();
                client.DefaultRequestHeaders.Referrer = new Uri(string.Concat(Request.Scheme, "://", Request.Host.ToUriComponent(), Request.PathBase.ToUriComponent(), Request.Path, Request.QueryString));

                var result = client.GetAsync(post.AssetUrl).Result.ReasonPhrase;

                if (result != "OK") throw new Exception(result);
                return "OK";
            }));

            model.Add(SiteStatusString("Post cache size: ", showErrors, () =>
            {
                return Repository._cache[typeof(Post)].Count().ToString();
            }));

            return View("SiteStatus", model);
        }
        public static string SiteStatusString(string s, bool? showErrors, Func<string> func)
        {
            System.Diagnostics.Stopwatch timer = System.Diagnostics.Stopwatch.StartNew();

            string value;
            try
            {
                value = func();
                s = "OK: " + s;
            }
            catch (Exception ex)
            {
                if (showErrors == false) throw;
                Exception inner = ex.InnerException;
                if (inner == null) value = ex.Message;
                else value = (inner.InnerException ?? inner).Message;
            }

            timer.Stop();
            s += value + " (" + (timer.ElapsedMilliseconds) + " ms)";

            return s;
        }

        [ResponseCache(CacheProfileName = "Page")]
        public async Task<ActionResult> Contacts()
        {
            var model = await GetBaseModel();
            model.Title = "Media Contacts";
            return View("CommContacts", model);
        }

        [ResponseCache(CacheProfileName = "Page")]
        public ActionResult Contact()
        {
            return Redirect(Properties.Settings.Default.ContactUri.ToString());
        }

        public ActionResult Error()
        {
            var exception = HttpContext.Features.Get<IExceptionHandlerFeature>();

            ViewData["statusCode"] = HttpContext.Response.StatusCode;
            ViewData["message"] = exception.Error.Message;
            ViewData["stackTrace"] = exception.Error.StackTrace;

            return View("ErrorView");
        }

        public async Task<ConnectViewModel> GetConnectModel()
        {
            var model = new ConnectViewModel();
            await LoadAsync(model);

            model.FacebookLinks = new Link[]
            {                 
                        new Link() { Url = "http://www.facebook.com/BizPaLBC", Title = "BC BizPaL" },
                        new Link() { Url = "https://www.facebook.com/bchonours", Title = "BC Honours" },
                        new Link() { Url = "http://www.facebook.com/pages/BCIC/124363430933347", Title = "Innovate BC" },
                        new Link() { Url = "http://www.facebook.com/YourBCParks", Title = "BC Parks" },
                        new Link() { Url = "http://www.facebook.com/TranBC", Title = "BC Transportation and Infrastructure" },
                        new Link() { Url = "http://www.facebook.com/BCForestFireInfo", Title = "BC Wildfire Service" },
                        new Link() { Url = "http://www.facebook.com/pages/Columbia-River-Treaty-Review/471508369560835?fref=ts", Title = "Columbia River Treaty Review" },
                        new Link() { Url = "http://www.facebook.com/pages/Conservation-Officer-Service/282011641840394", Title = "Conservation Officer Service" },
                        new Link() { Url = "https://www.facebook.com/QuitNowBC", Title = "QuitNowBC" },
                        new Link() { Url = "http://www.facebook.com/BCRecSitesandTrails", Title = "Rec Sites and Trails BC" },
                        new Link() { Url = "https://www.facebook.com/profile.php?id=100088142227724", Title = "Residential Tenancy Branch" },
                        new Link() { Url = "https://www.facebook.com/SuperNaturalBC ", Title = "Super, Natural British Columbia" },
                        new Link() { Url = "http://www.facebook.com/WorkBC", Title = "WorkBC" },
                        new Link() { Url = "https://www.facebook.com/bchousing.org/", Title = "BC Housing" },
                        new Link() { Url = "https://www.facebook.com/PreparedBC/", Title = "PreparedBC" },
                        new Link() { Url = "http://www.facebook.com/BCFireSafety", Title = "BC Fire Safety" },
                        new Link() { Url = "https://www.facebook.com/AgriService-BC-103287979487810", Title = "AgriService BC" },
                        new Link() { Url = "https://www.facebook.com/RoadSafetyBC", Title = "Road Safety BC" },
                        new Link() { Url = "https://www.facebook.com/HealthyBritishColumbia", Title = "HealthyBC" },
                        new Link() { Url = "https://www.facebook.com/EatDrinkBuyBC", Title = "Buy BC" },
                        new Link() { Url = "https://www.facebook.com/EmergencyInfoBC", Title = "Emergency Info BC" },
            }.OrderBy(t => t.Title)
            .Prepend(new Link() { Url = "https://www.facebook.com/GovernmentOfBCChinese", Title = "Government of British Columbia Chinese (卑詩省政府中文官方帳號)", Summary = "" })
            .Prepend(new Link() { Url = "http://www.facebook.com/BCProvincialGovernment", Title = "Government of British Columbia", Summary = "Join us for BC news, information and updates" })
            .ToArray();


            model.YoutubeLinks = new Link[]
            {                        
                         new Link() { Url = "http://www.youtube.com/bchousing1", Title = "BC Housing" },
                         new Link() { Url = "https://www.youtube.com/@BCDigitalTrust", Title = "BC Digital Trust" },
                         new Link() { Url = "http://www.youtube.com/BCPublicService", Title = "BC Public Service" },
                         new Link() { Url = "http://www.youtube.com/user/BCTradeInvest", Title = "BC Trade & Invest" },
                         new Link() { Url = "https://www.youtube.com/@BCWildfireService", Title = "BC Wildfire Service" },
                         new Link() { Url = "http://www.youtube.com/MinistryofTranBC", Title = "BC Ministry of Transportation and Transit" },
                         new Link() { Url = "http://www.youtube.com/CareerTrekBC", Title = "Career Trek BC" },
                         new Link() { Url = "http://www.youtube.com/EmergencyInfoBC", Title = "PreparedBC" },
                         new Link() { Url = "https://www.youtube.com/user/QuitNowBC", Title = "QuitNowBC" },
                         new Link() { Url = "http://www.youtube.com/user/TourismBC", Title = "Super, Natural British Columbia" },
                         new Link() { Url = "http://www.youtube.com/workbc", Title = "WorkBC" },
                         new Link() { Url = "https://www.youtube.com/playlist?list=PL98F546CAAFA58723", Title = "BC Parks" },
            }.OrderBy(t => t.Title).Prepend(new Link() { Url = "http://www.youtube.com/ProvinceofBC", Title = "Province of BC", Summary = "Subscribe to get the latest videos from the Government of British Columbia" }).ToArray();

            model.FlickrLinks = new Link[]
            {
                         new Link() { Url = "http://www.flickr.com/photos/tranbc/", Title = "BC Ministry of Transportation & Transit's photostream" }
            }.OrderBy(t => t.Title).Prepend(new Link() { Url = "http://www.flickr.com/photos/bcgovphotos", Title = "Province of BC", Summary = "View and share the latest photos from the Government of British Columbia" }).ToArray();

            model.TwitterLinks = new Link[]
            {
                        new Link() { Url = "https://x.com/ComplianceBC", Title = "@ComplianceBC", Summary = "Environmental compliance in BC: changes, enforcement and education" },
                        new Link() { Url = "http://x.com/bcgovfireinfo", Title = "@BCGovFireInfo", Summary = "Find updates on significant wildfires around the province" },
                        new Link() { Url = "http://x.com/BC_Housing", Title = "@BC_Housing", Summary = "Learn about housing solutions and the latest projects in BC" },
                        new Link() { Url = "https://x.com/innovate_bc", Title = "@Innovate_BC", Summary = "Info on developing entrepreneurial talent and commercializing technology in BC" },
                        new Link() { Url = "https://x.com/BCTradeInvest", Title = "@BCTradeInvest", Summary = "Find expertise to help your business grow internationally" },
                        new Link() { Url = "http://x.com/CRTreaty", Title = "@CRTreaty", Summary = "Join the discussion on the Columbia River Treaty Review" },
                        new Link() { Url = "http://x.com/DriveBC", Title = "@DriveBC" , Summary = "Get developing road closure & weather information in BC" },
                        new Link() { Url = "http://x.com/emergencyinfobc", Title = "@EmergencyInfoBC" , Summary = "Receive information during extreme weather and natural disasters" },
                        new Link() { Url = "https://x.com/PreparedBC", Title = "@PreparedBC" , Summary = "Ready for a disaster? Get preparedness tips & recovery info here." },
                        new Link() { Url = "http://x.com/govTogetherBC", Title = "@govTogetherBC" , Summary = "Find consultation and engagement opportunities in BC" },
                        new Link() { Url = "http://x.com/quitnowbc", Title = "@QuitNowBC", Summary = "Want to quit smoking? We can help!" },
                        new Link() { Url = "http://x.com/RoadSafetyBC", Title = "@RoadSafetyBC" , Summary = "Get information on road safety and driver behaviour" },
                        new Link() { Url = "http://x.com/TranBC", Title = "@TranBC" , Summary = "Engaging on BC transportation and transit services, projects and safety" },
                        new Link() { Url = "https://x.com/TranBC_LMD", Title = "@TranBC_LMD" , Summary = "Latest info from the Lower Mainland" },
                        new Link() { Url = "http://x.com/WorkBC", Title = "@WorkBC", Summary = "Explore career paths and get tips for finding jobs in British Columbia" },
                        new Link() { Url = "https://x.com/BCSheriffs", Title = "@BCSheriffs", Summary = "Learn about the diverse responsibilities and activities of the BC Sheriff Service" },
                        new Link() { Url = "https://x.com/creativebcs", Title = "@CreativeBCs", Summary = "Find information on BC's film, TV, music, interactive & digital media, books & magazines" },
                        new Link() { Url = "https://x.com/bc_eao", Title = "@BC_EAO", Summary = "Get project updates and environmental assessment information" },
                        new Link() { Url = "https://x.com/BC_FireSafety", Title = "@BC_FireSafety", Summary = "Learn about fire prevention, life safety and what’s happening in the BC fire service" },
                        new Link() { Url = "https://x.com/TenancyBc", Title = "@TenancyBC", Summary = "Information, education and dispute resolution services for landlords and tenants in BC" },
                        new Link() { Url = "https://x.com/_BCCOS", Title = "@_BCCOS", Summary = "Conservation Officer Service" },
                        new Link() { Url = "https://x.com/SpillsInfoBC", Title = "@SpillsInfoBC", Summary = "B.C. Spill Response" },
                        new Link() { Url = "https://x.com/BCProsecution", Title = "@BCProsecution", Summary = "BC Prosecution Service" },
                        new Link() { Url = "https://x.com/EatDrinkBuyBC", Title = "@EatDrinkBuyBC", Summary = "Buy BC helps you choose fresh-tasting food and beverage products that are grown, raised and made right here in BC" },
            }.OrderBy(t => t.Title).Prepend(new Link() { Url = "https://x.com/BCGovNews", Title = "@BCGovNews", Summary = "Read daily news tweets from the Government of British Columbia" }).ToArray();

            model.InstagramLinks = new Link[]
            {
                        new Link() { Url = "https://www.instagram.com/bchonours", Title = "BC Honours", Summary = "" },
                        new Link() { Url = "https://www.instagram.com/bchousing/", Title = "BC Housing", Summary = "" },
                        new Link() { Url = "https://www.instagram.com/yourbcparks/", Title = "BC Parks", Summary = "" },
                        new Link() { Url = "https://www.instagram.com/ministryoftranbc/", Title = "BC Ministry of Transportation and Transit", Summary = "" },
                        new Link() { Url = "https://www.instagram.com/creativebcs/", Title = "Creative BC", Summary = "" },
                        new Link() { Url = "https://www.instagram.com/supernaturalbc/", Title = "Super, Natural British Columbia", Summary = "" },
                        new Link() { Url = "https://www.instagram.com/innovate_bc/", Title = "Innovate BC", Summary = "" },
                        new Link() { Url = "https://www.instagram.com/prepared_bc/", Title = "Prepared BC", Summary = "" },
                        new Link() { Url = "https://www.instagram.com/roadsafetybc/", Title = "Road Safety BC", Summary = "" },
                        new Link() { Url = "https://www.instagram.com/hellohealthybc/", Title = "HealthyBC", Summary = "" },
                        new Link() { Url = "https://www.instagram.com/EatDrinkBuyBC/", Title = "Buy BC", Summary = "" },
                        new Link() { Url = "https://www.instagram.com/bcpublicservice/", Title = "BC Public Service", Summary = "" },
                        new Link() { Url = "https://www.instagram.com/bcgovfireinfo/", Title = "BC Wildfire Service", Summary = "" },
                        new Link() { Url = "https://www.instagram.com/workbc.ca/", Title = "WorkBC", Summary = "" },
            }.OrderBy(t => t.Title).Prepend(new Link() { Url = "https://www.instagram.com/governmentofbc/", Title = "Government of BC", Summary = "" }).ToArray();

            model.BlueskyLinks = new Link[]
            {
            }.OrderBy(t => t.Title).Prepend(new Link() { Url = "http://governmentofbc.bsky.social", Title = "Government of BC", Summary = "Read daily news from the Government of British Columbia" }).ToArray();

            model.ThreadsLinks = new Link[]
           {
               new Link() { Url = "https://www.threads.com/@prepared_bc", Title = "Prepared BC", Summary = "" },
           }.OrderBy(t => t.Title).Prepend(new Link() { Url = "https://www.threads.net/@governmentofbc", Title = "Government of BC", Summary = "" }).ToArray();

            model.BlogsLinks = new Link[]
            {
                        new Link() { Url = "https://www.britishcolumbia.ca/about-trade-and-invest-bc/news-stories/", Title = "BC Trade and Invest" },
                        new Link() { Url = "http://www.tranbc.ca/", Title = "TranBC" },
                        new Link() { Url = "https://www.workbc.ca/plan-career/blog", Title = "WorkBC" },
                        new Link() { Url = "https://engage.gov.bc.ca/bcparksblog", Title = "BC Parks" },
            }.OrderBy(t => t.Title).Prepend(new Link() { Url = "https://engage.gov.bc.ca/govtogetherbc/", Title = "GovTogetherBC" }).ToArray();

            var rssLinks = new List<Link>();
            var ministries = model.Ministries.Select(m => m.Index).OrderBy(c => c.Name == "Office of the Premier" ? 0 : 1).ThenBy(c => c.Name);
            var sectors = await Repository.GetSectorsAsync();
            var tags = await Repository.GetTagsAsync();
            var categories = ministries.Union(sectors).Union(tags).OrderBy(c => c.Name).ToList();
            foreach (var category in categories)
                rssLinks.Add(new Link() { Url = category.GetUri().ToString().TrimEnd('/') + "/feed", Title = category.Name });

            rssLinks.Add(new Link() { Url = "http://www.healthlinkbc.ca/publichealthalerts", Title = "HealthLinkBC" });

            model.RssLinks = rssLinks
                .OrderBy(t => t.Title)
                .Prepend(new Link() { Url = "https://news.gov.bc.ca/factsheets/feed", Title = "Factsheets & Opinion Editorials" })
                .Prepend(new Link() { Url = "https://news.gov.bc.ca/feed", Title = "BC Gov News" }).ToArray();

            model.LinkedinLinks = new Link[]
            {
                new Link() { Url = "https://www.linkedin.com/company/bc-public-service/", Title = "BC Public Service", Summary = ""},
                new Link() { Url = "https://www.linkedin.com/company/official-workbc/", Title = "WorkBC ", Summary = ""},
                new Link() { Url = "https://www.linkedin.com/company/trade-invest-british-columbia/", Title = "Trade & Invest BC", Summary = "" },
            }.Prepend(new Link() { Url = "https://www.linkedin.com/company/bcgov/", Title = "Government of BC", Summary = "" }).ToArray();

            return model;
        }

        public async Task<BaseViewModel> GetBaseModel()
        {
            var model = new BaseViewModel();
            await LoadAsync(model);
            return model;
        }

        public async Task<IList<DataIndex>> GetAllCategories()
        {
            var categoryModels = new List<DataIndex> { await Repository.GetHomeAsync() };
            categoryModels.AddRange(await Repository.GetMinistriesAsync());
            categoryModels.AddRange(await Repository.GetSectorsAsync());
            //categoryModels.AddRange(await Repository.GetThemesAsync());
            return categoryModels;
        }

        public async Task<SyndicationFeedViewModel> GetSyndicationFeedViewModel(string title, IEnumerable<string> postKeys)
        {
            var model = new SyndicationFeedViewModel();
            model.AlternateUri = new Uri(Configuration["NewsHostUri"]);

            model.Title = title;
            model.AlternateUri = null;
            model.Entries = (await Repository.GetPostsAsync(postKeys.Take(ProviderHelpers.MaximumSyndicationItems))).Where(e => e != null);

            return model;
        }
    }
}
