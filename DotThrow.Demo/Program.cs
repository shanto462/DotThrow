
using DotThrow.Builder;
using DotThrow.ExceptionExtensions;

string s = "aAaa";
string a = "324dd5a";

s.CreateThrower()
    .ThrowIfNullOrWhiteSpace()
    .ThrowIf(s => s.Length > 20, "Length is greater than 20!")
    .ThrowIf(s => s.Length == a.Length, "Both are equal!")
    .ThrowIfAllLower();

Validation<string>.AddRule(str => str.Length < 1, "String length is greater than 2", typeof(InvalidDataException));
Validation<string>.AddRule(str => str.Length < 1, "String length is greater than 3", typeof(InvalidDataException));
Validation<string>.AddRule(str => str.Length > 4, "String length is greater than 4", typeof(InvalidDataException));

Validation<string>.Verify(s);

List<string> lala = new List<string> { "a", "vc", "c" };
Validation<List<string>>.AddRule(list => list.Count > 10, "List Overflow", typeof(OverflowException));
Validation<List<string>>.Verify(lala);


List<int> lalai = new List<int> { 1, 2, 3, 4 };
Validation<List<int>>.Verify(lalai);