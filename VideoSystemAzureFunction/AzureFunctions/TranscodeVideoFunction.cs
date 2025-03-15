using System.Text;
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
        public void Run([QueueTrigger("transcode-queue", Connection = "AzureWebJobsStorage")] QueueMessage message)
        {
            var messageBytes = Convert.FromBase64String(message.MessageText);
            var jsonMessage = Encoding.UTF8.GetString(messageBytes);
            var videoMessage = JsonConvert.DeserializeObject<VideoModel>(jsonMessage);
            _logger.LogInformation($"C# Queue trigger function processed: {message.MessageText}");
        }
    }
}
