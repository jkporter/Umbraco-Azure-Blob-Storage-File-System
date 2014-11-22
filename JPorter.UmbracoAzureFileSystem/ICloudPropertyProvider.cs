using Microsoft.WindowsAzure.Storage.Blob;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JPorter.UmbracoAzureFileSystem
{
    public interface ICloudPropertyProvider
    {
        void SetProperties(ICloudBlob blob);
    }
}
