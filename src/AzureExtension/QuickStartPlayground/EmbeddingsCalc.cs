// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Globalization;

namespace AzureExtension.QuickStartPlayground;

/// <summary>
/// This class contains helper methods to perform vector database-like operations on the
/// sample projects. The extension uses this class to help find the reference sample that should
/// be used for the user's prompt.
/// </summary>
public static class EmbeddingsCalc
{
    private static double DotProduct(ReadOnlyMemory<float> a, ReadOnlyMemory<float> b)
    {
        return Enumerable.Range(0, a.Length).Sum(i => a.Span[i] * b.Span[i]);
    }

    private static double CalcCosineSimilarity(ReadOnlyMemory<float> a, ReadOnlyMemory<float> b)
    {
        try
        {
            var dotProduct = DotProduct(a, b);
            var magnitudeA = Math.Sqrt(DotProduct(a, a));
            var magnitudeB = Math.Sqrt(DotProduct(b, b));
            return dotProduct / (magnitudeA * magnitudeB);
        }
        catch (Exception)
        {
            return 0;
        }
    }

    public static List<(double CosineSimilarity, TrainingSample Sample)> SortByLanguageThenCosine(List<(double CosineSimilarity, TrainingSample Sample)> trainingSamples, string recommendedLanguage)
    {
        // Convert the recommendedLanguage to lowercase for case-insensitive comparison
        recommendedLanguage = recommendedLanguage.ToLower(CultureInfo.InvariantCulture);

        // Clone the list of docs to avoid modifying the original list
        var similarDocList = trainingSamples.ToList();

        // Sort doc list to rank highest any projects with the same language as recommended
        similarDocList.Sort((a, b) =>
        {
            // Sort by recommended language (case-insensitive) first
            var aHasRecommendedLang = a.Sample.Language != null ? a.Sample.Language.Equals(recommendedLanguage, StringComparison.OrdinalIgnoreCase) : false;
            var bHasRecommendedLang = b.Sample.Language != null ? b.Sample.Language.Equals(recommendedLanguage, StringComparison.OrdinalIgnoreCase) : false;

            if (aHasRecommendedLang && !bHasRecommendedLang)
            {
                return -1;
            }
            else if (!aHasRecommendedLang && bHasRecommendedLang)
            {
                return 1;
            }

            // If recommended languages are the same or both are different from the recommended language,
            // then sort by cosine similarity in descending order
            return b.CosineSimilarity.CompareTo(a.CosineSimilarity);
        });

        return similarDocList;
    }

    public static List<(double CosineSimilarity, TrainingSample Sample)> GetCosineSimilaritySamples(ReadOnlyMemory<float> questionEmbedding, IReadOnlyList<TrainingSample> trainingSamples)
    {
        // For each doc in docs, calculate the cosine similarity between the question embedding and the doc embedding
        // Sort the docs by the cosine similarity value
        var cosineSimilarityDocs = new List<(double CosineSimilarity, TrainingSample Sample)>();

        for (var i = 0; i < trainingSamples.Count; i++)
        {
            var sample = trainingSamples[i] ?? throw new ArgumentOutOfRangeException($"Document {i} is not expected to not be null");
            var cosineSimilarity = CalcCosineSimilarity(questionEmbedding, sample.Embedding);
            cosineSimilarityDocs.Add((cosineSimilarity, sample));
        }

        cosineSimilarityDocs.Sort((a, b) => b.CosineSimilarity.CompareTo(a.CosineSimilarity));

        return cosineSimilarityDocs;
    }
}
