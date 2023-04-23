using System.IO.Compression;
using System.Security.Cryptography;
using Azure.Storage.Blobs;
using LargeFileUploader.Services;
using LargeFileUploader.Settings;
using LargeFileUploader.Streaming;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace LargeFileUploader.Interfaces;

public class AzureBlobService : IAzureBlobService
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly AzureBlobStorageSettings _blobStorageSettings;

    public AzureBlobService(IOptions<AzureBlobStorageSettings> blobStorageSettings)
    {
        _blobStorageSettings = blobStorageSettings.Value;
        _blobServiceClient = new BlobServiceClient(_blobStorageSettings.ConnectionString);
    }

    public async Task ProcessBlob(Stream stream, string fileName)
    {
        stream.Seek(0, SeekOrigin.Begin);
        using var archive = new ZipArchive(stream);

        var result = new Node();

        foreach (var entry in archive.Entries)
        {
            var parts = entry.FullName.Split('/');
            var current = result;
            for (var i = 0; i < parts.Length; i++)
            {
                var part = parts[i];
                if (i == parts.Length - 1 && !string.IsNullOrEmpty(entry.Name))
                {
                    var file = new Node();
                    file.Name = part;
                    file.Type = "file";
                    current.Children.Add(file);
                }
                else
                {
                    if (!string.IsNullOrEmpty(part))
                    {
                        var directory = current.Children.FirstOrDefault(o => o.Name == part);
                        if (directory == null)
                        {
                            directory = new Node();
                            directory.Name = part;
                            directory.Type = "directory";
                            current.Children.Add(directory);
                        }

                        current = directory;
                    }
                }
            }

            if (!string.IsNullOrEmpty(entry.Name))
            {
                await using var entryStream = new SeekStream(entry.Open(), Convert.ToInt32(entry.Length));
                await AddFileToBlobStorage(entryStream, entry.Name);
            }
        }

        var serializedString = JsonConvert.SerializeObject(result);


        // foreach (var entry in archive.Entries.Where(e => !string.IsNullOrEmpty(e.Name)))
        // {
        //     await using var entryStream = new SeekStream(entry.Open(), Convert.ToInt32(entry.Length)); // works
        //     // await using var entryStream = new SeekableReadOnlyStream(entry.Open(), Convert.ToInt32(entry.Length)); // works
        //     // ReadableSeekStream and PeekableStream do not work due to the DeflateStream having Length unsupported
        //     // await using var entryStream = entry.Open();
        //     await AddFileToBlobStorage(entryStream, entry.Name);
        // }
    }

    // upload stream to azure blob
    public async Task AddFileToBlobStorage(Stream stream, string fileName)
    {
        // setup - create new ID, as well as necessary azure blob components
        var blobName = Guid.NewGuid().ToString();
        var blobContainerClient =
            _blobServiceClient.GetBlobContainerClient(_blobStorageSettings.ContainerName);
        await blobContainerClient.CreateIfNotExistsAsync();
        var blobClient = blobContainerClient.GetBlobClient(blobName);

        # region old way of doing the upload, where we convert to seekable stream

        // reset stream and calculate hash
        // stream.Seek(0, SeekOrigin.Begin);
        // var sha512 = SHA512.Create();
        // var hashTask = await sha512.ComputeHashAsync(stream);
        // var hash = sha512.Hash != null ? string.Concat(sha512.Hash.Select(b => b.ToString("x2"))) : string.Empty;

        // reset stream and upload blob
        // stream.Seek(0, SeekOrigin.Begin);
        // await blobClient.UploadAsync(stream);

        #endregion


        var blobStream = new PipeStream();
        var hashStream = new PipeStream();

        var fileWriteTask = Task.Run(async () => await blobClient.UploadAsync(blobStream));

        var sha512 = SHA512.Create();
        var hashTask = Task.Run(async () => await sha512.ComputeHashAsync(hashStream));

        var distributeTask = Task.Run(async () => await DistributeAsync(stream, new[] { blobStream, hashStream }));

        await Task.WhenAll(distributeTask, hashTask, fileWriteTask);

        var hash = sha512.Hash != null ? string.Concat(sha512.Hash.Select(b => b.ToString("x2"))) : string.Empty;

        var response = await blobClient.SetMetadataAsync(new Dictionary<string, string>
        {
            { "path", Path.GetDirectoryName(fileName)! },
            { "name", Path.GetFileName(fileName) },
            { "fullName", fileName },
            { "hash", hash }
        });

        // if (response is not null)
        // {
        //     // create blueprint
        //     var blueprint = new DossierBlueprint();
        //     blueprint.Id = Guid.NewGuid().ToString();
        //     blueprint.CreatedTime = DateTime.UtcNow;
        //     blueprint.FileHash = hash;
        //     blueprint.OrganizationId = Guid.NewGuid().ToString();
        //     blueprint.UserId = Guid.NewGuid().ToString();
        //
        //     await this.queueStorageService.AddMessageToQueueAsync(blueprint);
        // }
    }


    private static async Task DistributeAsync(Stream dataStream, PipeStream[] pipeStreams)
    {
        var buffer = new byte[8 * 1024L];
        while (await dataStream.ReadAsync(buffer, 0, buffer.Length, CancellationToken.None) is > 0 and var length)
            foreach (var pipeStream in pipeStreams)
                await pipeStream.WriteAsync(buffer, 0, length, CancellationToken.None);

        foreach (var pipeStream in pipeStreams) pipeStream.Complete();
    }

    private class Node
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public List<Node> Children { get; } = new();
    }
}