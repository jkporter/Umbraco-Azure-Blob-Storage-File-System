using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JPorter.UmbracoAzureFileSystem;
using Umbraco.Core.IO;

namespace JPorter.UmbracAzureFileSystemTest
{
    class Program
    {
        static void Main(string[] args)
        {
            IFileSystem fileSystem = new AzureBlobStorageFileSystem("DefaultEndpointsProtocol=https;AccountName=starbuckscoffeestories;AccountKey=XXRHauh+MLVFAPu+o+bHOjGhFD/3HMNu379KWz8V/3Pb0ZBYrjTSreO2KDYTQZo3GyrpiE9iMoxESlnVpSGfIA==", "media");


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
            Console.Read();
        }
    }
}
