using System.Text;
using System.Net.Http.Headers;
using Azure.Storage.Queues.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using VideoSystemAzureFunction.Models;

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

            using(var fileClient = new HttpClient())
            {
                fileClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                var fileBlobUri = $"https://localhost:7082/api/file/getSASUrl?filePath={Uri.EscapeDataString(videoMessage.BlobName)}";
                var sasResponse = await fileClient.GetAsync(fileBlobUri);
                var sasUri = await sasResponse.Content.ReadAsStringAsync();
            }
        }
    }
}
