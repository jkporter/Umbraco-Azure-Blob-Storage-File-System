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
            /* IFileSystem fileSystem = new AzureBlobStorageFileSystem("DefaultEndpointsProtocol=https;AccountName=starbuckscoffeestories;AccountKey=XXRHauh+MLVFAPu+o+bHOjGhFD/3HMNu379KWz8V/3Pb0ZBYrjTSreO2KDYTQZo3GyrpiE9iMoxESlnVpSGfIA==", "media");


            foreach (
                var d in
                    Directory.EnumerateDirectories(
                        @"D:\Users\Jonathan\Documents\Visual Studio 14\Projects\UmbracoAzureTest\UmbracoAzureTest\media\")
                )
            {
                Console.WriteLine(d);
            }

            foreach (var dir in fileSystem.GetFiles("created-packages"))
            {
                Console.WriteLine(dir);
            }
            Console.Read(); */

            var prefix = string.Empty;
            var pattern = Regex.Replace("/Test/t?st.*", @"[\*\?]|[^\*\?]+", (m) =>
            {
                switch (m.Value)
                {
                    case "*":
                        return ".*";
                    case "?":
                        return ".?";
                    default:
                        if (m.Index == 0)
                        {
                            prefix = m.Value;
                            return string.Empty;
                        }

                        return Regex.Escape(m.Value);
                }
            });

            Console.WriteLine(prefix);
            Console.WriteLine(pattern);

            Console.Read();
        }
    }
}
