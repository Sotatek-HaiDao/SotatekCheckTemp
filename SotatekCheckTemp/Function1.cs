// Default URL for triggering event grid function in the local environment.
// http://localhost:7071/runtime/webhooks/EventGrid?functionName={functionname}
using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.EventGrid;
using Microsoft.Extensions.Logging;
using Azure.Messaging.EventGrid;
using Azure.Core.Pipeline;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Net.Http;
using Azure.Identity;
using Azure.DigitalTwins.Core;
using Azure;
using System.Threading.Tasks;

namespace SotatekCheckTemp
{
    public static class Function1
    {
        private static readonly string adtInstanceUrl = Environment.GetEnvironmentVariable("ADT_SERVICE_URL");
        private static readonly HttpClient singletonHttpClientInstance = new HttpClient();

        [FunctionName("IOTHubtoTwins")]
        public async static Task Run([EventGridTrigger] EventGridEvent eventGridEvent, ILogger log)
        {
            if (adtInstanceUrl == null) log.LogError("Application setting \"ADT_SERVICE_URL\" not set");
            try
            {
                var cred = new ManagedIdentityCredential();

                var client = new DigitalTwinsClient(
                new Uri(adtInstanceUrl),
                cred,
                new DigitalTwinsClientOptions
                {
                    Transport = new HttpClientTransport(singletonHttpClientInstance)
                });

                log.LogInformation($"ADT service client connection created.");

                if (eventGridEvent != null && eventGridEvent.Data != null)
                {
                    log.LogInformation(eventGridEvent.Data.ToString());

                    // convert the message into a json object
                    JObject deviceMessage = (JObject)JsonConvert.DeserializeObject(eventGridEvent.Data.ToString());

                    // get our device id, temp and humidity from the object
                    string deviceId = (string)deviceMessage["systemProperties"]["iothub-connection-device-id"];
                    var temperature = deviceMessage["body"]["Temperature"];

                    //log the temperature
                    log.LogInformation($"Device:{deviceId} Temperature is:{temperature}");

                    // Update twin with temperature from raspberry pi>
                    var updateTwinData = new JsonPatchDocument();
                    log.LogInformation(updateTwinData.ToString());
                    updateTwinData.AppendReplace("/Temperature", temperature.Value<double>());
                    log.LogInformation(updateTwinData.ToString());
                    await client.UpdateDigitalTwinAsync(deviceId, updateTwinData);
                }
            }

            catch (Exception ex)
            {
                log.LogError($"Error in ingest function: {ex.Message}");
            }

        }
    }
}
