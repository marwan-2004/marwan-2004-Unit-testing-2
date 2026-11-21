using Moq;
using Simulation.Checkout.Application;
using Simulation.Checkout.Application.Abstractions;
using Simulation.Checkout.Domain;
using VerifyXunit;
using Xunit;

namespace Simulation.Checkout.Tests;

public class ReceiptSnapshotTests
{
    [Fact] // 4.1
    public async Task CheckoutAsync_WhenOrderMatchesExampleC_ShouldMatchVerifiedReceiptSnapshot()
    {
        var inventoryMock = new Mock<IInventoryService>();
        inventoryMock
            .Setup(x => x.IsAvailableAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var sut = new CheckoutService(
            Mock.Of<IOrderRepository>(),
            inventoryMock.Object,
            Mock.Of<INotificationService>());

        var order = new Order
        {
            Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            CustomerName = "Omar Hassan",
            CustomerEmail = "omar.hassan@example.com",
            Items = new[]
            {
                new OrderItem
                {
                    ProductId = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                    ProductName = "Ergonomic Chair",
                    UnitPrice = 500m,
                    Quantity = 3
                }
            }
        };

        var receipt = await sut.CheckoutAsync(order);

        await Verifier.Verify(receipt)
            .ScrubMember(nameof(CheckoutReceipt.CreatedAt));
    }
}