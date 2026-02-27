#if USE_JAVASCRIPT_SIGNALR
using Microsoft.AspNet.SignalR;
# endif
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace Gov.News.Website.Hubs
{
    public class LiveHub : IHostedService
#if USE_JAVASCRIPT_SIGNALR
        : Hub
#endif
    {
        private Task _pollingTask;
        private CancellationTokenSource _cts;
        private readonly Repository _repository;
        private readonly HttpClient _httpClient;
        //private static volatile bool _isWebcasting = false;
        private static volatile bool _isGranvilleLive = false;

        public LiveHub(Repository repository, IHttpClientFactory httpClientFactory)
        {
            _repository = repository;
            _httpClient = httpClientFactory.CreateClient();
        }

        public static bool IsGranvilleLive
        {
            get { return _isGranvilleLive; }
        }

        private static volatile IEnumerable<string> _webcastingPlaylists = null;
        public static IEnumerable<string> WebcastingPlaylists
        {
            get { return _webcastingPlaylists; }
        }

        // checks whether the url passed in is accessible via a http header request
        // if not accessible it returns false
        private async Task<bool> CheckAlive(string url)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
                return response.IsSuccessStatusCode;
            }
            catch (HttpRequestException)
            {
                /*  This is the expected path if the content is not available.
                    A HttpRequestException will be thrown if the status is not successful or if the request fails. */
                return false;
            }
            catch (TaskCanceledException)
            {
                /*  This is the expected path if the content is not available.
                    A TaskCanceledException can be thrown if the request times out. */
                return false;
            }
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            // Create a linked token so we can trigger cancellation outside of this token's cancellation
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            // Store the task we're executing
            _pollingTask = StartWebcastingLivePolling(_cts.Token);
            // If the task is completed then return it, otherwise it's running
            return _pollingTask.IsCompleted ? _pollingTask : Task.CompletedTask;
        }

        async Task StartWebcastingLivePolling(CancellationToken ct)
        {
            //TODO: Toggle this feature a better way during application development
            //if (System.Diagnostics.Debugger.IsAttached)
            //    return;

            while (true)
            {
                if (ct.IsCancellationRequested)
                {
                    break;
                }
                try
                {
                    var homeSettings = await _repository.GetHomeAsync();

                    var granville_setting = homeSettings?.Granville;
                    if (granville_setting == null)
                    {
                        SetGranvilleDead();
                    }
                    else
                    {
                        SetGranvilleLive();
                    }
                    
                    var manifest_url_setting = homeSettings?.LiveWebcastFlashMediaManifestUrl;
                    if (manifest_url_setting == null)
                    {
                        SetDead();
                        continue;
                    }

                    var m3u_playlist_setting = homeSettings.LiveWebcastM3uPlaylist;
                    if (m3u_playlist_setting == null || !await CheckAlive(m3u_playlist_setting))
                    {
                        SetDead();
                        continue;
                    }

                    SetLive(new List<string>() { manifest_url_setting, m3u_playlist_setting });
                }
                catch
                {
                    try
                    {
                        //MIGRATION: Implement Logging
                        //System.IO.File.AppendAllText(System.Web.Hosting.HostingEnvironment.MapPath("~") + @"\..\Log Files\PollWebcastingLive.log", ex + "\r\n");
                    }
                    catch
                    {
#if DEBUG
                        throw;
#endif
                    }

                }
                finally
                {
                    await Task.Delay(new System.TimeSpan(0, 0, 15), ct);
                }
            }
        }
        public async Task StopAsync(CancellationToken cancellationToken)
        {
            // Stop called without start
            if (_pollingTask == null)
            {
                return;
            }

            // Signal cancellation to the executing method
            _cts.Cancel();

            // Wait until the task completes or the stop token triggers
            await Task.WhenAny(_pollingTask, Task.Delay(-1, cancellationToken));

            // Throw if cancellation triggered
            cancellationToken.ThrowIfCancellationRequested();
        }

        // Call all the clients and let them know there Webcasting is not live
        private static void SetDead()
        {
            //_isWebcasting = false;
            _webcastingPlaylists = null;
#if USE_JAVASCRIPT_SIGNALR
            if (!Properties.Settings.Default.SignalREnabled)
                return;
            var context = GlobalHost.ConnectionManager.GetHubContext<LiveHub>();
            //TODO: optionally pass the urls for preview or an image for preview
            context.Clients.All.isLive(false, null);
#endif
        }

        // Call all the clients and let them know there Webcasting is live and pass the urls
        private static void SetLive(IEnumerable<string> links)
        {
            //_isWebcasting = true;
            _webcastingPlaylists = links;
#if USE_JAVASCRIPT_SIGNALR
            if (!Properties.Settings.Default.SignalREnabled)
                return;
            var context = GlobalHost.ConnectionManager.GetHubContext<LiveHub>();
            context.Clients.All.isLive(true, links);
#endif
        }

        private static void SetGranvilleLive()
        {
            _isGranvilleLive = true;
#if USE_JAVASCRIPT_SIGNALR
            if (!Properties.Settings.Default.SignalREnabled)
                return;
            var context = GlobalHost.ConnectionManager.GetHubContext<LiveHub>();
            context.Clients.All.isGranvilleLive(true);
#endif
        }

        private static void SetGranvilleDead()
        {
            _isGranvilleLive = false;
#if USE_JAVASCRIPT_SIGNALR
            if (!Properties.Settings.Default.SignalREnabled)
                return;
            var context = GlobalHost.ConnectionManager.GetHubContext<LiveHub>();
            context.Clients.All.isGranvilleLive(false);
#endif
        }
    }
}
