using VFXComposer.Broker;

namespace VFXComposer.Broker.Tests;

[TestClass]
[DoNotParallelize]
public sealed class UserModeBrokerProgramTests
{
    [TestMethod]
    public void DefaultProgramWritesOnlyW24FS001ToStandardErrorAndExits23()
    {
        var originalOut = Console.Out;
        var originalError = Console.Error;
        using var standardOut = new StringWriter();
        using var standardError = new StringWriter();
        try
        {
            Console.SetOut(standardOut);
            Console.SetError(standardError);

            var exitCode = Program.Main();

            Assert.AreEqual(23, exitCode);
            Assert.AreEqual(string.Empty, standardOut.ToString());
            Assert.AreEqual("W24FS001" + Environment.NewLine, standardError.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }
}
