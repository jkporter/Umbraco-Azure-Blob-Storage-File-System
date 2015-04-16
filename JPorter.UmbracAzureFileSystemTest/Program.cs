using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using JPorter.UmbracoAzureFileSystem;
using Umbraco.Core.IO;

namespace JPorter.UmbracAzureFileSystemTest
{
    class Program
    {
        static void Main(string[] args)
        {
            IFileSystem fileSystem = new AzureBlobStorageFileSystem("UseDevelopmentStorage=true;DevelopmentStorageProxyUri=http://127.0.0.1:10000/", "media");

            var d = new DirectoryInfo(args[0]);
            /* foreach (var file in d.EnumerateFiles("*.*", SearchOption.AllDirectories))
            {

                using (var s = file.OpenRead())
                {
                    Console.WriteLine(file.FullName.Substring(d.FullName.Length + 1));
                    fileSystem.AddFile(file.FullName.Substring(d.FullName.Length + 1), s);
                }
            } */


            
            foreach(var file in fileSystem.GetFiles(@"\SUPERSTREETFIGHTERIV"))
                Console.WriteLine(file);

            Console.Read();
        }

    }
}
