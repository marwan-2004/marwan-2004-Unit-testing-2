using AutoFixture;
using FluentAssertions;
using Moq;
using Simulation.Checkout.Application;
using Simulation.Checkout.Application.Abstractions;
using Simulation.Checkout.Domain;
using Simulation.Checkout.Domain.Exceptions;
using Simulation.Checkout.Tests.TestData;
using Xunit;

namespace Simulation.Checkout.Tests;

public class CheckoutServiceTests
{
    private readonly Mock<IOrderRepository> _orderRepositoryMock;
    private readonly Mock<IInventoryService> _inventoryServiceMock;
    private readonly Mock<INotificationService> _notificationServiceMock;
    private readonly CheckoutService _sut;

    public CheckoutServiceTests()
    {
        _orderRepositoryMock = new Mock<IOrderRepository>();
        _inventoryServiceMock = new Mock<IInventoryService>();
        _notificationServiceMock = new Mock<INotificationService>();

        _inventoryServiceMock
            .Setup(x => x.IsAvailableAsync(
                It.IsAny<Guid>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        _sut = new CheckoutService(
            _orderRepositoryMock.Object,
            _inventoryServiceMock.Object,
            _notificationServiceMock.Object);
    }

    // --- TASK 1: Business Rules ---

    [Fact] // 1.1
    public async Task CheckoutAsync_WhenOrderHasNoItems_ShouldThrowEmptyOrderException()
    {
        var order = OrderTestDataBuilder.CreateValidOrder(items: Array.Empty<OrderItem>());

        Func<Task> act = () => _sut.CheckoutAsync(order);

        await act.Should().ThrowAsync<EmptyOrderException>();
    }

    [Theory] // 1.2
    [InlineData(0)]
    [InlineData(-1)]
    public async Task CheckoutAsync_WhenQuantityIsZeroOrNegative_ShouldThrowInvalidQuantityExceptionContainingProductName(int invalidQuantity)
    {
        var order = OrderTestDataBuilder.CreateValidOrder(items: new[]
        {
            new OrderItem
            {
                ProductId = Guid.NewGuid(),
                ProductName = "Mechanical Keyboard",
                UnitPrice = 250m,
                Quantity = invalidQuantity
            }
        });

        Func<Task> act = () => _sut.CheckoutAsync(order);

        await act.Should().ThrowAsync<InvalidQuantityException>()
            .WithMessage("*Mechanical Keyboard*");
    }

    [Fact] // 1.3
    public async Task CheckoutAsync_WhenUnitPriceIsNegative_ShouldThrowInvalidPriceException()
    {
        var order = OrderTestDataBuilder.CreateValidOrder(items: new[]
        {
            new OrderItem
            {
                ProductId = Guid.NewGuid(),
                ProductName = "Wireless Mouse",
                UnitPrice = -0.01m,
                Quantity = 1
            }
        });

        Func<Task> act = () => _sut.CheckoutAsync(order);

        await act.Should().ThrowAsync<InvalidPriceException>();
    }

    [Fact] // 1.4
    public async Task CheckoutAsync_WhenUnitPriceIsZero_ShouldSucceed()
    {
        var order = OrderTestDataBuilder.CreateValidOrder(items: new[]
        {
            new OrderItem
            {
                ProductId = Guid.NewGuid(),
                ProductName = "Promotional Sticker",
                UnitPrice = 0m,
                Quantity = 1
            }
        });

        var receipt = await _sut.CheckoutAsync(order);

        receipt.Should().NotBeNull();
        receipt.Subtotal.Should().Be(0m);
        receipt.Total.Should().Be(0m);
    }

    [Theory] // 1.5
    [InlineData(999.99, 0)]
    [InlineData(1000.00, 100)]
    [InlineData(1500.00, 100)]
    public async Task CheckoutAsync_ForSubtotalBoundaries_ShouldApplyExpectedDiscount(decimal subtotal, decimal expectedDiscount)
    {
        var order = OrderTestDataBuilder.CreateValidOrder(items: new[]
        {
            new OrderItem
            {
                ProductId = Guid.NewGuid(),
                ProductName = "Bundle",
                UnitPrice = subtotal,
                Quantity = 1
            }
        });

        var receipt = await _sut.CheckoutAsync(order);

        receipt.Subtotal.Should().Be(subtotal);
        receipt.Discount.Should().Be(expectedDiscount);
    }

    [Fact] // 1.6 - Example A
    public async Task CheckoutAsync_WhenOrderMatchesExampleA_ShouldCalculateCorrectSubtotalDiscountAndTotal()
    {
        var order = OrderTestDataBuilder.CreateValidOrder(items: new[]
        {
            new OrderItem
            {
                ProductId = Guid.NewGuid(),
                ProductName = "Item A1",
                UnitPrice = 150m,
                Quantity = 2
            },
            new OrderItem
            {
                ProductId = Guid.NewGuid(),
                ProductName = "Item A2",
                UnitPrice = 400m,
                Quantity = 1
            }
        });

        var receipt = await _sut.CheckoutAsync(order);

        receipt.Subtotal.Should().Be(700m);
        receipt.Discount.Should().Be(0m);
        receipt.Total.Should().Be(700m);
    }

    [Fact] // 1.6 - Example C
    public async Task CheckoutAsync_WhenOrderMatchesExampleC_ShouldCapDiscountAtMaxDiscount()
    {
        var order = OrderTestDataBuilder.CreateValidOrder(items: new[]
        {
            new OrderItem
            {
                ProductId = Guid.NewGuid(),
                ProductName = "Item C1",
                UnitPrice = 500m,
                Quantity = 3
            }
        });

        var receipt = await _sut.CheckoutAsync(order);

        receipt.Subtotal.Should().Be(1500m);
        receipt.Discount.Should().Be(100m);
        receipt.Total.Should().Be(1400m);
    }

    // --- TASK 2: Mocks ---

    [Fact] // 2.1
    public async Task CheckoutAsync_WhenOrderIsValidAndAllItemsInStock_ShouldSaveOrderAndSendConfirmationOnce()
    {
        var order = OrderTestDataBuilder.CreateValidOrder();

        var receipt = await _sut.CheckoutAsync(order);

        receipt.Should().NotBeNull();
        receipt.OrderId.Should().Be(order.Id);
        _orderRepositoryMock.Verify(
            x => x.SaveAsync(order, It.IsAny<CancellationToken>()),
            Times.Once);
        _notificationServiceMock.Verify(
            x => x.SendOrderConfirmationAsync(receipt, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact] // 2.2
    public async Task CheckoutAsync_WhenOneItemIsOutOfStock_ShouldThrowAndNeverSaveOrNotify()
    {
        var outOfStockProductId = Guid.NewGuid();
        _inventoryServiceMock
            .Setup(x => x.IsAvailableAsync(outOfStockProductId, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var order = OrderTestDataBuilder.CreateValidOrder(items: new[]
        {
            new OrderItem
            {
                ProductId = outOfStockProductId,
                ProductName = "Graphics Card",
                UnitPrice = 8000m,
                Quantity = 1
            }
        });

        Func<Task> act = () => _sut.CheckoutAsync(order);

        await act.Should().ThrowAsync<InsufficientInventoryException>();
        _orderRepositoryMock.Verify(
            x => x.SaveAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _notificationServiceMock.Verify(
            x => x.SendOrderConfirmationAsync(It.IsAny<CheckoutReceipt>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact] // 2.3
    public async Task CheckoutAsync_WhenOrderFailsValidation_ShouldNeverCheckInventorySaveOrNotify()
    {
        var order = OrderTestDataBuilder.CreateValidOrder(items: new[]
        {
            new OrderItem
            {
                ProductId = Guid.NewGuid(),
                ProductName = "USB Cable",
                UnitPrice = 50m,
                Quantity = 0
            }
        });

        Func<Task> act = () => _sut.CheckoutAsync(order);

        await act.Should().ThrowAsync<InvalidQuantityException>();
        _inventoryServiceMock.Verify(
            x => x.IsAvailableAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _orderRepositoryMock.Verify(
            x => x.SaveAsync(It.IsAny<Order>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _notificationServiceMock.Verify(
            x => x.SendOrderConfirmationAsync(It.IsAny<CheckoutReceipt>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // --- TASK 3: Test Data (AutoFixture & Bogus) ---

    [Fact] // 3.1
    public async Task CheckoutAsync_WhenOrderGeneratedWithAutoFixture_ShouldSucceed()
    {
        var fixture = OrderTestDataBuilder.CreateCustomFixture();
        var order = fixture.Create<Order>();

        var receipt = await _sut.CheckoutAsync(order);

        receipt.Should().NotBeNull();
        receipt.OrderId.Should().Be(order.Id);
        receipt.Total.Should().BeGreaterThanOrEqualTo(0m);
    }

    [Fact] // 3.2
    public async Task CheckoutAsync_WhenCustomerGeneratedWithSeededBogus_ShouldPopulateMatchingReceiptCustomerDetails()
    {
        var (customerName, customerEmail) = OrderTestDataBuilder.CreateSeededCustomer(seed: 2026);
        var order = OrderTestDataBuilder.CreateValidOrder(
            customerName: customerName,
            customerEmail: customerEmail);

        var receipt = await _sut.CheckoutAsync(order);

        receipt.CustomerName.Should().Be(customerName);
        receipt.CustomerEmail.Should().Be(customerEmail);
    }
}