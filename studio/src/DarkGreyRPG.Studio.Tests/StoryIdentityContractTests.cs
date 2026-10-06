using System.Text.Json;
using DarkGreyRPG.Studio.Core.Identity;

namespace DarkGreyRPG.Studio.Tests;

[TestClass]
public sealed class StoryIdentityContractTests
{
    [TestMethod]
    public void SharedJavaAndStudioVectors()
    {
        var count = 0;
        foreach (var line in File.ReadLines(Path.Combine(AppContext.BaseDirectory, "IdentityVectors.jsonl")))
        {
            using var document = JsonDocument.Parse(line);
            var vector = document.RootElement;
            var input = vector.GetProperty("input").GetString()!;
            var valid = vector.GetProperty("valid").GetBoolean();
            count++;
            if (vector.GetProperty("type").GetString() == "uid")
            {
                Assert.AreEqual(valid, StoryUid.IsValid(input), $"UID vector {count}: {input}");
                if (valid) Assert.AreEqual(input, StoryUid.Parse(input).Value);
                else Assert.ThrowsExactly<ArgumentException>(() => StoryUid.Parse(input));
            }
            else if (valid)
            {
                var address = JsonSerializer.Deserialize<ResourceAddress>(input)!;
                Assert.AreEqual(vector.GetProperty("canonical").GetString(), JsonSerializer.Serialize(address), $"JSON vector {count}");
                Assert.AreEqual(vector.GetProperty("path").GetString(), address.RelativeDefinitionPath, $"Path vector {count}");
                Assert.AreEqual(address, JsonSerializer.Deserialize<ResourceAddress>(JsonSerializer.Serialize(address)));
                Assert.AreEqual(address, ResourceAddress.FromKey(address.ToKey()));
                Assert.IsTrue(ResourceAddress.IsKey(address.ToKey()));
            }
            else Assert.ThrowsExactly<JsonException>(() => JsonSerializer.Deserialize<ResourceAddress>(input), $"Address vector {count}: {input}");
        }
        Assert.IsGreaterThanOrEqualTo(50, count);
    }

    [TestMethod]
    public void OwnerAndKindAreBothPartOfIdentityAndPortablePaths()
    {
        var a = StoryUid.Parse("ST-2345-6789-ABCD-EFGH");
        var b = StoryUid.Parse("ST-JKLM-NPQR-STUV-WXYZ");
        ResourceAddress[] addresses = [new(a, ResourceKind.Actor, "r17"), new(b, ResourceKind.Actor, "r17"), new(a, ResourceKind.Item, "r17")];
        Assert.AreEqual(3, addresses.ToHashSet().Count);
        Assert.AreEqual(3, addresses.Select(x => x.RelativeDefinitionPath).ToHashSet(StringComparer.OrdinalIgnoreCase).Count);
        Assert.AreEqual(addresses[0], new ResourceAddress(StoryUid.Parse(a.Value), ResourceKind.Actor, "r17"));
        Assert.AreEqual($"referenced_resources/{a}/actor/r-r17.json", addresses[0].RelativeReferencePath);
    }

    [TestMethod]
    public void AllocationCreatesNewIdentitiesWithoutMutatingExistingOnes()
    {
        var identities = new HashSet<StoryUid>();
        var resources = new HashSet<ResourceAddress>();
        for (var index = 0; index < 2048; index++)
        {
            var uid = StoryUid.Create(identities);
            Assert.IsTrue(StoryUid.IsValid(uid.Value));
            Assert.IsTrue(identities.Add(uid));
            var address = ResourceAddress.Create(uid, ResourceKind.Actor, resources);
            Assert.IsTrue(resources.Add(address));
            Assert.AreEqual(33, address.LocalId.Length);
        }
        Assert.IsFalse(StoryUid.IsValid(null));
        Assert.IsFalse(ResourceAddress.IsValidLocalId(null));
        Assert.IsFalse(ResourceAddress.IsKey("Author:actor"));
        Assert.IsFalse(ResourceAddress.IsKey("r17"));
        Assert.IsFalse(ResourceAddress.IsKey(null));
        Assert.ThrowsExactly<JsonException>(() => JsonSerializer.Deserialize<StoryUid>("null"));
        Assert.ThrowsExactly<JsonException>(() => JsonSerializer.Deserialize<StoryUid>("\"Author:story\""));
        var first = identities.First();
        Assert.AreEqual(first, JsonSerializer.Deserialize<StoryUid>(JsonSerializer.Serialize(first)));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new ResourceAddress(first, (ResourceKind)99, "r17"));
    }
}
