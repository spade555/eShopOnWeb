using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Ardalis.GuardClauses;
using Azure.Messaging.ServiceBus;
using Microsoft.eShopWeb.ApplicationCore.Entities;
using Microsoft.eShopWeb.ApplicationCore.Entities.BasketAggregate;
using Microsoft.eShopWeb.ApplicationCore.Entities.OrderAggregate;
using Microsoft.eShopWeb.ApplicationCore.Interfaces;
using Microsoft.eShopWeb.ApplicationCore.Specifications;

namespace Microsoft.eShopWeb.ApplicationCore.Services;

public class OrderService : IOrderService
{
    private readonly IRepository<Order> _orderRepository;
    private readonly IUriComposer _uriComposer;
    private readonly IRepository<Basket> _basketRepository;
    private readonly IRepository<CatalogItem> _itemRepository;
    private readonly HttpClient _httpClient;

    public OrderService(IRepository<Basket> basketRepository,
        IRepository<CatalogItem> itemRepository,
        IRepository<Order> orderRepository,
        IUriComposer uriComposer,
        HttpClient httpClient)
    {
        _orderRepository = orderRepository;
        _uriComposer = uriComposer;
        _basketRepository = basketRepository;
        _itemRepository = itemRepository;
        _httpClient = httpClient;
    }

    public async Task CreateOrderAsync(int basketId, Address shippingAddress)
    {
        var basketSpec = new BasketWithItemsSpecification(basketId);
        var basket = await _basketRepository.FirstOrDefaultAsync(basketSpec);

        Guard.Against.Null(basket, nameof(basket));
        Guard.Against.EmptyBasketOnCheckout(basket.Items);

        var catalogItemsSpecification = new CatalogItemsSpecification(basket.Items.Select(item => item.CatalogItemId).ToArray());
        var catalogItems = await _itemRepository.ListAsync(catalogItemsSpecification);

        var items = basket.Items.Select(basketItem =>
        {
            var catalogItem = catalogItems.First(c => c.Id == basketItem.CatalogItemId);
            var itemOrdered = new CatalogItemOrdered(catalogItem.Id, catalogItem.Name, _uriComposer.ComposePicUri(catalogItem.PictureUri));
            var orderItem = new OrderItem(itemOrdered, basketItem.UnitPrice, basketItem.Quantity);
            return orderItem;
        }).ToList();

        await using var client = new ServiceBusClient("Endpoint=sb://eshoponweborders.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=Fh/ahpk6p8SDC1SM6+R6PEHLI8qg2bn7L+ASbI0dSf8=");
        var sender = client.CreateSender("orders");

        var order = new Order(basket.BuyerId, shippingAddress, items);

        var itemsToOrder =
            new
            {
                id = Guid.NewGuid().ToString(),
                shippingAddress = $"{order.ShipToAddress.ZipCode} {order.ShipToAddress.Country} {order.ShipToAddress.State} {order.ShipToAddress.Street}",
                finalPrice = order.OrderItems.Sum(i => i.Units * i.UnitPrice),
                items = order.OrderItems.Select(i =>
                    new
                    {
                        id = i.ItemOrdered.CatalogItemId,
                        quantity = i.Units,
                    }),
            };

        var json = JsonExtensions.ToJson(itemsToOrder);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var busMessage = new ServiceBusMessage(json);
        await sender.SendMessageAsync(busMessage);

        var response = await _httpClient.PostAsync("https://deliveryorderprocessorfunc.azurewebsites.net/api/Function1?code=2hWjSkjLhH84uJA_UUXo2jpxF8Y9kGpkAndGuXzrWzT3AzFuhn3rVw==", content);

        await _orderRepository.AddAsync(order);
    }
}
