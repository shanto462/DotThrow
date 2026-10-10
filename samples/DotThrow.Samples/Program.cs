using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using DotThrow;
using DotThrow.Samples;
using DotThrow.Testing;

// DotThrow samples. Each section runs and prints what happens.
//   dotnet run --project samples/DotThrow.Samples
// The app also runs as a Native AOT binary:
//   dotnet publish samples/DotThrow.Samples -c Release -r osx-arm64

// Same output on every machine: messages and printed values use the invariant culture.
CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;

// ----------------------------------------------------------------------------------------
Show.Title("1. Guard clauses in a constructor");

var ada = new Customer("Ada Lovelace", "ada@example.com", 36);
Show.Line($"Created {ada.Name} <{ada.Email}>, age {ada.Age}.");

// The first failed check throws. The exception type says what kind of problem it is,
// and ParamName is filled in for you.
Show.Error(() => new Customer("   ", "ada@example.com", 36));
Show.Error(() => new Customer("Ada Lovelace", "ada.example.com", 36));
Show.Error(() => new Customer("Ada Lovelace", "ada@example.com", 200));

// ----------------------------------------------------------------------------------------
Show.Title("2. Names come from your code");

var request = new SignUpRequest(Name: "Ada Lovelace", Email: "");
Show.Error(() => request.Email.Throw().IfNullOrWhiteSpace());

int? quantity = null;
Show.Error(() => quantity.Throw().IfNull());

// IfNull() on a nullable value type also unwraps it.
int? pageNumber = 3;
int page = pageNumber.Throw().IfNull().IfNegativeOrZero();
Show.Line($"pageNumber unwrapped to int: {page}");

// ----------------------------------------------------------------------------------------
Show.Title("3. Custom messages and custom exceptions");

// A custom message can use the same placeholders as the default one.
var pageSize = 500;
Show.Error(() => pageSize.Throw().IfGreaterThan(100, "Ask for at most {ComparisonValue} items per page."));

// The factory gets the failure (name, message, error code) and returns your exception.
var balance = -25m;
Show.Error(() => balance.Throw(failure => new DomainException(failure.ErrorCode, failure.Message)).IfNegative());

// ----------------------------------------------------------------------------------------
Show.Title("4. WhenPresent for optional values");

string? middleName = null;
middleName.Throw().WhenPresent().IfLongerThan(20);
Show.Line("A null middle name passes.");

middleName = "Augusta Ada Byron King";
Show.Error(() => middleName.Throw().WhenPresent().IfLongerThan(20));

// Without WhenPresent, every content check fails a null value with the "Null" code.
string? nickname = null;
Show.Error(() => nickname.Throw().IfLongerThan(20));

// An optional number: WhenPresent() skips null and unwraps int? to int for the range check.
int? age = 200;
Show.Error(() => { age.Throw().WhenPresent().IfOutOfRange(0, 150); });

// ----------------------------------------------------------------------------------------
Show.Title("5. Collect mode: every failure, no exceptions");

var form = new SignUpRequest(Name: "", Email: "ada.example.com", MiddleName: "Augusta Ada Byron King", Age: 12);

// Check(form, f => f.Name) names the failure "Name", as a validator does.
var report = new ValidationReport();
report.Check(form, f => f.Name).IfNullOrWhiteSpace();
report.Check(form, f => f.Email).IfNullOrWhiteSpace().IfNotEmailAddress();
report.Check(form, f => f.MiddleName).WhenPresent().IfLongerThan(20);
report.Check(form, f => f.Age).WhenPresent().IfLessThan(13, "You must be 13 or older to sign up.");

// Each chain reports at most one failure: the first check that failed.
Show.Report(report);

Show.Line("ToDictionary(), the shape that Results.ValidationProblem(...) takes:");
Show.Dictionary(report);

// When you want an exception after all, ThrowIfInvalid throws one with every failure.
Show.Error(report.ThrowIfInvalid);

// ----------------------------------------------------------------------------------------
Show.Title("6. Validator<T> with nested objects and collections");

var orderValidator = OrderRules.Create();

var goodOrder = new Order(
    Number: "ORD-1001",
    Email: "ada@example.com",
    ShippingAddress: new Address("12 Analytical Way", "London", "12345"),
    Lines: [new OrderLine("SKU-ENGINE", 1, 950m)])
{
    Tags = ["gift"],
    IsGift = true,
    GiftMessage = "Happy birthday!",
};
Show.Line("A good order:");
Show.Report(orderValidator.Validate(goodOrder));

var badOrder = new Order(
    Number: "1002",
    Email: "ada.example.com",
    ShippingAddress: new Address("12 Analytical Way", "", "ABC"),
    Lines: [new OrderLine("SKU-ENGINE", 1, 950m), new OrderLine("ENGINE", 200, 100.005m)])
{
    Tags = ["gift", " ", "difference-engine"],
    IsGift = true,
    GiftMessage = null,
};
Show.Line("A bad order:");
Show.Report(orderValidator.Validate(badOrder));

// ----------------------------------------------------------------------------------------
Show.Title("7. Rule options: messages, names, severity, conditions");

var reviewRules = new Validator<Order>()
    .For(x => x.Email, email => email.IfNotEmailAddress(), rule => rule
        .WithName("E-mail address")
        .WithMessage("Please check {PropertyName}."))
    .ForEach(x => x.Tags, tag => tag.IfLongerThan(10), rule => rule
        .WithSeverity(Severity.Warning)
        .WithErrorCode("LongTag"))
    .When(x => x.IsGift, gift => gift
        .For(x => x.GiftMessage, message => message.IfNullOrWhiteSpace()));

Show.Report(reviewRules.Validate(badOrder));

// Validate only some properties, for example the field a user just edited.
Show.Line("Only the Email property:");
Show.Report(reviewRules.Validate(badOrder, options => options.IncludeProperties(x => x.Email)));

// ----------------------------------------------------------------------------------------
Show.Title("8. Groups: run a different set of rules for each situation");

var draftRules = new Validator<Order>()
    .For(x => x.Number, number => number.IfNullOrWhiteSpace())
    .Group("checkout", checkout => checkout
        .For(x => x.ShippingAddress, address => address.IfNull())
        .For(x => x.Lines, lines => lines.IfNullOrEmpty()));

var draft = new Order(Number: "", Email: "ada@example.com", ShippingAddress: null, Lines: []);

void CountFailures(string call, params string[] groups) =>
    Show.Line($"{call,-52} -> {draftRules.Validate(draft, groups).Failures.Count} failure(s)");

CountFailures("Validate(draft)");
CountFailures("Validate(draft, \"checkout\")", "checkout");
CountFailures("Validate(draft, ValidationGroups.Default, \"checkout\")", ValidationGroups.Default, "checkout");
CountFailures("Validate(draft, ValidationGroups.All)", ValidationGroups.All);

// ----------------------------------------------------------------------------------------
Show.Title("9. Async rules");

var users = new FakeUserStore("ada@example.com");
var signUpRules = new Validator<SignUpRequest>()
    .For(x => x.Name, name => name.IfNullOrWhiteSpace())
    .For(x => x.Email, email => email.IfNullOrWhiteSpace().IfNotEmailAddress(), rule => rule
        .DependentRules(lookup => lookup.MustAsync(
            x => x.Email,
            async (email, cancellationToken) => !await users.EmailExistsAsync(email, cancellationToken),
            "The email address is already registered.",
            taken => taken.WithErrorCode("EmailTaken"))));

Show.Line("A new user:");
Show.Report(await signUpRules.ValidateAsync(new SignUpRequest("Charles Babbage", "charles@example.com")));

Show.Line("An email that is already taken:");
Show.Report(await signUpRules.ValidateAsync(new SignUpRequest("Ada Lovelace", "ada@example.com")));

Show.Line("ValidateAndThrowAsync:");
await Show.ErrorAsync(() => signUpRules.ValidateAndThrowAsync(new SignUpRequest("", "ada@example.com")));

Show.Line("Validate (not async) on a validator with async rules:");
Show.Error(() => signUpRules.Validate(new SignUpRequest("Charles Babbage", "charles@example.com")));

// ----------------------------------------------------------------------------------------
Show.Title("10. Your own checks");

// IfNotSku and IfOutsideBusinessHours are written in CustomChecks.cs.
var sku = "sku-12";
Show.Error(() => sku.Throw().IfNotSku());

var pickupTime = new DateTime(2026, 10, 12, 18, 30, 0, DateTimeKind.Local);
Show.Error(() => pickupTime.Throw().IfOutsideBusinessHours());

// The same checks work in collect mode, and in a validator (see OrderLineValidator).
var lineReport = new ValidationReport();
lineReport.Check(sku).IfNullOrWhiteSpace().IfNotSku();
lineReport.Check(pickupTime).IfOutsideBusinessHours();
Show.Report(lineReport);

// ----------------------------------------------------------------------------------------
Show.Title("11. More built-in checks");

var slug = "Hello World";
var boxSize = 7;
var price = 19.999m;
var website = new Uri("http://contoso.example");
var server = IPAddress.Parse("10.0.0.5");
var settings = new Dictionary<string, string> { ["theme"] = "dark" };
var tagIds = new HashSet<int> { 1, 2, 3 };
var deliveryDate = new DateOnly(2026, 10, 10);
var permissions = FilePermissions.Read;
var cardNumber = "4111 1111 1111 1112";
using var upload = new MemoryStream();

var more = new ValidationReport();
more.Check(slug).IfNotSlug();
more.Check(boxSize).IfNotMultipleOf(5);
more.Check(price).IfPrecisionScaleExceeded(5, 2);
more.Check(website).IfNotHttps();
more.Check(server).IfPrivate();
more.Check(settings).IfNotContainsKey("language");
more.Check(tagIds).IfCountGreaterThan(2);
more.Check(deliveryDate).IfWeekend();
more.Check(permissions).IfNotHasFlag(FilePermissions.Write);
more.Check(cardNumber).IfNotCreditCardNumber();
more.Check(upload).IfEmpty();
Show.Report(more);

// ----------------------------------------------------------------------------------------
Show.Title("12. Message templates and languages");

var title = "A very long title for a short field";
MessageTemplates.Set(ErrorCodes.LongerThan, "{PropertyName} is too long: {TotalLength} of {MaxLength} characters.");
MessageTemplates.Set(ErrorCodes.LongerThan, "'{PropertyName}' darf höchstens {MaxLength} Zeichen haben.", new CultureInfo("de"));

Show.Line("Reworded for every culture:");
Show.Error(() => title.Throw().IfLongerThan(20));

MessageTemplates.Culture = new CultureInfo("de-AT");
Show.Line("German, found through the culture chain de-AT -> de:");
Show.Error(() => title.Throw().IfLongerThan(20));

// Back to the built-in English messages and the current UI culture.
MessageTemplates.Reset();

// ----------------------------------------------------------------------------------------
Show.Title("13. Testing helpers (DotThrow.Testing)");

// In a test project these lines are your assertions: they throw when the expectation fails.
var result = orderValidator.TestValidate(badOrder);
result.ShouldHaveValidationErrorFor(x => x.Number).WithErrorCode(ErrorCodes.NotStartsWith);
result.ShouldHaveValidationErrorFor("ShippingAddress.City");
result.ShouldNotHaveValidationErrorFor("ShippingAddress.Street");
Show.Line("Number, ShippingAddress.City and ShippingAddress.Street behave as expected.");

Show.Error(() => orderValidator.TestValidate(goodOrder).ShouldHaveValidationErrorFor(x => x.Email));

// ----------------------------------------------------------------------------------------
Show.Title("Done");
Show.Line("All samples ran.");
