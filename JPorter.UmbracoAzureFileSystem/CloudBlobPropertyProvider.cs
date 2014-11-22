using System.IO;
using Microsoft.Web.Administration;
using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Hosting;

namespace JPorter.UmbracoAzureFileSystem
{
    class StaticContentCloudBlobPropertyProvider : ICloudPropertyProvider
    {
        public void SetProperties(ICloudBlob blob)
        {
            using (var serverManager = new ServerManager())
            {
                var siteName = HostingEnvironment.ApplicationHost.GetSiteName();
                var config = serverManager.GetWebConfiguration(siteName);
                var staticContentSection = config.GetSection("system.webServer/staticContent");
                var staticContentCollection = staticContentSection.GetCollection();

                var mimeMap =
                    staticContentCollection.SingleOrDefault(
                        c =>
                            c.GetAttributeValue("fileExtension") != null &&
                            String.Compare(c.GetAttributeValue("fileExtension").ToString(), Path.GetExtension(blob.Name),
                                StringComparison.OrdinalIgnoreCase) == 0);

                if (mimeMap == null) return;
                var mimeType = mimeMap.GetAttributeValue("mimeType").ToString();
                blob.Properties.ContentType = mimeType.Split(';')[0];
            }
        }
    }

}
