using DotThrow.Internal;
using Xunit;

namespace DotThrow.Tests.Internals;

public sealed class SelectorNamesTests
{
    [Theory]
    [InlineData("x => x.Name", "Name")]
    [InlineData("x=>x.Name", "Name")]
    [InlineData("  x  =>  x.Name  ", "Name")]
    [InlineData("x => x.Address.City", "Address.City")]
    [InlineData("(x) => x.Name", "Name")]
    [InlineData("static x => x.Name", "Name")]
    [InlineData("static (x) => x.Name", "Name")]
    [InlineData("(User x) => x.Name", "Name")]
    [InlineData("static (User x) => x.Name", "Name")]
    [InlineData("(Dictionary<string, int> x) => x.Count", "Count")]
    [InlineData("_ => _.Name", "Name")]
    [InlineData("@class => @class.Name", "Name")]
    [InlineData("user => user.Email", "Email")]
    [InlineData("x =>\n    x.Name", "Name")]
    [InlineData("x => x.Items[0]", "Items[0]")]
    [InlineData("x => x.Name.Trim()", "Name.Trim()")]
    [InlineData("x => x.Items.Where(i => i.Active)", "Items.Where(i => i.Active)")]
    [InlineData("x => xy.Name", "xy.Name")]
    [InlineData("x => GetName(x)", "GetName(x)")]
    [InlineData("x => (x.Name)", "(x.Name)")]
    [InlineData("x => x", "value")]
    [InlineData("static x => x", "value")]
    [InlineData("_ => 42", "42")]
    [InlineData("(x, y) => x", "x")]
    [InlineData("(a.b) => a.b.Name", "a.b.Name")]
    [InlineData("GetName", "GetName")]
    [InlineData("  selector  ", "selector")]
    [InlineData("", "value")]
    [InlineData("   ", "value")]
    [InlineData(null, "value")]
    public void FromExpression(string? expression, string expected) => Assert.Equal(expected, SelectorNames.FromExpression(expression));

    [Theory]
    [InlineData("x => x.Address!.City", "Address.City")]
    [InlineData("x => x.Address?.City", "Address.City")]
    [InlineData("x => x.Lines?[0].Sku", "Lines[0].Sku")]
    [InlineData("x => x.Address!\n    .City", "Address.City")]
    public void FromExpression_drops_nullable_operators(string expression, string expected) =>
        Assert.Equal(expected, SelectorNames.FromExpression(expression));
}
