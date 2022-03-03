
using DotThrow.Builder;
using DotThrow.ExceptionExtensions;

string s = "aAaaa";
string a = "324dd5a";

s.CreateThrower()
    .ThrowIfNullOrWhiteSpace()
    .ThrowIf(s => s.Length > 20, "Length is greater than 20!")
    .ThrowIf(s => s.Length == a.Length, "Both are equal!")
    .ThrowIfAllLower();

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
Validator<List<string>>.Verify(lala);


List<int> lalai = new() { 1, 2, 3, 4 };
Validator<List<int>>.Verify(lalai);