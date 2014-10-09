using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JPorter.UmbracoAzureFileSystem
{
    interface ICloudPropertyProvider
    {
        public void SetProperties(ICloudBlob blob);
    }
}
