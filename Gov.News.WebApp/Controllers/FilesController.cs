#define USE_AZURE

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Gov.News.Website.Controllers
{
    public class FilesController : Shared.NewsroomController
    {
        public FilesController(Repository repository, IConfiguration configuration) : base(repository, configuration)
        {
        }

        public async Task<ActionResult> Single(string path)
        {
            //Microsoft Word calls with "Microsoft-WebDAV-MiniRedir/6.1.7601" user agent when opening a Word document from corporate network.
            if (string.IsNullOrEmpty(path))
                return StatusCode(StatusCodes.Status403Forbidden);

            if (path.Any(e => Path.GetInvalidPathChars().Contains(e)))
                return await SearchNotFound();

#if USE_AZURE
            try
            {
                Stream stream;
                try
                {
                    stream = await GetAzureStream("files/" + path.ToLower());
                }
                catch (Exception)
                {
                    stream = await GetAzureStream("files/" + path); // for backwards compatibility
                }
            string contentType;
            if (!new FileExtensionContentTypeProvider().TryGetContentType(path, out contentType))
                contentType = "application/octet-stream";

                return File(stream, contentType);
            }
            catch (Exception)
            {
                //Will redirect to search page if file doesn't exist or there is an error.
            }
#else
            string localPath = Path.GetFullPath(Path.Combine(Properties.Settings.Default.ContentFilesUnc, path));

            //Verify that file is not outside of the "files" folder.
            if (!localPath.StartsWith(Path.Combine(Properties.Settings.Default.ContentFilesUnc).TrimEnd('\\') + "\\"))
                return await SearchNotFound();

            string contentType;
            if (!new FileExtensionContentTypeProvider().TryGetContentType(localPath, out contentType))
                contentType = "application/octet-stream";

            if (System.IO.File.Exists(localPath))
            {
                //var file = System.IO.File.ReadAllBytes(localPath);
                //return File(file, contentType);

                return PhysicalFile(localPath, contentType);
            }
#endif
            return await SearchNotFound();
        }
    }
}
