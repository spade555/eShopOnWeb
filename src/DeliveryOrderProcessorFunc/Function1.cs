using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Cosmos;
using System.Text.Json;
using System.Net;
using System.Collections.Generic;

namespace DeliveryOrderProcessorFunc
{
    public static class Function1
    {
        [FunctionName("Function1")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req, ILogger log)
        {
            log.LogInformation("OrderFunc processed a request.");

            var connectionString = "AccountEndpoint=https://szgeshoponweb.documents.azure.com:443/;AccountKey=xTAX4Fpr39Nnr2ae0kTHKOrLTxcePA0rFM67pjCU3FQHk5IYraXKCBj4CuH71txf8E5xGjhgByoXACDbwFbX1Q==;";

            var cosmosOptions = new CosmosClientOptions
            {
                SerializerOptions = new CosmosSerializationOptions { PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase },
            };

            var client = new CosmosClient(connectionString, cosmosOptions);
            var database = client.GetDatabase("orders");
            var container = database.GetContainer("orders");

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            };

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var order = JsonSerializer.Deserialize<Order>(requestBody, jsonOptions);

            ItemResponse<Order> result = null;
            try
            {
                result = await container.CreateItemAsync(order);
            }
            catch (Exception ex)
            {
                log.LogError($"{ex}");
            }

            string responseMessage = result.StatusCode is HttpStatusCode.Created
                ? $"{result.Resource.Id} order created."
                : $"Order with id of {result.Resource.Id} failed to create.";

            return new OkObjectResult(responseMessage);
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
