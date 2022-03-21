using DotThrow.Builder;
using DotThrow.ExceptionExtensions;

ICollection<int> vs = new List<int> { 1, 2 };
vs.CreateThrower()
    .ThrowIfEmpty()
    .ThrowIf(x => x.Count > 10, "List has more than one element")
    .ThrowIfCountIsGreaterThan(4)
    .ThrowIfCountIsLessThan(2)
    .ThrowIfCountIsEqualTo(3);

IDictionary<string, int> map = new Dictionary<string, int>
{
    { "x", 1 }
};
map.CreateThrower()
    .ThrowIfEmpty()
    .ThrowIf(x => x.ContainsKey("y"));

HashSet<string> set = new HashSet<string> { "x" };
set.CreateThrower()
    .ThrowIfEmpty()
    .ThrowIf(x => x.Contains("x"));

string s = "aAaaa";
string a = "324dd5a";

s.CreateThrower()
    .ThrowIfNullOrWhiteSpace()
    .ThrowIf(s => s.Length > 20, "Length is greater than 20!")
    .ThrowIf(s => s.Length == a.Length, "Both are equal!")
    .ThrowIfAllLower()
    .ThrowIfNotValidJson()
    .ThrowIfNotValidXml();

int b = 10;
b.CreateThrower()
    .ThrowIfEqual(10)
    .ThrowIfGreaterThanOrEqualZero()
    .ThrowIfLessThanOrEqualZero()
    .ThrowIfZero()
    .ThrowIf(x => (x & 5) == 0);


Validator<string>.AddRule(str => str.Length < 1, "String length is greater than 2", typeof(InvalidDataException), "String length is less than equal to 2");
Validator<string>.AddRule(str => str.Length < 1, "String length is greater than 3", typeof(InvalidDataException));
Validator<string>.AddRule(str => str.Length > 4, "String length is greater than 4", typeof(InvalidDataException), "String length is less than equal to 4");

var reports = Validator<string>.VerifyNotThrow(s);

foreach (var report in reports)
{
    Console.WriteLine(report);
}

List<string> lala = new() { "a", "vc", "c" };
Validator<List<string>>.AddRule(list => list.Count > 10, "List Overflow", typeof(OverflowException));
Validator<List<string>>.AddRule(list => list.Any(str => str.Equals("a")), "List Contains a", typeof(InvalidDataException));


List<int> lalai = new() { 1, 2, 3, 4 };
Validator<List<int>>.AddRule(list => list.Count > 3, "List contains more than 3 items", typeof(OverflowException), "List Contains Regular items", "test group 1");
Validator<List<int>>.AddRule(list => list.Count > 5, "List contains more than 5 items", typeof(OverflowException), "List Contains Regular items", "test group 1");

List<int> lalaii = new() { 1, 2, 3, 4, 5, 6 };

var reportss = Validator<List<int>>.VerifyNotThrow(lalaii, "test group 1");

foreach (var report in reportss)
{
    Console.WriteLine(report);
}
Console.ReadLine();