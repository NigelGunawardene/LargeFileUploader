using LargeFileUploader.Attributes;
using LargeFileUploader.Exceptions;
using LargeFileUploader.Helpers;
using LargeFileUploader.Services;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;

namespace LargeFileUploader.Controllers;

[Route("api/[controller]")]
[ApiController]
public class UploadController : ControllerBase
{
    private const long MaxFileSize = 10L * 1024L * 1024L * 1024L; // 10GB, adjust to your need
    private const int LengthLimit = 70;
    private readonly IAzureBlobService _azureBlobService;

    public UploadController(IAzureBlobService azureBlobService)
    {
        _azureBlobService = azureBlobService;
    }

    [HttpPost]
    [DisableFormValueModelBinding]
    [RequestSizeLimit(MaxFileSize)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxFileSize)]
    public async Task ReceiveFile()
    {
        var syncIoFeature = HttpContext.Features.Get<IHttpBodyControlFeature>();
        if (syncIoFeature != null) syncIoFeature.AllowSynchronousIO = true;

        if (Request.ContentType != null && !MultipartRequestHelper.IsMultipartContentType(Request.ContentType))
            throw new BadRequestException("Not a multipart request");

        var boundary =
            MultipartRequestHelper.GetBoundary(MediaTypeHeaderValue.Parse(Request.ContentType), LengthLimit);
        var reader = new MultipartReader(boundary, Request.Body);

        // note: this is for a single file, you could also process multiple files
        var section = await reader.ReadNextSectionAsync();

        if (section == null)
            throw new BadRequestException("No sections in multipart defined");

        if (!ContentDispositionHeaderValue.TryParse(section.ContentDisposition, out var contentDisposition))
            throw new BadRequestException("No content disposition in multipart defined");

        var fileName = contentDisposition.FileNameStar.ToString();
        if (string.IsNullOrEmpty(fileName)) fileName = contentDisposition.FileName.ToString();

        if (string.IsNullOrEmpty(fileName))
            throw new BadRequestException("No filename defined.");

        await using var fileStream = section.Body;
        await _azureBlobService.ProcessBlob(fileStream, fileName);
        // await SendFileSomewhere(fileStream);
    }

// This should probably not be inside the controller class
    // private async Task SendFileSomewhere(Stream stream)
    // {
    //     using var request = new HttpRequestMessage()
    //     {
    //         Method = HttpMethod.Post,
    //         RequestUri = new Uri("YOUR_DESTINATION_URI"),
    //         Content = new StreamContent(stream),
    //     };
    //     using var response = await _httpClient.SendAsync(request);
    //     // TODO check response status etc.
    // }
}