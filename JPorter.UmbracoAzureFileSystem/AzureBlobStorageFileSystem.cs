using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Umbraco.Core.IO;
using Umbraco.Core;
using System.Web.Hosting;

namespace JPorter.UmbracoAzureFileSystem
{
    public class AzureBlobStorageFileSystem : IFileSystem
    {
        readonly CloudBlobClient _client;
        readonly CloudBlobContainer _container;

        internal string RelativeAddress { get; private set; }
        private readonly string _rootUrl;

        public AzureBlobStorageFileSystem(string connectionString, string containerName)
        {
            var storageAccount = CloudStorageAccount.Parse(connectionString);
            _client = storageAccount.CreateCloudBlobClient();

            _container = _client.GetContainerReference(containerName);
            _container.CreateIfNotExists();

            // Container Relative
            RelativeAddress = string.Empty;
            _rootUrl = _container.Uri.ToString();
        }

        public AzureBlobStorageFileSystem(string connectionString, string containerName, string relativeAddress)
            : this(connectionString, containerName)
        {
            RelativeAddress = relativeAddress;
            _rootUrl = _container.GetDirectoryReference(relativeAddress).Uri.ToString();
        }

        public AzureBlobStorageFileSystem(string connectionString, string containerName, string relativeAddress, string rootUrl)
            : this(connectionString, containerName, relativeAddress)
        {
            _rootUrl = rootUrl;
        }

        public ICloudPropertyProvider PropertyProvider
        {
            get;
            set;
        }

        private ICloudBlob GetBlob(string path)
        {
            return _container.GetBlockBlobReference(path.Replace(Path.DirectorySeparatorChar, '/'));
        }

        private CloudBlobDirectory GetDirectory(string path)
        {
            path = path.TrimStart(Path.DirectorySeparatorChar);
            return _container.GetDirectoryReference(path.Replace(Path.DirectorySeparatorChar, '/'));
        }

        public void AddFile(string path, Stream stream)
        {
            AddFile(path, stream, true);
        }

        public void AddFile(string path, Stream stream, bool overrideIfExists)
        {
            var blob = GetBlob(GetFullPath(path));
            if (!overrideIfExists && blob.Exists())
                throw new InvalidOperationException(string.Format("A file at path '{0}' already exists", path));

            if (stream.CanSeek)
                stream.Seek(0, 0);

            blob.UploadFromStream(stream);

            PropertyProvider.SetProperties(blob);
        }

        public void DeleteDirectory(string path)
        {
            DeleteDirectory(path, false);
        }

        public void DeleteDirectory(string path, bool recursive)
        {
            var directory = GetDirectory(GetFullPath(path));
            if (!recursive)
            {
                if (DirectoryExists(directory))
                    throw new InvalidOperationException(string.Format("A directory at path '{0}' is not empty", path));
                return;
            }

            foreach (var blob in directory.ListBlobs(true).Cast<ICloudBlob>())
                DeleteFile(blob);
        }

        public void DeleteFile(string path)
        {
            DeleteFile(GetBlob(GetFullPath(path)));
        }

        protected void DeleteFile(ICloudBlob blob)
        {
            blob.DeleteIfExists();
        }

        public bool DirectoryExists(string path)
        {
            return DirectoryExists(GetDirectory(GetFullPath(path)));
        }

        protected bool DirectoryExists(CloudBlobDirectory directory)
        {
            return directory.ListBlobsSegmented(false, BlobListingDetails.None, 1, new BlobContinuationToken(), null, null).Results.Any();
        }

        public bool FileExists(string path)
        {
            return GetBlob(GetFullPath(path)).Exists();
        }

        public DateTimeOffset GetCreated(string path)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<string> GetDirectories(string path)
        {
            path = EnsureTrailingSeparator(GetFullPath(path));
            return GetDirectory(path).ListBlobs().OfType<CloudBlobDirectory>().Select(dir => GetRelativePath(dir.Prefix.TrimEnd("/")));
        }

        public IEnumerable<string> GetFiles(string path)
        {
            return GetFiles(path, "*.*");
        }

        public IEnumerable<string> GetFiles(string path, string filter)
        {
            path = EnsureTrailingSeparator(GetFullPath(path));

            var prefix = string.Empty;
            var pattern = Regex.Replace(filter, @"[\*\?]|.+?", (m) =>
            {
                switch (m.Value)
                {
                    case "*":
                        return ".*";
                    case "?":
                        return ".?";
                    default:
                        if (m.Index == 0)
                            prefix = m.Value;
                        return Regex.Escape(m.Value);
                }
            });

            var directory = GetDirectory(path);
            return directory.Container.ListBlobs(directory.Prefix + prefix).OfType<ICloudBlob>().Where(blob => Regex.IsMatch(blob.Name, pattern)).Select(blob => GetRelativePath(blob.Name));
        }

        public string GetFullPath(string path)
        {
            return !path.StartsWith(RelativeAddress)
            ? Path.Combine(RelativeAddress, path)
            : path;
        }

        public DateTimeOffset GetLastModified(string path)
        {
            var blob = GetBlob(GetFullPath(path));
            return blob.Properties.LastModified.GetValueOrDefault();
        }

        public string GetRelativePath(string fullPathOrUrl)
        {
            var relativePath = fullPathOrUrl
            .TrimStart(_rootUrl)
            .Replace('/', Path.DirectorySeparatorChar)
            .TrimStart(RelativeAddress)
            .TrimStart(Path.DirectorySeparatorChar);
            return relativePath;
        }

        public string GetUrl(string path)
        {
            return _rootUrl.TrimEnd("/") + "/" + path
            .TrimStart(Path.DirectorySeparatorChar)
            .Replace(Path.DirectorySeparatorChar, '/')
            .TrimEnd("/");
        }

        public Stream OpenFile(string path)
        {
            var fullPath = GetFullPath(path);
            return GetBlob(fullPath).OpenRead();
        }
        protected string EnsureTrailingSeparator(string path)
        {
            if (!path.EndsWith(Path.DirectorySeparatorChar.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal))
                path = path + Path.DirectorySeparatorChar;
            return path;
        }
    }
}