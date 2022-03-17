# DotThrow

DotThrow is a .NET library for automatic exception catching and rule validation

## Installation

Download Source Code from [Releases](https://github.com/shanto462/DotThrow/releases).

For installing from nuget, run this command from Package Manager Console
```powershell
Install-Package DotThrow -Version 1.0.0
```
Or from .NET CLI
```powershell
dotnet add package DotThrow --version 1.0.0
```


## Usage

DotThrow offers three features. We can create automatic exception caching in method builder style or we can create validation rules for each type in general or grouped way.

## Example 1 (Method Builder Style):

```csharp
string s = "aAaaa";

s.CreateThrower()
    .ThrowIfNullOrWhiteSpace()
    .ThrowIf(s => s.Length > 20, "Length is greater than 20!")
    .ThrowIf(s => s.Length == a.Length, "Both are equal!")
    .ThrowIfAllLower();
```
## Example 2 (Validation Rules Non Grouped):

```csharp
Validator<string>.AddRule(str => str.Length < 1, "String length is greater than 2", typeof(InvalidDataException), "String length is less than equal to 2");
Validator<string>.AddRule(str => str.Length < 1, "String length is greater than 3", typeof(InvalidDataException));
Validator<string>.AddRule(str => str.Length > 4, "String length is greater than 4", typeof(InvalidDataException), "String length is less than equal to 4");

string a = "1234";
// This will throw exception whenever test variable break a rule
Validator<string>.Verify(a);

// This will not throw exception but generates report for each rule if test variable breaks it or not
var reports = Validator<string>.VerifyNotThrow(a);
foreach (var report in reports)
{
    Console.WriteLine(report);
}
```
## Example 3 (Validation Rules Grouped):

```csharp
// Here we are creating a group named "test group 1"
Validator<List<int>>.AddRule(list => list.Count > 3, "List contains more than 3 items", typeof(OverflowException), "List Contains Regular items", "test group 1");
Validator<List<int>>.AddRule(list => list.Count > 5, "List contains more than 5 items", typeof(OverflowException), "List Contains Regular items", "test group 1");

List<int> list = new() { 1, 2, 3, 4, 5, 6 };

// We are passing the group name parameter in the method
Validator<List<int>>.Verify(list, "test group 1");

var reports = Validator<List<int>>.VerifyNotThrow(list, "test group 1");
foreach (var report in reports)
{
    Console.WriteLine(report);
}
```

## Roadmap
Currently this project is in beta version and Method Builder is introduced only for **string** class. Though API can be **extended** by creating more extension method of **ExceptionThrower** struct.\
\
These are the features or improvement currently in development - 
1. Method Builder Extensions for more type (Collections, Primitives.....)
2. Configuration file format for Method Builder and Validation Rule in XML and Json. So that no extra code needed and configuration is portable between projects.
3. File or DB logging of complete stack trace.

## Contributing
Pull requests are welcome. For major changes, please open an issue first to discuss what you would like to change.

Please make sure to update tests as appropriate.
## License
[MIT](https://choosealicense.com/licenses/mit/)
## Contact
Email: <shalahuddinshanto@gmail.com>
