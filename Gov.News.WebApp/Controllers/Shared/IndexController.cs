using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Gov.News.Api.Models;
using Gov.News.Website.Middleware;
using Gov.News.Website.Models;
using Gov.News.Website.Providers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using TimeSpan = System.TimeSpan;

namespace Gov.News.Website.Controllers.Shared
{
    public class IndexController<T> : Shared.NewsroomController where T : DataIndex
    {
        public IndexController(Repository repository, IConfiguration configuration) : base(repository, configuration)
        {
        }

        [ResponseCache(CacheProfileName = "Feed"), Noindex]
        public async Task<ActionResult> Feed(string key, string postKind, string format)
        {
            if (this.GetType().GetTypeInfo().IsDefined(typeof(ObsoleteAttribute), false))
                return NotFound();

            var model = await GetFeedModel(key, postKind);

            if (model == null)
                return NotFound();

            return await GetNewsFeedContent(format, model, Request.Query["newsOnDemand"] == "1", true);
        }

        [Obsolete]
        public ActionResult Embed(string key, string type, string format)
        {
            return StatusCode(StatusCodes.Status410Gone);
        }

        [ResponseCache(CacheProfileName = "Default"), Noindex, Obsolete]
        public ActionResult MoreNews(string key, string postKind)
        {
            if (this.GetType().GetTypeInfo().IsDefined(typeof(ObsoleteAttribute), false))
                return NotFound();

            var collection = (string)RouteData.Values["category"];
            var ministry = collection == "ministries" ? key : null;
            var sector = collection == "sectors" ? key : null;
            //var newstype = ConvertTypeToSingular(type);
            return RedirectToAction("Search", "Default", new { ministry = ministry, sector = sector, content = postKind });
        }

        [ResponseCache(CacheProfileName = "Archive"), Obsolete]
        public ActionResult Archive(string key, string postKind)
        {
            if (this.GetType().GetTypeInfo().IsDefined(typeof(ObsoleteAttribute), false))
                return NotFound();

            var collection = (string)RouteData.Values["category"];
            var ministry = collection == "ministries" ? key : null;
            var sector = collection == "sectors" ? key : null;
            ///var newstype = ConvertTypeToSingular(type);
            return RedirectToAction("Search", "Default", new { ministry = ministry, sector = sector, content = postKind });
        }

        [ResponseCache(CacheProfileName = "Archive"), Noindex, Obsolete]
        public ActionResult Month(string key, string postKind, int year, int? month)
        {
            if (this.GetType().GetTypeInfo().IsDefined(typeof(ObsoleteAttribute), false))
                return NotFound();

            var collection = (string)RouteData.Values["category"];
            var ministry = collection == "ministries" ? key : null;
            var sector = collection == "sectors" ? key : null;
            //var newstype = ConvertTypeToSingular(type);
            if (month.HasValue)
            {
                var yearmonth = new DateTime(year, month.Value, 1).ToString("yyyy-MM-dd") + ".." + new DateTime(year, month.Value, 1).AddMonths(1).AddDays(-1).ToString("yyyy-MM-dd");
                return RedirectToAction("Search", "Default", new { ministry = ministry, sector = sector, content = postKind, daterange = yearmonth });
            }
            else
            {
                var yearmonth = new DateTime(year, 1, 1).ToString("yyyy-MM-dd") + ".." + new DateTime(year, 1, 1).AddYears(1).AddDays(-1).ToString("yyyy-MM-dd");
                return RedirectToAction("Search", "Default", new { ministry = ministry, sector = sector, content = postKind, daterange = yearmonth });
            }
        }

        protected ActionResult GetCachedImage(byte[] imageBytes, string imageType, DateTimeOffset? timestampModified, string fileName)
        {
            if (imageBytes == null)
                return NotFound();

            if (NotModifiedSince(timestampModified))
            {
                return StatusCode(StatusCodes.Status304NotModified);
            }

            //Browsers honor max-age for images but not for pages (e.g Newsletters list)
            HttpContext.Response.Headers["Cache-Control"] = new string[] { "private", "max-age=" + new TimeSpan(0, 1, 0).TotalSeconds.ToString("0") };

            return File(imageBytes, imageType, fileName);
        }

        protected async Task<ActionResult> GetNewsFeedContent(string format, SyndicationFeedViewModel model, bool newsOnDemand, bool resetRssFeeds)
        {
            if (this.GetType().GetTypeInfo().IsDefined(typeof(ObsoleteAttribute), false))
                return NotFound();

            NewsSyndicationFeed feed = new NewsSyndicationFeed(format, model, Repository);

            string content = await feed.GetContentAsync(ControllerContext, newsOnDemand, resetRssFeeds);

            string contentType;
            if (format == "atom")
            {
                contentType = "application/atom+xml";
            }
            else if (format == null || format == "rss2")
            {
                contentType = "application/rss+xml";
            }
            else
            {
                throw new NotImplementedException();
            }
            return Content(content, contentType);
        }

        [ResponseCache(CacheProfileName = "Default"), Noindex]
        public virtual async Task<ActionResult> MoreArticles(string key, string postKind, int offset = 0)
        {
            if (this.GetType().GetTypeInfo().IsDefined(typeof(ObsoleteAttribute), false))
                return NotFound();

            //TEST: Is this the same as ControllerContext.RequestContext.HttpContext.Request.IsAjaxRequest()
            if (ControllerContext.HttpContext.Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                //Set a limit on how many articles a user can load
                //if (offset > 20)
                //   return new EmptyResult();

                var dataIndex = await GetDataIndex(key, (string)RouteData.Values["category"]);

                var model = await GetMoreArticlesModel(dataIndex, postKind, offset);

                return PartialView("MoreArticles", model);
            }
            else
            {
                return NotFound();
            }
        }

        private async Task<HomeViewModel> GetMoreArticlesModel(DataIndex dataIndex, string postKind, int skipCount)
        {
            var latestNews = await Repository.GetLatestPostsAsync(dataIndex, ProviderHelpers.MaximumLatestNewsItemsLoadMore, postKind, GetIndexFilter(dataIndex), skipCount);
            var model = new IndexModel(dataIndex, latestNews);

            return new HomeViewModel() { IndexModel = model };
        }

        public async Task<HomeViewModel> GetHomePosts(string postKind)
        {
            DataIndex homeIndex = await Repository.GetHomeAsync();
            if (homeIndex == null)
                return null;

            int count = ProviderHelpers.MaximumLatestNewsItemsLoadMore + ProviderHelpers.MaximumLatestNewsItems;
            var latestNews = await Repository.GetLatestPostsAsync(homeIndex, count, postKind);
            var model = new HomeViewModel() { IndexModel = new IndexModel(homeIndex, latestNews) };

            if (string.IsNullOrEmpty(postKind))
            {
                model.Title = "Home";

                model.FeedUri = ProviderHelpers.Uri(new Uri(Configuration["NewsHostUri"]), "feed");

                await LoadAsync(model, new List<IndexModel> { model.IndexModel });

                model.SlideItems = await Repository.GetSlidesAsync();

                //await LoadSectors(model);
                //await ProviderHelpers.LoadMediaAssets(model);
            }
            else
            {
                model.Title = Char.ToUpper(postKind[0]) + postKind.Substring(1);

                if (model.Title == "Factsheets") {
                    model.Title = model.Title + " & Opinion Editorials";
                }

                model.FeedUri = ProviderHelpers.Uri(new Uri(Configuration["NewsHostUri"]), postKind + "/" + "feed");
                await LoadAsync(model);
            }

            //model.MoreNewsUri = ProviderHelpers.Uri(Properties.Settings.Default.NewsHostUri, "releases/archive");

            model.Footer = await GetFooter(null);

            return model;
        }

        /*public async Task LoadSectors(HomeViewModel model)
        {
            using (Profiler.StepStatic("Loading Sectors"))
            {
                model.PostSectors = new Dictionary<Post, IEnumerable<Category>>();
                if (model.TopStory != null)
                {
                    IEnumerable<Category> sectors = await Repository.GetPostSectorsAsync(model.TopStory);
                    model.PostSectors.Add(model.TopStory, sectors);
                }

                IEnumerable<Category> postSectors = null;
                if (model.FeatureStory != null && !model.PostSectors.TryGetValue(model.FeatureStory, out postSectors))
                {
                    postSectors = await Repository.GetPostSectorsAsync(model.FeatureStory);
                    model.PostSectors.Add(model.FeatureStory, postSectors);
                }

                if (model.LatestNews != null)
                {
                    foreach (Post post in model.LatestNews)
                    {
                        if (!model.PostSectors.TryGetValue(post, out postSectors))
                        {
                            postSectors = await Repository.GetPostSectorsAsync(post);
                            model.PostSectors.Add(post, postSectors);
                        }
                    }
                }
            }
        }*/

        private async Task<SyndicationFeedViewModel> GetFeedModel(string key, string postKind)
        {
            DataIndex index = await GetDataIndex(key, (string)RouteData.Values["category"]);

            if (index == null)
                return null;

            var model = new SyndicationFeedViewModel();
            model.AlternateUri = new Uri(Configuration["NewsHostUri"]);

            var posts = new List<Post>();
            if (string.IsNullOrEmpty(postKind))
            {
                model.Title = index.Name;
                var loadedPosts = await IndexModel.LoadTopAndFeaturePosts(index, Repository);
                var topPost = loadedPosts.SingleOrDefault(p => p.Key == index.TopPostKey);
                if (topPost != null)
                    posts.Add(topPost);

                var featurePost = loadedPosts.SingleOrDefault(p => p.Key == index.FeaturePostKey);
                if (featurePost != null)
                    posts.Add(featurePost);
            }
            else
            {
                model.Title = index.Name + " " + char.ToUpper(postKind[0]) + postKind.Substring(1);
            }
            posts.AddRange(await Repository.GetLatestPostsAsync(index, ProviderHelpers.MaximumSyndicationItems - posts.Count, postKind, GetIndexFilter(index)));
            model.Entries = posts;

            return model;
        }

        protected Func<Post, bool> GetIndexFilter(DataIndex index)
        {
            switch (index.Kind)
            {
                case "ministries":
                    return MinistryFilter(index.Key); ;
                case "sectors":
                    return SectorFilter(index.Key); ;
                case "themes":
                    return ThemeFilter(index.Key); ;
                case "tags":
                    return TagFilter(index.Key); ;
            }
            return null;
        }

        internal static Func<Post, bool> MinistryFilter(string ministryKey)
        {
            return post => post.LeadMinistryKey == ministryKey || post.MinistryKeys.Any(k => k == ministryKey);
        }

        internal static Func<Post, bool> SectorFilter(string sectorKey)
        {
            return post => post.SectorKeys.Any(k => k == sectorKey);
        }
        internal static Func<Post, bool> TagFilter(string sectorKey)
        {
            return post => post.TagKeys.Any(k => k == sectorKey);
        }
        internal static Func<Post, bool> ThemeFilter(string sectorKey)
        {
            return post => post.ThemeKeys.Any(k => k == sectorKey);
        }

        protected async Task<DataIndex> GetDataIndex(string key, string indexKind)
        {
            if (this is DefaultController || this is PostsController)
            {
                return await Repository.GetHomeAsync();
            }
            else
            {
                return (await GetCategoryList(indexKind)).SingleOrDefault(t => t.Key == key);
            }
        }
        protected async Task<IEnumerable<Category>> GetCategoryList(string categoryKind)
        {
            if (categoryKind.ToLower() == "news-subscribe") categoryKind = "tags"; // handle news-subscribe as an alias for tags

            switch (categoryKind)
            {
                case "ministries":
                    return await Repository.GetMinistriesAsync();
                case "sectors":
                    return await Repository.GetSectorsAsync();
                case "tags":
                    return await Repository.GetTagsAsync();
                case "themes":
                    return await Repository.GetThemesAsync();
            }
            throw new NotImplementedException();
        }

    }
}
