using System;
using System.Collections.Generic;
using System.Linq;

namespace NLPWebScraper
{
    class Utils
    {
        public static int ComputeLevenshteinDistance(string a, string b)
        {
            if (String.IsNullOrEmpty(a) && String.IsNullOrEmpty(b))
                return 0;

            if (String.IsNullOrEmpty(a))
                return b.Length;

            if (String.IsNullOrEmpty(b))
                return a.Length;

            int lengthA = a.Length;
            int lengthB = b.Length;
            var distances = new int[lengthA + 1, lengthB + 1];

            for (int i = 0; i <= lengthA; distances[i, 0] = i++) ;
            for (int j = 0; j <= lengthB; distances[0, j] = j++) ;

            for (int i = 1; i <= lengthA; i++)
            {
                for (int j = 1; j <= lengthB; j++)
                {
                    int cost = b[j - 1] == a[i - 1] ? 0 : 1;
                    distances[i, j] = Math.Min
                        (
                        Math.Min(distances[i - 1, j] + 1, distances[i, j - 1] + 1),
                        distances[i - 1, j - 1] + cost
                        );
                }
            }

            return distances[lengthA, lengthB];
        }

        public static List<int> AllIndexesOf(string content, string value)
        {
            if (String.IsNullOrEmpty(value))
                return new List<int>();

            List<int> indexes = new List<int>();
            for (int index = 0; ; index += value.Length)
            {
#pragma warning disable CA1307 // Specify StringComparison
                index = content.IndexOf(value, index);
#pragma warning restore CA1307 // Specify StringComparison
                if (index == -1)
                    return indexes;

                indexes.Add(index);
            }
        }

        public static List<Tuple<int, int>> MergeToTuples(List<int> listOne, List<int> listTwo)
        {
            for (int index = 0; index < listOne.Count - 1; index++)
            {
                for (int indexTwo = 1; indexTwo < listTwo.Count; indexTwo++)
                {
                    if (index == indexTwo)
                        continue;

                    if (listTwo[indexTwo] < listOne[index + 1] && listTwo[indexTwo] > listOne[index])
                    {
                        listTwo.RemoveAt(indexTwo - 1);
                        indexTwo--;
                    }
                }
            }

            if (listOne.Count != listTwo.Count)
                return new List<Tuple<int, int>>();

            List<Tuple<int, int>> tupleList = new List<Tuple<int, int>>();
            for (int index = 0; index < listOne.Count; index++)
                tupleList.Add(new Tuple<int, int>(listOne[index], listTwo[index]));

            return tupleList;
        }

        public static List<string> GetNamedEntities(string content, List<Tuple<int, int>> indexes)
        {
            List<string> namedEntities = new List<string>();

            foreach (var index in indexes)
                namedEntities.Add(content.Substring(index.Item1, index.Item2 - index.Item1));

            return namedEntities;
        }

        public static List<Dictionary<string, double>> Transform(List<List<List<string>>> stemmedDocuments, int vocabularyThreshold = 3)
        {
            Dictionary<string, double> vocabularyIDF = new Dictionary<string, double>();

            // Get the vocabulary and stem the documents at the same time.
            List<string> vocabulary = GetVocabulary(stemmedDocuments, vocabularyThreshold);

            // Remove all punctuation vocabulary entries.
            vocabulary = vocabulary.Where(word => word.All(letter => char.IsLetterOrDigit(letter))).ToList();

            // Calculate the IDF for each vocabulary term.
            foreach (var term in vocabulary)
            {
                double numberOfDocsContainingTerm = stemmedDocuments.Where(document => document.Where(sentence => sentence.Any(word => word.ToLower() == term)).Any()).Count();
                vocabularyIDF[term] = Math.Log((double)stemmedDocuments.Count / numberOfDocsContainingTerm != 0 ? ((double)1 + numberOfDocsContainingTerm) : 1);
            }

            // Transform each document into a vector of tfidf values.
            return TransformToTFIDFVectors(stemmedDocuments, vocabularyIDF);
        }

        private static List<Dictionary<string, double>> TransformToTFIDFVectors(List<List<List<string>>> stemmedDocs, Dictionary<string, double> vocabularyIDF)
        {
            // Transform each document into a vector of tfidf values.
            List<Dictionary<string, double>> returnList = new List<Dictionary<string, double>>();

            int documentIdx = 0;
            foreach (var doc in stemmedDocs)
            {
                returnList.Add(new Dictionary<string, double>());
                foreach (var vocab in vocabularyIDF)
                {
                    // Term frequency = count how many times the term appears in this document.
                    double tf = 0.0f;
                    doc.ForEach(sentence => tf += sentence.Where(word => word.ToLower() == vocab.Key).Count());
                    double tfidf = tf * vocab.Value;

                    returnList[documentIdx][vocab.Key] = tfidf;
                }
                documentIdx++;
            }

            return returnList;
        }

        private static List<string> GetVocabulary(List<List<List<string>>> stemmedDocs, int vocabularyThreshold)
        {
            List<string> vocabulary = new List<string>();
            Dictionary<string, int> wordCountList = new Dictionary<string, int>();

            foreach (var document in stemmedDocs)
            {
                foreach (var sentence in document)
                {
                    foreach (var word in sentence)
                    {
                        string lowerCaseWord = word.ToLower();

                        if (StopWords.GetStopWordsList().Contains(lowerCaseWord))
                            continue;

                        if (wordCountList.ContainsKey(lowerCaseWord))
                        {
                            wordCountList[lowerCaseWord]++;
                        }
                        else
                        {
                            wordCountList.Add(lowerCaseWord, 0);
                        }
                    }
                }
            }

            var vocabList = wordCountList.Where(w => w.Value >= vocabularyThreshold);
            foreach (var item in vocabList)
            {
                vocabulary.Add(item.Key);
            }

            return vocabulary;
        }
    }
}
