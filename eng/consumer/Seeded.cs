using DotThrow;

// Code with known mistakes. check.sh expects the analyzers that ship inside the DotThrow
// package to report exactly these diagnostics, which proves they load in this SDK.
internal static class Seeded
{
    public static void Run(string name, SignUpValidator validator, SignUp request)
    {
        name.Throw();                // DT0001: a guard chain without checks
        validator.Validate(request); // DT0010: the validation report is not used
    }
}
