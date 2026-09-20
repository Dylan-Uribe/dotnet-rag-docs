namespace Rag.Tests.TestSupport;

/// <summary>
/// Deterministic stand-in for a real embedding model: the first component encodes
/// the text it came from, so a test can tell which text produced which vector.
/// Injective for the (distinct) inputs the tests use.
/// </summary>
internal static class TestVectors
{
    public const int Dimensions = 1536;

    public static float[] For(string text, int dimensions = Dimensions)
    {
        var vector = new float[dimensions];
        vector[0] = text.Sum(character => (float)character);

        return vector;
    }
}
