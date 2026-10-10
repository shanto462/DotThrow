using DotThrow.Tests.Validation.Models;
using Xunit;

namespace DotThrow.Tests.Validation;

// ValidationOptions.Properties, with FluentValidation IncludeProperties semantics.
public sealed class PropertyFilterTests
{
    private static Validator<Order> OrderValidator() => new Validator<Order>()
        .For(x => x.Number, n => n.IfNullOrEmpty())
        .For(x => x.Total, t => t.IfNegativeOrZero())
        .ForEach(x => x.Lines, new LineValidator())
        .For(x => x.Customer, new Validator<Customer>()
            .For(x => x.Name, n => n.IfNullOrEmpty())
            .For(x => x.Address, new AddressValidator()))
        .Must(x => x.Total < 1000, "Too much.");

    private static Order Bad() => new()
    {
        Total = 5000,
        Lines = [new Line(), new Line { Sku = "a", Quantity = 0 }],
        Customer = new Customer { Address = new Address() },
    };

    private static string[] Run(params string[] properties) =>
        Names.Of(OrderValidator().Validate(Bad(), new ValidationOptions { Properties = properties }));

    [Fact]
    public void A_property_selects_its_own_rules_only_and_skips_whole_object_rules()
    {
        Assert.Equal(["Number"], Run("Number"));
    }

    [Fact]
    public void A_parent_selects_every_rule_of_the_child()
    {
        Assert.Equal(["Customer.Name", "Customer.Address.Street", "Customer.Address.City", "Customer.Address.PostalCode"], Run("Customer"));
    }

    [Fact]
    public void A_nested_path_selects_the_rules_on_the_way_and_the_matching_child_rule()
    {
        Assert.Equal(["Customer.Address.City"], Run("Customer.Address.City"));
    }

    [Fact]
    public void Collection_paths_with_an_index_or_a_wildcard()
    {
        Assert.Equal(["Lines[0].Sku", "Lines[0].Quantity", "Lines[1].Quantity"], Run("Lines"));
        Assert.Equal(["Lines[0].Quantity", "Lines[1].Quantity"], Run("Lines[].Quantity"));
        Assert.Equal(["Lines[1].Quantity"], Run("Lines[1].Quantity"));
    }

    [Fact]
    public void Included_rules_are_filtered_one_by_one()
    {
        var shared = new Validator<Order>().For(x => x.Number, n => n.IfNullOrEmpty()).For(x => x.Total, t => t.IfNegative());
        var validator = new Validator<Order>().Include(shared);

        var report = validator.Validate(new Order { Total = -1 }, new ValidationOptions { Properties = ["Total"] });

        Assert.Equal(["Total"], Names.Of(report));
    }
}
