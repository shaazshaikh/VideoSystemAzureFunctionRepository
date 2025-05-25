using System.Text;
using System.Net.Http.Headers;
using Azure.Storage.Queues.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using VideoSystemAzureFunction.Models;
using System.Diagnostics;

namespace VideoSystemAzureFunction.AzureFunctions
{
    public class TranscodeVideoFunction
    {
        private readonly ILogger<TranscodeVideoFunction> _logger;

        public TranscodeVideoFunction(ILogger<TranscodeVideoFunction> logger)
        {
            _logger = logger;
        }

        [Function(nameof(TranscodeVideoFunction))]
        public async Task Run([QueueTrigger("transcode-queue", Connection = "AzureWebJobsStorage")] QueueMessage message)
        {
            var messageBytes = Convert.FromBase64String(message.MessageText);
            var jsonMessage = Encoding.UTF8.GetString(messageBytes);
            var videoMessage = JsonConvert.DeserializeObject<VideoModel>(jsonMessage);
            _logger.LogInformation($"processing video: {videoMessage.BlobName}");

            var clientId = Environment.GetEnvironmentVariable("ClientId");
            var clientSecret = Environment.GetEnvironmentVariable("ClientSecret");
            string token = string.Empty;
            using (var authClient = new HttpClient())
            {
                var authRequestBody = new
                {
                    ClientId = clientId,
                    ClientSecret = clientSecret
                };
                var jsonBody = new StringContent(JsonConvert.SerializeObject(authRequestBody), Encoding.UTF8, "application/json");
                var tokenReponse = await authClient.PostAsync("https://localhost:7279/api/login/getClientToken", jsonBody);
                token = await tokenReponse.Content.ReadAsStringAsync();               
            }

            string sasUri = string.Empty;
            using (var fileClient = new HttpClient())
            {
                fileClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                var fileBlobUri = $"https://localhost:7082/api/file/getSASUrl?filePath={Uri.EscapeDataString(videoMessage.BlobName)}";
                var sasResponse = await fileClient.GetAsync(fileBlobUri);
                sasUri = await sasResponse.Content.ReadAsStringAsync();
                sasUri = sasUri.Trim().Trim('"');
            }

            var homeDirectory = @"C:\Complete backup\Windows backup\InputVideos";
            var blobRelativePath = videoMessage.BlobName.Replace("/", Path.DirectorySeparatorChar.ToString());
            var inputVideoPath = Path.Combine(homeDirectory, blobRelativePath);
            var inputVideoDirectory = Path.GetDirectoryName(inputVideoPath);
            if (!string.IsNullOrEmpty(inputVideoDirectory))
            {
                try
                {
                    Directory.CreateDirectory(inputVideoDirectory);
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Failed to create directory: {inputVideoDirectory}, Exception: {ex}");
                    throw;
                }
            }
            else
            {
                _logger.LogWarning("Input video directory path is null or empty.");
            }
                        
            using (var httpClient = new HttpClient())
            {
                using (var stream = await httpClient.GetStreamAsync(sasUri))
                {
                    using (var fileStream = File.Create(inputVideoPath))
                    {
                        await stream.CopyToAsync(fileStream);
                    }
                }
            }

            var resolutions = new Dictionary<string, string>
            {
                { "360p", "640x360" },
                { "480p", "854x480" },
                { "720p", "1280x720" }
            };

            foreach(var resolution in resolutions)
            {
                var outputDirectory = Path.Combine(inputVideoDirectory, $"{Path.GetFileNameWithoutExtension(videoMessage.BlobName)}-{resolution.Key}");
                Directory.CreateDirectory(outputDirectory);
                var outputPath = Path.Combine(outputDirectory, "playlist.m3u8");

                var ffmpegArguments = $"-i \"{inputVideoPath}\" -vf scale={resolution.Value} -profile:v baseline -level 3.0 -start_number 0 -hls_time 10 -hls_list_size 0 -f hls \"{outputPath}\"";

                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "ffmpeg",
                        Arguments = ffmpegArguments,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                process.Start();
                string output = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                if(process.ExitCode != 0)
                {
                    _logger.LogError($"FFmpeg failed for {resolution.Key} and resolution: {output}");
                }
                else
                {
                    _logger.LogInformation($"Successfully transcoded {resolution.Key} resolution to {outputPath}");
                }
            }
            _logger.LogInformation("Resolutions completed");
        }
    }
}
