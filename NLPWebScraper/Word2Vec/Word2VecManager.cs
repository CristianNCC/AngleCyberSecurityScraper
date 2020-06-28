// This is a personal academic project. Dear PVS-Studio, please check it.

// PVS-Studio Static Code Analyzer for C, C++, C#, and Java: http://www.viva64.com

using System;
using System.Collections.Generic;
using System.Linq;
using PageRank.Rank;
using Word2Vec.Net;
using PageRank.Graph;
using System.Windows;
using System.IO;

namespace NLPWebScraper
{
    public static class Word2VecManager
    {
        #region Constants
        private const string word2VecDatabasePath = "../Files/googleWord2Vec.bin";
        private const int databaseFeatureSize = 300;
        #endregion

        #region Members
        public delegate void UpdateGUIMethod();
        private static UpdateGUIMethod callbackToGUI = null;

        private static Dictionary<string, float[]> word2VecCache = new Dictionary<string, float[]>();
        private static Distance word2VecDistance = null;

        public static UpdateGUIMethod CallbackToGUI { get => callbackToGUI; set => callbackToGUI = value; }
        #endregion

        #region Private methods
        public static void LoadUpWord2VecDatabase(int initSize = 150000)
        {
            if (word2VecDistance == null && File.Exists(word2VecDatabasePath))
                word2VecDistance = new Distance(word2VecDatabasePath, initSize);
        }

        private static void ComputeSentenceVector(string[] sentence, ref float[] sumSentence)
        {
            LoadUpWord2VecDatabase();
            if (word2VecDistance == null)
                return;

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

                for (int i = 0; i < databaseFeatureSize; i++)
                    sumSentence[i] += currentWordVec[i];
            }
        }
        #endregion

        #region Public methods
        public static float[] GetVecForWord(string word, int initSize = 150000)
        {
            LoadUpWord2VecDatabase(initSize);
            if (word2VecDistance == null)
                return Array.Empty<float>();

            float[] vec = new float[300];
            if (word2VecCache.ContainsKey(word))
            {
                vec = word2VecCache[word];
            }
            else
            {
                vec = word2VecDistance.GetVecForWord(word);
                word2VecCache[word] = vec;
            }

            return vec;
        }

        public static float GetSentencesSimilarity(string[] sentenceOne, string[] sentenceTwo)
        {
            if (sentenceOne == null || sentenceTwo == null)
                return 0.0f;

            float[] sumSentenceOne = new float[databaseFeatureSize];
            Array.Clear(sumSentenceOne, 0, sumSentenceOne.Length);
            float[] sumSentenceTwo = new float[databaseFeatureSize];
            Array.Clear(sumSentenceTwo, 0, sumSentenceTwo.Length);

            ComputeSentenceVector(sentenceOne, ref sumSentenceOne);
            ComputeSentenceVector(sentenceTwo, ref sumSentenceTwo);

            float ratioOne = 1.0f / sumSentenceOne.Sum();
            sumSentenceOne = sumSentenceOne.Select(o => o * ratioOne).ToList().ToArray();
            float ratioTwo = 1.0f / sumSentenceTwo.Sum();
            sumSentenceTwo = sumSentenceTwo.Select(o => o * ratioTwo).ToList().ToArray();
            
            return MainUtils.CalculateCosineSimilarity(sumSentenceOne, sumSentenceTwo);
        }

        public static void RunWord2Vec(List<ScrapedWebsite> scrapedWebsites, int summarySize)
        {
            if (scrapedWebsites == null)
                return;

            LoadUpWord2VecDatabase();
            if (word2VecDistance == null)
                return;

            for (int iWebsiteIdx = 0; iWebsiteIdx < scrapedWebsites.Count; iWebsiteIdx++)
            {
                DynamicallyScrapedWebsite scrapedWebsite = scrapedWebsites[iWebsiteIdx] as DynamicallyScrapedWebsite;
                if (scrapedWebsite == null)
                    continue;

                foreach (var scrapingResult in scrapedWebsite.scrapingResults)
                {
                    if (scrapingResult.sentencesWords.Count == 0)
                        continue;

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

                            documentMatrix[sentenceOneIdx].Add(new float());
                            documentMatrix[sentenceOneIdx][sentenceTwoIdx] = GetSentencesSimilarity(sentenceOne, sentenceTwo);
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
                        documentMatrix[rowIdx].RemoveAll(o => Math.Abs(o) < 1e-6);

                    // TextRank algorithm for sentences in documents.
                    var rankedDictionary = new PageRank<string>().Rank(documentGraph, 1.0f);

                    if (rankedDictionary == null)
                        return;

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
                        scrapingResult.contentSummary += scrapingResult.sentencesWords[topSentencesIdx].Aggregate((i, j) => i + " " + j) + Environment.NewLine;
                        wordCount += scrapingResult.sentencesWords[topSentencesIdx].Count;

                        if (wordCount > summarySize)
                        {
                            for (int i = 0; i < scrapingResult.contentSummary.Length; i++)
                            {
                                if (i < scrapingResult.contentSummary.Length - 1 && scrapingResult.contentSummary[i] == ' ' 
                                    && (scrapingResult.contentSummary[i+1] == '.' || scrapingResult.contentSummary[i + 1] == ','))
                                {
                                    scrapingResult.contentSummary = scrapingResult.contentSummary.Remove(i, 1);
                                }
                            }

                            break;
                        }
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
