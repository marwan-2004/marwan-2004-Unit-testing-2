using AutoFixture;
using Bogus;
using Simulation.Checkout.Domain;

namespace Simulation.Checkout.Tests.TestData;

public static class OrderTestDataBuilder
{
    public static Fixture CreateCustomFixture()
    {
        var fixture = new Fixture();

        fixture.Customize<OrderItem>(composer => composer
            .With(x => x.Quantity, () => Random.Shared.Next(1, 10))
            .With(x => x.UnitPrice, () => Math.Round((decimal)(Random.Shared.NextDouble() * 500), 2)));

        return fixture;
    }

    public static (string Name, string Email) CreateSeededCustomer(int seed = 12345)
    {
        Randomizer.Seed = new Random(seed);
        var faker = new Faker();
        return (faker.Name.FullName(), faker.Internet.Email());
    }

    public static Order CreateValidOrder(
        IReadOnlyList<OrderItem>? items = null,
        string customerName = "Test Customer",
        string customerEmail = "customer@example.com")
    {
        return new Order
        {
            CustomerName = customerName,
            CustomerEmail = customerEmail,
            Items = items ?? new List<OrderItem>
            {
                new()
                {
                    ProductId = Guid.NewGuid(),
                    ProductName = "Default Product",
                    UnitPrice = 100m,
                    Quantity = 1
                }
            }
        };
    }
}