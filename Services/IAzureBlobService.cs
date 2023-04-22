namespace LargeFileUploader.Services;

public interface IAzureBlobService
{
    Task ProcessBlob(Stream stream, string fileName);
    Task AddFileToBlobStorage(Stream stream, string fileName);
}