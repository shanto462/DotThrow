using DotThrow;
using DotThrow.Samples;

// DotThrow samples. Each section runs and prints what happens.
//   dotnet run --project samples/DotThrow.Samples
// The app also runs as a Native AOT binary:
//   dotnet publish samples/DotThrow.Samples -c Release -r osx-arm64

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

var pageSize = 0;
Show.Error(() => pageSize.Throw().IfNegativeOrZero("Ask for at least one item per page."));

// The factory gets the failure (name, message, error code) and returns your exception.
var balance = -25m;
Show.Error(() => balance.Throw(failure => new DomainException(failure.ErrorCode, failure.Message)).IfNegative());

// ----------------------------------------------------------------------------------------
Show.Title("4. AllowNull for optional values");

string? middleName = null;
middleName.Throw().AllowNull().IfLongerThan(20);
Show.Line("A null middle name passes.");

middleName = "Augusta Ada Byron King";
Show.Error(() => middleName.Throw().AllowNull().IfLongerThan(20));

// Without AllowNull, every content check fails a null value with the "Null" code.
string? nickname = null;
Show.Error(() => nickname.Throw().IfLongerThan(20));

// ----------------------------------------------------------------------------------------
Show.Title("5. Collect mode: every failure, no exceptions");

var form = new SignUpRequest(Name: "", Email: "ada.example.com", MiddleName: "Augusta Ada Byron King");
var age = 12;

var report = new ValidationReport();
report.Check(form.Name).IfNullOrWhiteSpace();
report.Check(form.Email).IfNullOrWhiteSpace().IfNotEmailAddress();
report.Check(form.MiddleName).AllowNull().IfLongerThan(20);
report.Check(age).IfLessThan(13, "You must be 13 or older to sign up.");

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
    Lines: [new OrderLine("SKU-ENGINE", 1, 950m), new OrderLine("ENGINE", 200, 100m)])
{
    Tags = ["gift", " ", "difference-engine"],
    IsGift = true,
    GiftMessage = "",
};
Show.Line("A bad order:");
Show.Report(orderValidator.Validate(badOrder));

// ----------------------------------------------------------------------------------------
Show.Title("7. Groups: run a different set of rules for each situation");

var draftRules = new Validator<Order>()
    .For(x => x.Number, number => number.IfNullOrWhiteSpace())
    .For(x => x.ShippingAddress, address => address.IfNull(), group: "checkout")
    .For(x => x.Lines, lines => lines.IfNullOrEmpty(), group: "checkout");

var draft = new Order(Number: "", Email: "ada@example.com", ShippingAddress: null, Lines: []);

void CountFailures(string call, params string[] groups) =>
    Show.Line($"{call,-52} -> {draftRules.Validate(draft, groups).Failures.Count} failure(s)");

CountFailures("Validate(draft)");
CountFailures("Validate(draft, \"checkout\")", "checkout");
CountFailures("Validate(draft, ValidationGroups.Default, \"checkout\")", ValidationGroups.Default, "checkout");
CountFailures("Validate(draft, ValidationGroups.All)", ValidationGroups.All);

// ----------------------------------------------------------------------------------------
Show.Title("8. Async rules");

var users = new FakeUserStore("ada@example.com");
var signUpRules = new Validator<SignUpRequest>()
    .For(x => x.Name, name => name.IfNullOrWhiteSpace())
    .For(x => x.Email, email => email.IfNullOrWhiteSpace().IfNotEmailAddress())
    .MustAsync(
        async (x, cancellationToken) => !await users.EmailExistsAsync(x.Email, cancellationToken),
        "The email address is already registered.",
        name: "Email",
        errorCode: "EmailTaken");

Show.Line("A new user:");
Show.Report(await signUpRules.ValidateAsync(new SignUpRequest("Charles Babbage", "charles@example.com")));

Show.Line("An email that is already taken:");
Show.Report(await signUpRules.ValidateAsync(new SignUpRequest("Ada Lovelace", "ada@example.com")));

Show.Line("ValidateAndThrowAsync:");
await Show.ErrorAsync(() => signUpRules.ValidateAndThrowAsync(new SignUpRequest("", "ada@example.com")));

Show.Line("Validate (not async) on a validator with async rules:");
Show.Error(() => signUpRules.Validate(new SignUpRequest("Charles Babbage", "charles@example.com")));

// ----------------------------------------------------------------------------------------
Show.Title("9. Your own checks");

// IfNotSku and IfMoreDecimalPlacesThan are written in CustomChecks.cs.
var sku = "sku-12";
Show.Error(() => sku.Throw().IfNotSku());

var price = 9.999m;
Show.Error(() => price.Throw().IfMoreDecimalPlacesThan(2));

// The same checks work in collect mode, and in a validator (see OrderLineValidator).
var lineReport = new ValidationReport();
lineReport.Check(sku).IfNullOrWhiteSpace().IfNotSku();
lineReport.Check(price).IfNegative().IfMoreDecimalPlacesThan(2);
Show.Report(lineReport);

// ----------------------------------------------------------------------------------------
Show.Title("Done");
Show.Line("All samples ran.");
