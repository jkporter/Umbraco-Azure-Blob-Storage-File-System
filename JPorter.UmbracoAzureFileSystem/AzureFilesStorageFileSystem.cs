using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.File;
using Umbraco.Core;
using Umbraco.Core.IO;
using Umbraco.Core.Logging;

namespace JPorter.UmbracoAzureFileSystem
{

    public class AzureFilesStorageFileSystem : IFileSystem
    {
        internal string RootPath { get; private set; }
        private readonly string _rootUrl;

        readonly DateTimeOffset _missingReturnDate = new DateTimeOffset(1601, 1, 1, 0, 0, 0, new TimeSpan(0));

        public AzureFilesStorageFileSystem(string shareName, string rootUrl)
        {
            /* if (string.IsNullOrEmpty(rootPath))
                throw new ArgumentException("The argument 'rootPath' cannot be null or empty.");

            if (string.IsNullOrEmpty(rootUrl))
                throw new ArgumentException("The argument 'rootUrl' cannot be null or empty.");

            if (rootPath.StartsWith("~/"))
                throw new ArgumentException("The rootPath argument cannot be a virtual path and cannot start with '~/'");

            RootPath = rootPath;
            _rootUrl = rootUrl; */

            CloudStorageAccount account = CloudStorageAccount.DevelopmentStorageAccount;
            CloudFileClient client = account.CreateCloudFileClient();
            CloudFileShare share = client.GetShareReference(shareName);
            share.CreateIfNotExists();

            RootPath = share.GetRootDirectoryReference().Name;
            _rootUrl = share.GetRootDirectoryReference().Uri.AbsoluteUri;

            share.GetRootDirectoryReference().GetFileReference("")
        }

        protected CloudFileDirectory GetCloudDirectory(string path)
        {
            return null;
        }

        protected CloudFile GetCloudFile(string path)
        {
            return null;
        }

        public IEnumerable<string> GetDirectories(string path)
        {
            path = EnsureTrailingSeparator(GetFullPath(path));

            try
            {
                var cloudDirectory = GetCloudDirectory(path);
                if (cloudDirectory.Exists())
                    return
                        cloudDirectory.ListFilesAndDirectories()
                            .OfType<CloudFileDirectory>()
                            .Select(subDirectories => GetRelativePath(subDirectories.Uri.AbsoluteUri));
            }
            catch (UnauthorizedAccessException ex)
            {
                LogHelper.Error<AzureFilesStorageFileSystem>("Not authorized to get directories", ex);
            }
            catch (DirectoryNotFoundException ex)
            {
                LogHelper.Error<AzureFilesStorageFileSystem>("Directory not found", ex);
            }

            return Enumerable.Empty<string>();
        }

        public void DeleteDirectory(string path)
        {
            DeleteDirectory(path, false);
        }

        public void DeleteDirectory(string path, bool recursive)
        {
            if (DirectoryExists(path) == false)
                return;

            try
            {
                Directory.Delete(GetFullPath(path), recursive);
            }
            catch (DirectoryNotFoundException ex)
            {
                LogHelper.Error<AzureFilesStorageFileSystem>("Directory not found", ex);
            }

        }

        public bool DirectoryExists(string path)
        {
            return GetCloudDirectory(GetFullPath(path)).Exists();
        }

        public void AddFile(string path, Stream stream)
        {
            AddFile(path, stream, true);
        }

        public void AddFile(string path, Stream stream, bool overrideIfExists)
        {
            var fsRelativePath = GetRelativePath(path);

            var exists = FileExists(fsRelativePath);
            if (exists && overrideIfExists == false) throw new InvalidOperationException(
                $"A file at path '{path}' already exists");

            EnsureDirectory(Path.GetDirectoryName(fsRelativePath));

            if (stream.CanSeek)
                stream.Seek(0, 0);

            GetCloudFile(GetFullPath(fsRelativePath)).UploadFromStream(stream);
        }

        public IEnumerable<string> GetFiles(string path)
        {
            return GetFiles(path, "*.*");
        }

        public IEnumerable<string> GetFiles(string path, string filter)
        {
            var fsRelativePath = GetRelativePath(path);

            var fullPath = EnsureTrailingSeparator(GetFullPath(fsRelativePath));

            var filterPattern = Regex.Replace(filter, @"[\*\?]|[^\*\?]+", (m) =>
            {
                switch (m.Value)
                {
                    case "*":
                        return ".*?";
                    case "?":
                        return ".";
                    default:
                        return Regex.Escape(m.Value);
                }
            });

            var filterRegex = new Regex(filterPattern);

            try
            {
                if (Directory.Exists(fullPath))
                    return
                        GetCloudDirectory(fullPath)
                            .ListFilesAndDirectories()
                            .OfType<CloudFile>()
                            .Where(cloudFile => filterRegex.IsMatch(cloudFile.Name))
                            .Select(cloudFile => GetRelativePath(cloudFile.Uri.AbsoluteUri));
            }
            catch (UnauthorizedAccessException ex)
            {
                LogHelper.Error<AzureFilesStorageFileSystem>("Not authorized to get directories", ex);
            }
            catch (DirectoryNotFoundException ex)
            {
                LogHelper.Error<AzureFilesStorageFileSystem>("Directory not found", ex);
            }

            return Enumerable.Empty<string>();
        }

        public Stream OpenFile(string path)
        {
            var fullPath = GetFullPath(path);
            return GetCloudFile(fullPath).OpenRead();
        }

        public void DeleteFile(string path)
        {
            if (!FileExists(path))
                return;

            try
            {
                GetCloudFile(GetFullPath(path)).Delete();
            }
            catch (FileNotFoundException ex)
            {
                LogHelper.Info<AzureFilesStorageFileSystem>(
                    $"DeleteFile failed with FileNotFoundException: {ex.InnerException}");
            }
        }

        public bool FileExists(string path)
        {
            return GetCloudFile(GetFullPath(path)).Exists();
        }

        public string GetRelativePath(string fullPathOrUrl)
        {
            var relativePath = fullPathOrUrl
                .TrimStart(_rootUrl)
                .Replace('/', Path.DirectorySeparatorChar)
                .TrimStart(RootPath)
                .TrimStart(Path.DirectorySeparatorChar);

            return relativePath;
        }

        public string GetFullPath(string path)
        {
            //if the path starts with a '/' then it's most likely not a FS relative path which is required so convert it
            if (path.StartsWith("/"))
            {
                path = GetRelativePath(path);
            }

            return !path.StartsWith(RootPath)
                ? Path.Combine(RootPath, path)
                : path;
        }

        public string GetUrl(string path)
        {
            return _rootUrl.TrimEnd("/") + "/" + path
                .TrimStart(Path.DirectorySeparatorChar)
                .Replace(Path.DirectorySeparatorChar, '/')
                .TrimEnd("/");
        }

        public DateTimeOffset GetLastModified(string path)
        {
            if(DirectoryExists(path))
                return GetCloudDirectory(GetFullPath(path)).Properties.LastModified.GetValueOrDefault(_missingReturnDate).ToUniversalTime();

            var cloudFile = GetCloudFile(GetFullPath(path));
            if (cloudFile.Properties.ContentDisposition != null)
            {
                ContentDispositionHeaderValue value = new ContentDispositionHeaderValue(cloudFile.Properties.ContentDisposition);
                return value.ModificationDate.GetValueOrDefault(cloudFile.Properties.LastModified.GetValueOrDefault(_missingReturnDate)).ToUniversalTime();
            }

            return cloudFile.Properties.LastModified.GetValueOrDefault(_missingReturnDate).ToUniversalTime();
        }

        public DateTimeOffset GetCreated(string path)
        {
            if (DirectoryExists(path))
                return _missingReturnDate;

            var cloudFile = GetCloudFile(GetFullPath(path));
            if(cloudFile.Properties.ContentDisposition != null)
            {
                ContentDispositionHeaderValue value = new ContentDispositionHeaderValue(cloudFile.Properties.ContentDisposition);
                return value.CreationDate.GetValueOrDefault(_missingReturnDate).ToUniversalTime();
            }

            return _missingReturnDate;
        }

        #region Helper Methods

        protected virtual void EnsureDirectory(string path)
        {
            path = GetFullPath(path);
            Directory.CreateDirectory(path);
        }

        protected string EnsureTrailingSeparator(string path)
        {
            if (!path.EndsWith(Path.DirectorySeparatorChar.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal))
                path = path + Path.DirectorySeparatorChar;

            return path;
        }

        #endregion
    }
}
