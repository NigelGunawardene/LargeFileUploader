using System.IO.Compression;
using System.Security.Cryptography;
using Azure.Storage.Blobs;
using LargeFileUploader.Services;
using LargeFileUploader.Settings;
using LargeFileUploader.Streaming;
using Microsoft.Extensions.Options;

namespace LargeFileUploader.Interfaces;

public class AzureBlobService : IAzureBlobService
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly AzureBlobStorageSettings _blobStorageSettings;

    public AzureBlobService(IOptions<AzureBlobStorageSettings> blobStorageSettings)
    {
        _blobStorageSettings = blobStorageSettings.Value;
        _blobServiceClient = new BlobServiceClient(this._blobStorageSettings.ConnectionString);
    }

    public async Task ProcessBlob(Stream stream, string fileName)
    {
        stream.Seek(0, SeekOrigin.Begin);
        using var archive = new ZipArchive(stream);
        foreach (var entry in archive.Entries.Where(e => !string.IsNullOrEmpty(e.Name)))
        {
            await using var entryStream = entry.Open();
            await AddFileToBlobStorage(stream, fileName);
        }
    }
    
    // upload stream to azure blob
    public async Task AddFileToBlobStorage(Stream stream, string fileName)
    {
        var blobStream = new PipeStream();
        var hashStream = new PipeStream();
 
        var blobName = Guid.NewGuid().ToString();
 
        var blobContainerClient = this._blobServiceClient.GetBlobContainerClient(this._blobStorageSettings.ContainerName);
        await blobContainerClient.CreateIfNotExistsAsync();
 
        var blobClient = blobContainerClient.GetBlobClient(blobName);
 
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
            { "hash", hash },
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
        {
            foreach (var pipeStream in pipeStreams)
            {
                await pipeStream.WriteAsync(buffer, 0, length, CancellationToken.None);
            }
        }
 
        foreach (var pipeStream in pipeStreams)
        {
            pipeStream.Complete();
        }
    }
}