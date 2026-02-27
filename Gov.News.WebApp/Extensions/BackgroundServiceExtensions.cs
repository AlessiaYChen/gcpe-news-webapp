using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Gov.News.Website
{
    public static class BackgroundServiceExtensions
    {
        //http://stackoverflow.com/questions/27676140/what-is-the-equivalent-of-registerobject-queuebackgroundworkitem-in-asp-net-5

        public static void UseBackgroundService(this IApplicationBuilder builder, Func<CancellationToken, Task> service)
        {
            var lifetime = (IHostApplicationLifetime)builder.ApplicationServices.GetService(typeof(IHostApplicationLifetime));

            var stoppingToken = lifetime.ApplicationStopping;
            var stoppedToken = lifetime.ApplicationStopped;

            Task serviceTask = Task.Run(() => service(stoppingToken));

            stoppedToken.Register(() =>
            {
                try
                {
                    if (!serviceTask.Wait(TimeSpan.FromSeconds(30)))
                    {
                        // Log: Background service didn't gracefully shutdown. 
                    }
                }
                catch (Exception)
                {
                }
            });
        }
    }
}
