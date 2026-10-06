using System.Text.Json;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;

namespace DarkGreyRPG.Studio.Tests;

[TestClass]
public sealed class DialogueCapacity0333Tests
{
    [TestMethod]
    public void StandardFontVectorsAndSingleSafetyMarginAgree()
    {
        using var data = JsonDocument.Parse(typeof(DialogueCapacityProfile).Assembly.GetManifestResourceStream("DialogueCapacityProfile.json")!);
        Assert.AreEqual((int)Math.Floor(DialogueCapacityProfile.Raw * .9), DialogueCapacityProfile.Safe);
        foreach (var vector in data.RootElement.GetProperty("vectors").EnumerateArray())
        {
            var text = vector.GetProperty("text").GetString()!;
            var expected = Math.Max(vector.GetProperty("normal_result").GetProperty("used").GetInt32(), vector.GetProperty("unicode_result").GetProperty("used").GetInt32());
            var result = DialogueCapacityProfile.Measure(text);
            Assert.AreEqual(expected, result.Used, text);
            Assert.IsFalse(result.Over, "Length alone must never block dialogue input.");
            Assert.IsTrue(result.Pages >= 1);
        }
    }

    [TestMethod]
    public void UnsupportedAndLegacyMultilineTextAreWarningsWithoutRewritingSource()
    {
        Assert.IsTrue(DialogueCapacityProfile.Measure("😀").Unsupported);
        Assert.IsTrue(DialogueCapacityProfile.Measure("a\r\nb").ManualNewline);
        Assert.IsFalse(DialogueCapacityProfile.Measure(new string('中', 1000)).Over);
        Assert.IsTrue(DialogueCapacityProfile.Measure(new string('中', 1000)).Pages > 1);
        Assert.IsTrue(DialogueCapacityProfile.Measure(new string('W', 60)).Used > DialogueCapacityProfile.Measure(new string('i', 60)).Used);
    }
}
