using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Azure.Storage.Blobs;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OrderItemsReserverFunc
{
    public class Function1
    {
        [FunctionName("Function1")]
        public static async Task Run([ServiceBusTrigger("orders", Connection = "OrdersBusConnectionString")]string myQueueItem, ILogger log)
        {
            var httpClient = new HttpClient();

            log.LogInformation($"C# ServiceBus queue trigger function processed message: {myQueueItem}");

            var blobConnectionString = "DefaultEndpointsProtocol=https;AccountName=eshoponweb;AccountKey=9jivvGC3Hejnev+3a8DGVI0oKT3P7F+MhjAelhgsU0YazNTIgsNvdssEI38aOkPG47jdwK4jSKrt+AStWRbNSA==;EndpointSuffix=core.windows.net";
            var blobContainerName = "orders";

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            };

            var order = JsonSerializer.Deserialize<Order>(myQueueItem, jsonOptions);

            try
            {
                var blobServiceClient = new BlobServiceClient(blobConnectionString);
                var blobContainerClient = blobServiceClient.GetBlobContainerClient(blobContainerName);
                var blobClient = blobContainerClient.GetBlobClient($"{order.Id}.json");

                var bytes = Encoding.ASCII.GetBytes(myQueueItem);
                var stream = new MemoryStream(bytes);
                await blobClient.UploadAsync(stream);
            }
            catch (Exception ex)
            {
                log.LogError($"{ex}");

                var content = new StringContent(myQueueItem, Encoding.UTF8, "application/json");
                await httpClient.PostAsync("https://prod-73.westeurope.logic.azure.com:443/workflows/d2772569bd8942dfb25dd2c4120a0aac/triggers/manual/paths/invoke?api-version=2016-10-01&sp=%2Ftriggers%2Fmanual%2Frun&sv=1.0&sig=3rn_GMWNLASoAtRaLzJupZ8ql3WMvEBGPW7CtWrNj5o", content);
            }
        }

        public class Order
        {
            public string Id { get; set; }
            public string ShippingAddress { get; set; }
            public double FinalPrice { get; set; }
            public IEnumerable<Item> Items { get; set; }
        }

        public class Item
        {
            public int ItemId { get; set; }
            public int Quantity { get; set; }
        }
    }
}
