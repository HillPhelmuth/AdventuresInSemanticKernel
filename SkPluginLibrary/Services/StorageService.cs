using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.Storage.Blobs;

namespace SkPluginLibrary.Services
{
    public class StorageService
    {
        private readonly BlobContainerClient _containerClient;

        public StorageService(BlobServiceClient blobServiceClient)
        {
            var containers = blobServiceClient.GetBlobContainers();
            if (containers is null || !containers.Any(x => x.Name.Contains("skpluginlibrary")))
            {
                _containerClient = blobServiceClient.CreateBlobContainer("skpluginlibrary");
            }
            else
            {
                _containerClient = blobServiceClient.GetBlobContainerClient("skpluginlibrary");
            }
        }
        public void CreateZipStream(Dictionary<string, byte[]> files, string zipFileName = "docs.zip")
        {
            using var zipMemoryStream = new MemoryStream();
            using (var archive = new ZipArchive(zipMemoryStream, ZipArchiveMode.Update, true))
            {
                foreach (var file in files)
                {
                    var fileName = file.Key;
                    var fileContent = file.Value;

                    var entry = archive.CreateEntry(fileName);

                    using var entryStream = entry.Open();
                    entryStream.Write(fileContent, 0, fileContent.Length);
                }
            }

            // Reset the position of the memory stream before returning it
            zipMemoryStream.Position = 0;

            UploadStreamToBlob(zipMemoryStream, zipFileName);
        }

        private async void UploadStreamToBlob(MemoryStream zipMemoryStream, string blobName)
        {

            var blobClient = _containerClient.GetBlobClient(blobName);
            await blobClient.UploadAsync(zipMemoryStream, true);

            // Reset the memory stream position before closing
            zipMemoryStream.Position = 0;
            zipMemoryStream.Close();
        }
    }
}
