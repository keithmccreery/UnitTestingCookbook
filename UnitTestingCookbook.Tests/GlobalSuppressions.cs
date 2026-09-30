// This file is used by Code Analysis to maintain SuppressMessage
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given
// a specific target and scoped to a namespace, type, member, etc.

using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Assertion", "NUnit2005:Consider using Assert.That(actual, Is.EqualTo(expected)) instead of Assert.AreEqual(expected, actual)", Justification = "<Pending>", Scope = "member", Target = "~M:UnitTestingCookbook.Test.AwesomeAssertionsTest.C_Assert_Vs_AwesomeAssertion_ErrorMessage")]
[assembly: SuppressMessage("AsyncUsage", "AsyncFixer01:Unnecessary async/await usage", Justification = "<Pending>", Scope = "member", Target = "~M:UnitTestingCookbook.Test.AwesomeAssertionsTest.M_ExceptionAsync~System.Threading.Tasks.Task")]
[assembly: SuppressMessage("AsyncUsage", "AsyncFixer01:Unnecessary async/await usage", Justification = "<Pending>", Scope = "member", Target = "~M:UnitTestingCookbook.Test.AwesomeAssertionsTest.N_ExceptionAndInnerExceptionAsync~System.Threading.Tasks.Task")]
[assembly: SuppressMessage("AsyncUsage", "AsyncFixer01:Unnecessary async/await usage", Justification = "<Pending>", Scope = "member", Target = "~M:UnitTestingCookbook.Test.AwesomeAssertionsTest.U_CompleteWithinAsyncWithResult~System.Threading.Tasks.Task")]
[assembly: SuppressMessage("AsyncUsage", "AsyncFixer01:Unnecessary async/await usage", Justification = "<Pending>", Scope = "member", Target = "~M:UnitTestingCookbook.Test.DockerTest.OneTimeSetup~System.Threading.Tasks.Task")]
[assembly: SuppressMessage("System.IO.Abstractions", "IO0006:Replace Path class with IFileSystem.Path for improved testability", Justification = "<Pending>", Scope = "member", Target = "~M:UnitTestingCookbook.Test.AwesomeAssertionsAddOnsTest.X_DependencyInjection")]
[assembly: SuppressMessage("System.IO.Abstractions", "IO0008:Replace StringWriter class with IFileSystem.StringWriter for improved testability", Justification = "<Pending>", Scope = "member", Target = "~M:UnitTestingCookbook.Test.GeneralTipsTest.A_CaptureConsole")]
[assembly: SuppressMessage("AsyncUsage", "AsyncFixer01:Unnecessary async/await usage", Justification = "<Pending>", Scope = "member", Target = "~M:UnitTestingCookbook.Test.WireMockNetPollyPoliciesTest.B_WireMockNet_Polly_CancellationToken~System.Threading.Tasks.Task")]
[assembly: SuppressMessage("Minor Code Smell", "S1481:Unused local variables should be removed", Justification = "<Pending>", Scope = "member", Target = "~M:UnitTestingCookbook.Test.AwesomeAssertionsAddOnsTest.X_DependencyInjection")]
