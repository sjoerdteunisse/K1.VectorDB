namespace K1.VectorDB.Engine.Tests;

[TestFixture]
public class VectorDbDocumentTests
{
    [Test]
    public void ConstructorValidation()
    {
        VectorDbDocument a = new();
        var vectorDbDocument = new VectorDbDocument();
        var vectorDbDocumentTest = new VectorDbDocument("test");
        var vectorDbDocumentIdTest = new VectorDbDocument("test_id", "test");

        Assert.Multiple(() =>
        {
            Assert.That(a.DocumentContent == string.Empty, Is.True);
            Assert.That(vectorDbDocument.DocumentContent == string.Empty, Is.True);
            Assert.That(vectorDbDocumentTest.DocumentContent == "test", Is.True);
            Assert.That(vectorDbDocumentIdTest.DocumentContent == "test", Is.True);

            Assert.That(a.Id, Is.Not.EqualTo(vectorDbDocument.Id));
            Assert.That(a.Id, Is.Not.EqualTo(vectorDbDocumentTest.Id));
            Assert.That(vectorDbDocument.Id, Is.Not.EqualTo(vectorDbDocumentTest.Id));
            Assert.That(vectorDbDocumentIdTest.Id, Is.EqualTo("test_id"));
        });

        Assert.Pass();
    }
}