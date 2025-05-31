namespace Taxes.Tests;

[TestClass]
public class FileUtilsTest
{
    [TestMethod]
    public void CalculateMD5Digest()
    {
        var filePath = Path.GetTempFileName();
        try
        {
            File.WriteAllText(filePath, "Hello, world!");
            var md5Digest = FileUtils.CalculateMD5Digest(filePath);
            Assert.AreEqual("6cd3556deb0da54bca060b4c39479839", md5Digest);
        }
        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }
}
