using System;
using System.Collections.Generic;
using System.Linq;
using PageRank.Rank;
using Word2Vec.Net;
using PageRank.Graph;
using System.Windows;

namespace NLPWebScraper
{
    public static class Word2VecManager
    {
        public delegate void UpdateGUIMethod();
        private static UpdateGUIMethod callbackToGUI = null;

        private static Dictionary<string, float[]> word2VecCache = new Dictionary<string, float[]>();
        private static Distance word2VecDistance = null;

        public static UpdateGUIMethod CallbackToGUI { get => callbackToGUI; set => callbackToGUI = value; }

        #region Private methods
        private static void LoadUpWord2VecDatabase()
        {
            if (word2VecDistance == null)
                word2VecDistance = new Distance("googleWord2Vec.bin");
        }

        private static void ComputeSentenceVector(string[] sentence, ref float[] sumSentence)
        {
            LoadUpWord2VecDatabase();

            for (int wordIdx = 0; wordIdx < sentence.Length; wordIdx++)
            {
                string word = sentence[wordIdx];
                float[] currentWordVec;

                if (word2VecCache.ContainsKey(word))
                {
                    currentWordVec = word2VecCache[word];
                }
                else
                {
                    currentWordVec = word2VecDistance.GetVecForWord(word);
                    word2VecCache[word] = currentWordVec;
                }

                if (currentWordVec.Length == 0)
                    continue;

                for (int i = 0; i < 300; i++)
                    sumSentence[i] += currentWordVec[i];
            }
        }
        #endregion

        #region Public methods
        public static float[] GetVecForWord(string word)
        {
            LoadUpWord2VecDatabase();
            return word2VecDistance.GetVecForWord(word);
        }

        public static void RunWord2Vec(List<ScrapedWebsite> scrapedWebsites)
        {
            if (scrapedWebsites == null)
                return;

            LoadUpWord2VecDatabase();

            for (int iWebsiteIdx = 0; iWebsiteIdx < scrapedWebsites.Count; iWebsiteIdx++)
            {
                DynamicallyScrapedWebsite scrapedWebsite = scrapedWebsites[iWebsiteIdx] as DynamicallyScrapedWebsite;
                if (scrapedWebsite == null)
                    continue;

                foreach (var scrapingResult in scrapedWebsite.scrapingResults)
                {
                    List<List<float>> documentMatrix = new List<List<float>>();

                    for (int sentenceOneIdx = 0; sentenceOneIdx < scrapingResult.sentencesWords.Count; sentenceOneIdx++)
                    {
                        documentMatrix.Add(new List<float>());
                        for (int sentenceTwoIdx = 0; sentenceTwoIdx < scrapingResult.sentencesWords.Count; sentenceTwoIdx++)
                        {
                            if (sentenceOneIdx == sentenceTwoIdx)
                            {
                                documentMatrix[sentenceOneIdx].Add(new float());
                                documentMatrix[sentenceOneIdx][sentenceTwoIdx] = 0.0f;
                                continue;
                            }

                            string[] sentenceOne = scrapingResult.sentencesWords[sentenceOneIdx].ToArray();
                            string[] sentenceTwo = scrapingResult.sentencesWords[sentenceTwoIdx].ToArray();

                            float[] sumSentenceOne = new float[300];
                            Array.Clear(sumSentenceOne, 0, sumSentenceOne.Length);
                            float[] sumSentenceTwo = new float[300];
                            Array.Clear(sumSentenceTwo, 0, sumSentenceTwo.Length);

                            ComputeSentenceVector(sentenceOne, ref sumSentenceOne);
                            ComputeSentenceVector(sentenceTwo, ref sumSentenceTwo);

                            float ratioOne = 1.0f / sumSentenceOne.Sum();
                            sumSentenceOne = sumSentenceOne.Select(o => o * ratioOne).ToList().ToArray();
                            float ratioTwo = 1.0f / sumSentenceTwo.Sum();
                            sumSentenceTwo = sumSentenceTwo.Select(o => o * ratioTwo).ToList().ToArray();

                            documentMatrix[sentenceOneIdx].Add(new float());
                            documentMatrix[sentenceOneIdx][sentenceTwoIdx] = Utils.CalculateCosineSimilarity(sumSentenceOne, sumSentenceTwo);
                        }
                    }

                    // Normalize every row in the matrix.
                    for (int rowIdx = 0; rowIdx < scrapingResult.sentencesWords.Count; rowIdx++)
                    {
                        float ratio = 1.0f / documentMatrix[rowIdx].Sum();
                        documentMatrix[rowIdx] = documentMatrix[rowIdx].Select(o => o * ratio).ToList();
                    }

                    // Convert the similarity matrix into an undirected weighted graph.
                    UnDirectedGraph<int> documentGraph = new UnDirectedGraph<int>();
                    for (int sentenceOneIdx = 0; sentenceOneIdx < scrapingResult.sentencesWords.Count; sentenceOneIdx++)
                    {
                        for (int sentenceTwoIdx = 0; sentenceTwoIdx < scrapingResult.sentencesWords.Count; sentenceTwoIdx++)
                        {
                            if (sentenceOneIdx == sentenceTwoIdx)
                                continue;

                            documentGraph.AddEdge(sentenceOneIdx, sentenceTwoIdx, documentMatrix[sentenceOneIdx][sentenceTwoIdx]);
                        }
                    }

                    // Remove the diagonal which only contains zeroes.
                    for (int rowIdx = 0; rowIdx < scrapingResult.sentencesWords.Count; rowIdx++)
                        documentMatrix[rowIdx].RemoveAll(o => o == 0.0f);

                    // TextRank algorithm for sentences in documents.
                    var rankedDictionary = new PageRank<string>().Rank(documentGraph, 1.0f);

                    // In PageRank, we're looking for higher scores, so we sort in a descending manner.
                    var rankedSentencesList = rankedDictionary.ToList().OrderByDescending(sentence => sentence.Value).ToList();

                    List<int> topSentencesIndexes = new List<int>();
                    foreach (var sentencePair in rankedSentencesList)
                    {
                        if (scrapingResult.sentencesWords[sentencePair.Key].Select(word => word.ToLower()).Intersect(
                            scrapingResult.topWords, StringComparer.InvariantCultureIgnoreCase).Any())
                        {
                            topSentencesIndexes.Add(sentencePair.Key);
                        }
                    }

                    // Sort the indexes in ascending order so the summary makes more sense.
                    topSentencesIndexes = topSentencesIndexes.OrderBy(sentence => sentence).ToList();

                    int wordCount = 0;
                    // Fill out the summary for each result.
                    foreach (var topSentencesIdx in topSentencesIndexes)
                    {
                        scrapingResult.contentSummary += scrapingResult.sentencesWords[topSentencesIdx].Aggregate((i, j) => i + " " + j);
                        wordCount += scrapingResult.sentencesWords[topSentencesIdx].Count;

                        if (wordCount > 200)
                            break;
                    }
                }
            }
            // We are not on the main (GUI) thread so we need to update the GUI with an invoke.
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (callbackToGUI != null)
                    callbackToGUI();
            });
        }
        #endregion
    }
}
