using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DotThrow.Tests.Support;
using DotThrow.Tests.Validation.Users;
using Xunit;

namespace DotThrow.Tests.Validation;

public sealed class ValidatorTests
{
    [Fact]
    public void Valid_object_has_no_failures()
    {
        var report = new UserValidator().Validate(User.Valid());

        Assert.True(report.IsValid);
    }

    [Fact]
    public void For_rule_reports_the_first_failure_named_after_the_property()
    {
        var user = User.Valid();
        user.Name = "";
        user.Email = "ada.example.com";
        user.Age = -1;

        var report = new UserValidator().Validate(user);

        Assert.Collection(
            report.Failures,
            f => Assert.Equal(("Name", ErrorCodes.WhiteSpace, "'Name' must not be empty or white space."), (f.Name, f.ErrorCode, f.Message)),
            f => Assert.Equal(("Email", ErrorCodes.NotEmailAddress), (f.Name, f.ErrorCode)),
            f => Assert.Equal(("Age", ErrorCodes.Negative), (f.Name, f.ErrorCode)));
    }

    [Fact]
    public void For_rule_name_is_the_property_path()
    {
        var validator = new Validator<Shipment>()
            .For(x => x.Destination.City, city => city.IfNullOrEmpty())
            .For(static s => s.Destination.Zip, zip => zip.IfNullOrEmpty())
            .For((Shipment shipment) => shipment.Destination, destination => destination.IfNull().If(d => d.City is null));

        var report = validator.Validate(new Shipment());

        Assert.Equal(new[] { "Destination.City", "Destination.Zip", "Destination" }, report.Failures.Select(f => f.Name));
    }

    [Fact]
    public void For_rule_name_drops_null_forgiving_and_null_conditional_operators()
    {
        var validator = new Validator<User>()
            .For(x => x.Address!.City, city => city.IfNullOrEmpty())
            .For(x => x.Address?.Zip, zip => zip.IfNullOrEmpty());

        var report = validator.Validate(new User { Address = new Address() });

        Assert.Equal(new[] { "Address.City", "Address.Zip" }, report.Failures.Select(f => f.Name));
    }

    [Fact]
    public void For_rule_uses_an_explicit_name()
    {
        var validator = new Validator<User>().For(x => x.Email, email => email.IfNull(), configure: r => r.OverridePropertyName("email_address"));

        var report = validator.Validate(new User());

        Assert.Equal("email_address", Assert.Single(report.Failures).Name);
        Assert.Equal("'email_address' must not be null.", report.Failures[0].Message);
    }

    [Fact]
    public void For_rule_on_the_object_itself_has_an_empty_name_and_value_in_the_message()
    {
        var validator = new Validator<string>().For(x => x, value => value.IfShorterThan(5));

        var report = validator.Validate("Ada");

        var failure = Assert.Single(report.Failures);
        Assert.Equal("", failure.Name);
        Assert.Equal("'value' must be at least 5 characters long.", failure.Message);
    }

    [Fact]
    public void Child_validator_prefixes_names()
    {
        var user = User.Valid();
        user.Address = new Address { City = " ", Zip = "12345678901" };

        var report = new UserValidator().Validate(user);

        Assert.Equal(new[] { "Address.City", "Address.Zip" }, report.Failures.Select(f => f.Name));
        Assert.Equal("'City' must not be empty or white space.", report.Failures[0].Message);
    }

    [Fact]
    public void Child_validator_uses_an_explicit_prefix()
    {
        var validator = new Validator<User>().For(x => x.Address, new AddressValidator(), configure: r => r.OverridePropertyName("home"));

        var report = validator.Validate(new User { Address = new Address { Zip = "1" } });

        Assert.Equal("home.City", Assert.Single(report.Failures).Name);
    }

    [Fact]
    public void Null_child_is_skipped()
    {
        var user = User.Valid();
        user.Address = null;

        var report = new UserValidator().Validate(user);

        Assert.True(report.IsValid);
    }

    [Fact]
    public void Null_child_can_be_required_with_its_own_rule()
    {
        var validator = new Validator<User>()
            .For(x => x.Address, address => address.IfNull())
            .For(x => x.Address, new AddressValidator());

        var report = validator.Validate(new User());

        var failure = Assert.Single(report.Failures);
        Assert.Equal(("Address", ErrorCodes.Null), (failure.Name, failure.ErrorCode));
    }

    [Fact]
    public void Nested_child_validators_build_the_full_path()
    {
        var holder = new Validator<Holder>().For(x => x.User, new UserValidator());
        var value = new Holder { User = User.Valid() };
        value.User.Address!.City = "";

        var report = holder.Validate(value);

        Assert.Equal("User.Address.City", Assert.Single(report.Failures).Name);
    }

    [Fact]
    public void ForEach_rule_names_items_by_index()
    {
        var user = User.Valid();
        user.Tags = ["ok", "", null, new string('x', 21)];

        var report = new UserValidator().Validate(user);

        Assert.Collection(
            report.Failures,
            f => Assert.Equal(("Tags[1]", ErrorCodes.WhiteSpace), (f.Name, f.ErrorCode)),
            f => Assert.Equal(("Tags[2]", ErrorCodes.Null), (f.Name, f.ErrorCode)),
            f => Assert.Equal(("Tags[3]", ErrorCodes.LongerThan), (f.Name, f.ErrorCode)));
        // The failure name keeps the index; the message names the collection.
        Assert.Equal("'Tags' must be at most 20 characters long.", report.Failures[2].Message);
    }

    [Fact]
    public void ForEach_rule_skips_a_null_collection()
    {
        var user = User.Valid();
        user.Tags = null;

        var report = new UserValidator().Validate(user);

        Assert.True(report.IsValid);
    }

    [Fact]
    public void ForEach_child_validator_names_items_by_index_and_skips_null_items()
    {
        var validator = new Validator<Order>().ForEach(x => x.Lines, new LineValidator());
        var order = new Order
        {
            Lines =
            [
                new Line { Sku = "A-1", Quantity = 1 },
                null,
                new Line { Sku = "", Quantity = 0 },
            ],
        };

        var report = validator.Validate(order);

        Assert.Equal(new[] { "Lines[2].Sku", "Lines[2].Quantity" }, report.Failures.Select(f => f.Name));
    }

    [Fact]
    public void ForEach_child_validator_skips_a_null_collection()
    {
        var validator = new Validator<Order>().ForEach(x => x.Lines, new LineValidator());

        Assert.True(validator.Validate(new Order()).IsValid);
    }

    [Fact]
    public void ForEach_uses_an_explicit_name()
    {
        var validator = new Validator<Order>()
            .ForEach(x => x.Lines, line => line.IfNull(), configure: r => r.OverridePropertyName("items"))
            .ForEach(x => x.Lines, new LineValidator(), configure: r => r.OverridePropertyName("lines"));
        var order = new Order { Lines = [null, new Line { Sku = "A", Quantity = 0 }] };

        var report = validator.Validate(order);

        Assert.Equal(new[] { "items[0]", "lines[1].Quantity" }, report.Failures.Select(f => f.Name));
    }

    [Fact]
    public void ForEach_enumerates_a_lazy_sequence_once()
    {
        var sequence = new CountingSequence<int>(i => i - 1, count: 3);
        var validator = new Validator<CountingSequence<int>>().ForEach(x => x, item => item.IfNegative(), configure: r => r.OverridePropertyName("Numbers"));

        var report = validator.Validate(sequence);

        Assert.Equal("Numbers[0]", Assert.Single(report.Failures).Name);
        Assert.Equal(1, sequence.Enumerations);
    }

    [Fact]
    public void Must_rule_reports_a_whole_object_failure()
    {
        var validator = new Validator<Order>()
            .Must(x => x.Total >= 0, "The total must not be negative.")
            .Must(x => x.Lines is { Count: > 0 }, "The order needs a line.", configure: r => r.OverridePropertyName("Lines").WithErrorCode("NoLines"));

        var report = validator.Validate(new Order { Total = -1 });

        Assert.Collection(
            report.Failures,
            f => Assert.Equal(("", "The total must not be negative.", ErrorCodes.Must), (f.Name, f.Message, f.ErrorCode)),
            f => Assert.Equal(("Lines", "The order needs a line.", "NoLines"), (f.Name, f.Message, f.ErrorCode)));
        Assert.Equal(new[] { "The total must not be negative." }, report.ToDictionary()[""]);
    }

    [Fact]
    public void Custom_rule_can_add_many_failures()
    {
        var validator = new Validator<Order>().Custom((order, report) =>
        {
            var sum = order.Lines?.Sum(l => l!.Price * l.Quantity) ?? 0;
            report.Check(order.Total, "Total").IfNotEqualTo(sum);
            report.AddFailure("Lines", "Custom failure.", "Custom");
        });
        var order = new Order { Total = 10, Lines = [new Line { Price = 2, Quantity = 3 }] };

        var report = validator.Validate(order);

        Assert.Collection(
            report.Failures,
            f => Assert.Equal(("Total", ErrorCodes.NotEqualTo, "'Total' must be equal to 6."), (f.Name, f.ErrorCode, f.Message)),
            f => Assert.Equal(("Lines", "Custom"), (f.Name, f.ErrorCode)));
    }

    [Fact]
    public void Include_adds_the_rules_of_another_validator_without_a_prefix()
    {
        var basics = new Validator<User>().For(x => x.Name, name => name.IfNullOrEmpty());
        var validator = new Validator<User>()
            .Include(basics)
            .For(x => x.Email, email => email.IfNullOrEmpty());

        var report = validator.Validate(new User());

        Assert.Equal(new[] { "Name", "Email" }, report.Failures.Select(f => f.Name));
    }

    [Fact]
    public void Include_of_itself_throws()
    {
        var validator = new Validator<User>();

        var exception = Assert.Throws<ArgumentException>(() => validator.Include(validator));

        Assert.Equal("validator", exception.ParamName);
    }

    [Fact]
    public void Rules_without_a_group_run_by_default()
    {
        var validator = GroupedValidator();

        var report = validator.Validate(new User());

        Assert.Equal(new[] { "Name" }, report.Failures.Select(f => f.Name));
    }

    [Fact]
    public void Default_group_can_be_named()
    {
        var validator = GroupedValidator();

        var report = validator.Validate(new User(), ValidationGroups.Default);

        Assert.Equal(new[] { "Name" }, report.Failures.Select(f => f.Name));
    }

    [Fact]
    public void Named_group_runs_only_its_rules()
    {
        var validator = GroupedValidator();

        var report = validator.Validate(new User(), "admin");

        Assert.Equal(new[] { "Role" }, report.Failures.Select(f => f.Name));
    }

    [Fact]
    public void Several_groups_run_together_in_rule_order()
    {
        var validator = GroupedValidator();

        var report = validator.Validate(new User(), "billing", ValidationGroups.Default, "admin");

        Assert.Equal(new[] { "Name", "Role", "Email" }, report.Failures.Select(f => f.Name));
    }

    [Fact]
    public void All_group_runs_every_rule()
    {
        var validator = GroupedValidator();

        var report = validator.Validate(new User(), ValidationGroups.All);

        Assert.Equal(new[] { "Name", "Role", "Email" }, report.Failures.Select(f => f.Name));
    }

    [Fact]
    public void Group_names_ignore_case()
    {
        var validator = GroupedValidator();

        var report = validator.Validate(new User(), "ADMIN");

        Assert.Equal(new[] { "Role" }, report.Failures.Select(f => f.Name));
    }

    [Fact]
    public void Null_groups_mean_the_default_group()
    {
        var validator = GroupedValidator();

        var report = validator.Validate(new User(), null!);

        Assert.Equal(new[] { "Name" }, report.Failures.Select(f => f.Name));
    }

    [Fact]
    public void Groups_are_passed_to_child_validators()
    {
        var address = new Validator<Address>()
            .For(x => x.City, city => city.IfNullOrEmpty())
            .For(x => x.Zip, zip => zip.IfNullOrEmpty(), configure: r => r.InGroup("shipping"));
        var validator = new Validator<User>()
            .For(x => x.Address, address)
            .For(x => x.Address, address, configure: r => r.InGroup("shipping"));
        var user = new User { Address = new Address() };

        var defaults = validator.Validate(user);
        var shipping = validator.Validate(user, "shipping");
        var both = validator.Validate(user, ValidationGroups.Default, "shipping");

        Assert.Equal(new[] { "Address.City" }, defaults.Failures.Select(f => f.Name));
        Assert.Equal(new[] { "Address.Zip" }, shipping.Failures.Select(f => f.Name));
        Assert.Equal(new[] { "Address.City", "Address.Zip", "Address.City", "Address.Zip" }, both.Failures.Select(f => f.Name));
    }

    [Fact]
    public void When_filter_decides_if_a_rule_runs()
    {
        var calls = 0;
        var validator = new Validator<User>()
            .For(x => x.Email, email => email.IfNullOrEmpty(), configure: r => r.When(x => x.Role == "admin"))
            .Must(
                _ =>
                {
                    calls++;
                    return false;
                },
                "Never valid.",
                configure: r => r.When(x => x.Age > 100));

        var user = validator.Validate(new User { Role = "user" });
        var admin = validator.Validate(new User { Role = "admin" });

        Assert.True(user.IsValid);
        Assert.Equal("Email", Assert.Single(admin.Failures).Name);
        Assert.Equal(0, calls);
    }

    [Fact]
    public void When_filter_works_on_every_rule_kind()
    {
        Func<Order, bool> never = _ => false;
        var validator = new Validator<Order>()
            .For(x => x.Total, total => total.IfNegative(), configure: r => r.When(never))
            .For(x => x.Lines, new Validator<IReadOnlyList<Line?>>().Must(_ => false, "x"), configure: r => r.When(never))
            .ForEach(x => x.Lines, line => line.IfNull(), configure: r => r.When(never))
            .ForEach(x => x.Lines, new LineValidator(), configure: r => r.When(never))
            .Must(_ => false, "x", configure: r => r.When(never))
            .MustAsync((_, _) => Task.FromResult(false), "x", configure: r => r.When(never))
            .Custom((_, r) => r.AddFailure("x", "x"), configure: r => r.When(never))
            .CustomAsync((_, r, _) => Task.CompletedTask, configure: r => r.When(never))
            .Include(new Validator<Order>().Must(_ => false, "x"), configure: r => r.When(never));
        var order = new Order { Total = -1, Lines = [null, new Line()] };

        Assert.True(validator.Validate(order).IsValid);
    }

    [Fact]
    public void Group_works_on_every_rule_kind()
    {
        var validator = new Validator<Order>()
            .For(x => x.Total, total => total.IfNegative(), configure: r => r.InGroup("g"))
            .For(x => x.Lines, new Validator<IReadOnlyList<Line?>>().Must(_ => false, "child", configure: r => r.InGroup("g")), configure: r => r.InGroup("g"))
            .ForEach(x => x.Lines, line => line.IfNull(), configure: r => r.InGroup("g"))
            .ForEach(x => x.Lines, new LineValidator(), configure: r => r.InGroup("g"))
            .Must(_ => false, "must", configure: r => r.InGroup("g"))
            .Custom((_, r) => r.AddFailure("custom", "custom"), configure: r => r.InGroup("g"))
            .Include(new Validator<Order>().Must(_ => false, "included", configure: r => r.InGroup("g")), configure: r => r.InGroup("g"));
        var order = new Order { Total = -1, Lines = [null, new Line()] };

        Assert.True(validator.Validate(order).IsValid);
        Assert.Equal(new[] { "Total", "Lines", "Lines[0]", "", "custom", "" }, validator.Validate(order, "g").Failures.Select(f => f.Name));
    }

    [Fact]
    public void Validate_null_throws_ArgumentNullException()
    {
        var validator = new UserValidator();

        var exception = Assert.Throws<ArgumentNullException>(() => validator.Validate(null!));

        Assert.Equal("value", exception.ParamName);
    }

    [Fact]
    public void ValidateAndThrow_throws_with_every_failure()
    {
        var user = User.Valid();
        user.Name = "";
        user.Age = 200;

        var exception = Assert.Throws<ValidationFailedException>(() => new UserValidator().ValidateAndThrow(user));

        Assert.Equal(new[] { "Name", "Age" }, exception.Failures.Select(f => f.Name));
        Assert.Equal(
            "Validation failed with 2 errors:" + Environment.NewLine
            + " - Name: 'Name' must not be empty or white space." + Environment.NewLine
            + " - Age: 'Age' must be less than or equal to 150.",
            exception.Message);
    }

    [Fact]
    public void ValidateAndThrow_does_nothing_for_a_valid_object()
    {
        new UserValidator().ValidateAndThrow(User.Valid());
        new UserValidator().ValidateAndThrow(User.Valid(), ValidationGroups.All);
    }

    [Fact]
    public void ValidateAndThrow_uses_the_groups()
    {
        var validator = GroupedValidator();

        var exception = Assert.Throws<ValidationFailedException>(() => validator.ValidateAndThrow(new User(), "billing"));

        Assert.Equal("Email", Assert.Single(exception.Failures).Name);
    }

    [Fact]
    public void Validator_works_through_the_interface()
    {
        Assert.True(ValidateThroughInterface(new UserValidator(), User.Valid()).IsValid);
        Assert.False(ValidateThroughInterface(new UserValidator(), new User()).IsValid);
        Assert.Equal("Stub", Assert.Single(ValidateThroughInterface(new StubValidator<User>(), User.Valid()).Failures).Name);
    }

    [Fact]
    public void Hand_written_validators_work_as_children_and_get_the_groups()
    {
        var stub = new StubValidator<Address>();
        var included = new StubValidator<User>();
        var validator = new Validator<User>()
            .For(x => x.Address, stub)
            .Include(included, configure: r => r.InGroup("admin"));

        var defaults = validator.Validate(User.Valid());
        var admin = validator.Validate(User.Valid(), ValidationGroups.Default, "admin");

        Assert.Equal(new[] { "Address.Stub" }, defaults.Failures.Select(f => f.Name));
        Assert.Equal(new[] { "Address.Stub", "Stub" }, admin.Failures.Select(f => f.Name));
        // A default run passes no groups; a run with groups passes the requested groups on.
        Assert.Empty(stub.Groups[0]);
        Assert.Equal(new[] { ValidationGroups.Default, "admin" }, stub.Groups[1]);
        Assert.Equal(new[] { ValidationGroups.Default, "admin" }, included.Groups[0]);
    }

    [Fact]
    public void Validator_is_contravariant()
    {
        IValidator<object> anything = new Validator<object>().Must(x => x is not string, "Strings are not allowed.");
        var validator = new Validator<Holder>().For(x => x.Payload, anything);

        var report = validator.Validate(new Holder { Payload = "text" });

        Assert.Equal("Payload", Assert.Single(report.Failures).Name);
    }

    [Fact]
    public void Value_type_objects_can_be_validated()
    {
        var validator = new Validator<int>().For(x => x, value => value.IfNegative()).Must(x => x % 2 == 0, "Must be even.");

        Assert.True(validator.Validate(4).IsValid);
        Assert.Equal(new[] { "", "" }, validator.Validate(-13).Failures.Select(f => f.Name));
    }

    [Fact]
    public void Each_run_gets_a_new_report()
    {
        var validator = new UserValidator();

        var first = validator.Validate(new User());
        var second = validator.Validate(new User());

        Assert.NotSame(first, second);
        Assert.Equal(first.Failures.Count, second.Failures.Count);
    }

    [Fact]
    public void Builder_methods_return_the_same_validator()
    {
        var validator = new Validator<User>();

        Assert.Same(validator, validator.For(x => x.Name, n => n.IfNull()));
        Assert.Same(validator, validator.For(x => x.Address, new AddressValidator()));
        Assert.Same(validator, validator.ForEach(x => x.Tags, t => t.IfNull()));
        Assert.Same(validator, validator.ForEach(x => x.Tags, new Validator<string?>()));
        Assert.Same(validator, validator.Must(_ => true, "m"));
        Assert.Same(validator, validator.MustAsync((_, _) => Task.FromResult(true), "m"));
        Assert.Same(validator, validator.Custom((_, _) => { }));
        Assert.Same(validator, validator.CustomAsync((_, _, _) => Task.CompletedTask));
        Assert.Same(validator, validator.Include(new Validator<User>()));
    }

    [Fact]
    public void Builder_methods_reject_null_arguments()
    {
        var validator = new Validator<User>();

        Assert.Equal("selector", Assert.Throws<ArgumentNullException>(() => validator.For((Func<User, string?>)null!, n => n.IfNull())).ParamName);
        Assert.Equal("rules", Assert.Throws<ArgumentNullException>(() => validator.For(x => x.Name, (GuardChain<string?>)null!)).ParamName);
        Assert.Equal("selector", Assert.Throws<ArgumentNullException>(() => validator.For((Func<User, Address?>)null!, new AddressValidator())).ParamName);
        Assert.Equal("validator", Assert.Throws<ArgumentNullException>(() => validator.For(x => x.Address, (IValidator<Address>)null!)).ParamName);
        Assert.Equal("selector", Assert.Throws<ArgumentNullException>(() => validator.ForEach((Func<User, IEnumerable<string?>?>)null!, t => t.IfNull())).ParamName);
        Assert.Equal("rules", Assert.Throws<ArgumentNullException>(() => validator.ForEach(x => x.Tags, (GuardChain<string?>)null!)).ParamName);
        Assert.Equal("selector", Assert.Throws<ArgumentNullException>(() => validator.ForEach((Func<User, IEnumerable<string?>?>)null!, new Validator<string?>())).ParamName);
        Assert.Equal("validator", Assert.Throws<ArgumentNullException>(() => validator.ForEach(x => x.Tags, (IValidator<string?>)null!)).ParamName);
        Assert.Equal("predicate", Assert.Throws<ArgumentNullException>(() => validator.Must((Func<User, bool>)null!, "m")).ParamName);
        Assert.Equal("predicate", Assert.Throws<ArgumentNullException>(() => validator.MustAsync((Func<User, CancellationToken, Task<bool>>)null!, "m")).ParamName);
        Assert.Equal("rule", Assert.Throws<ArgumentNullException>(() => validator.Custom(null!)).ParamName);
        Assert.Equal("rule", Assert.Throws<ArgumentNullException>(() => validator.CustomAsync(null!)).ParamName);
        Assert.Equal("validator", Assert.Throws<ArgumentNullException>(() => validator.Include(null!)).ParamName);
    }

    [Fact]
    public void Exceptions_from_rules_propagate()
    {
        var validator = new Validator<User>().Must(_ => throw new InvalidOperationException("Rule failed."), "m");

        var exception = Assert.Throws<InvalidOperationException>(() => validator.Validate(new User()));

        Assert.Equal("Rule failed.", exception.Message);
    }

    [Fact]
    public void Validating_from_many_threads_while_adding_rules_is_safe()
    {
        const int RuleCount = 200;
        var validator = new Validator<User>();
        using var start = new ManualResetEventSlim(false);
        var errors = new List<string>();
        var adding = true;

        var readers = Enumerable.Range(0, 4).Select(_ => new Thread(() =>
        {
            start.Wait();
            while (Volatile.Read(ref adding))
            {
                var report = validator.Validate(new User());

                // Each run sees a snapshot: the first N rules, in the order they were added.
                for (var i = 0; i < report.Failures.Count; i++)
                {
                    if (report.Failures[i].ErrorCode != $"R{i}")
                    {
                        lock (errors)
                        {
                            errors.Add($"Position {i} had {report.Failures[i].ErrorCode}.");
                        }
                    }
                }
            }
        })).ToList();

        readers.ForEach(t => t.Start());
        start.Set();
        for (var i = 0; i < RuleCount; i++)
        {
            var code = $"R{i}";
            validator.Must(_ => false, "Always fails.", configure: r => r.WithErrorCode(code));
        }

        Volatile.Write(ref adding, false);
        readers.ForEach(t => t.Join());

        Assert.Empty(errors);
        Assert.Equal(RuleCount, validator.Validate(new User()).Failures.Count);
    }

    [Fact]
    public void Shared_validator_gives_the_same_result_on_every_thread()
    {
        var validator = new UserValidator();
        var user = User.Valid();
        user.Name = "";
        user.Tags = ["", "ok", null];
        var expected = validator.Validate(user).Failures.Select(f => f.ToString()).ToList();

        var results = Enumerable.Range(0, 64)
            .AsParallel()
            .Select(_ => validator.Validate(user).Failures.Select(f => f.ToString()).ToList())
            .ToList();

        Assert.All(results, result => Assert.Equal(expected, result));
    }

    private static ValidationReport ValidateThroughInterface(IValidator<User> validator, User user) => validator.Validate(user);

    private static Validator<User> GroupedValidator() => new Validator<User>()
        .For(x => x.Name, name => name.IfNullOrEmpty())
        .For(x => x.Role, role => role.IfNullOrEmpty(), configure: r => r.InGroup("admin"))
        .For(x => x.Email, email => email.IfNullOrEmpty(), configure: r => r.InGroup("billing"));

    public sealed class Holder
    {
        public User? User { get; set; }

        public object? Payload { get; set; }
    }
}
