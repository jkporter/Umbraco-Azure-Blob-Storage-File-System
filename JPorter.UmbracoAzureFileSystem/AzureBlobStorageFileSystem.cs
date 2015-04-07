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
using System.Net.Http.Headers;

namespace JPorter.UmbracoAzureFileSystem
{
    public class AzureBlobStorageFileSystem : IFileSystem
    {
        readonly CloudBlobContainer _container;

        readonly DateTimeOffset _missingReturnDate = new DateTimeOffset(1601, 1, 1, 0, 0, 0, new TimeSpan(0));

        internal string RelativeAddress { get; private set; }
        private readonly string _rootUrl;

        public AzureBlobStorageFileSystem(string connectionString, string containerName, string relativeAddress, string rootUrl)
            : this(connectionString, containerName)
        {
            RelativeAddress = relativeAddress;
            _rootUrl = rootUrl;
        }

        public AzureBlobStorageFileSystem(string connectionString, string containerName)
        {
            var storageAccount = CloudStorageAccount.Parse(connectionString);
            var client = storageAccount.CreateCloudBlobClient();

            _container = client.GetContainerReference(containerName);
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

        public ICloudPropertyProvider PropertyProvider
        {
            get;
            set;
        }

        protected ICloudBlob GetBlob(string path)
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

            ContentDispositionHeaderValue contentDispositionHeaderValue;
            if (ContentDispositionHeaderValue.TryParse(blob.Properties.ContentDisposition,
                out contentDispositionHeaderValue)) return;
            contentDispositionHeaderValue = new ContentDispositionHeaderValue("inline")
            {
                CreationDate = blob.Properties.LastModified,
                FileName = path,
                FileNameStar = path
            };
            blob.Properties.ContentDisposition = contentDispositionHeaderValue.ToString();
            blob.SetProperties();
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
            return directory.ListBlobsSegmented(true, BlobListingDetails.None, 1, new BlobContinuationToken(), null, null).Results.Any();
        }

        public bool FileExists(string path)
        {
            return GetBlob(GetFullPath(path)).Exists();
        }

        public DateTimeOffset GetCreated(string path)
        {
            var blob = GetBlob(GetFullPath(path));
            if (!blob.Exists())
                return _missingReturnDate;

            ContentDispositionHeaderValue contentDispositionHeaderValue;
            if (ContentDispositionHeaderValue.TryParse(blob.Properties.ContentDisposition,
                out contentDispositionHeaderValue) && contentDispositionHeaderValue.CreationDate.HasValue)
                return contentDispositionHeaderValue.CreationDate.Value;

            throw new PlatformNotSupportedException();
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
            string preWildCard = null;

            var pattern = Regex.Replace(filter, @"[\*\?]|[^\*\?]+", (m) =>
            {
                switch (m.Value)
                {
                    case "*":
                        return ".*?";
                    case "?":
                        return ".";
                    default:
                        if (m.Index != 0) return Regex.Escape(m.Value);
                        preWildCard = m.Value;
                        return string.Empty;
                }
            });

            var directory = GetDirectory(path);
            return
                directory.ListBlobs()
                    .OfType<ICloudBlob>()
                    .Where(blob => Regex.IsMatch(blob.Name, Regex.Escape(preWildCard ?? string.Empty) + pattern, RegexOptions.Singleline))
                    .Select(blob => GetRelativePath(blob.Name));
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
            return GetLastModifed(blob);
        }

        protected DateTimeOffset GetLastModifed(CloudBlobDirectory directory)
        {
            return directory.ListBlobs(true).Cast<ICloudBlob>().Max(blob => GetLastModifed(blob));
        }

        protected DateTimeOffset GetLastModifed(ICloudBlob blob)
        {
            if (!blob.Exists())
                return _missingReturnDate;

            ContentDispositionHeaderValue contentDispositionHeaderValue;
            if (ContentDispositionHeaderValue.TryParse(blob.Properties.ContentDisposition,
                out contentDispositionHeaderValue) && contentDispositionHeaderValue.ModificationDate.HasValue)
                return contentDispositionHeaderValue.ModificationDate.Value;

            if (blob.Properties.LastModified.HasValue)
                return blob.Properties.LastModified.Value;

            throw new Exception();
        }

        public string GetRelativePath(string fullPathOrUrl)
        {
            /* try
            {
                var uri = new Uri(fullPathOrUrl);
                if (uri.IsAbsoluteUri)
                    fullPathOrUrl = uri.AbsolutePath.Substring(uri.AbsolutePath.IndexOf('/', 1));
            }
            catch (Exception)
            {

            } */

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
            return GetBlob(GetFullPath(path)).OpenRead();
        }

        protected string EnsureTrailingSeparator(string path)
        {
            if (!path.EndsWith(Path.DirectorySeparatorChar.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal))
                path = path + Path.DirectorySeparatorChar;
            return path;
        }
    }
}