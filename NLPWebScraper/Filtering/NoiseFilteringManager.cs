// This is a personal academic project. Dear PVS-Studio, please check it.

// PVS-Studio Static Code Analyzer for C, C++, C#, and Java: http://www.viva64.com

using AngleSharp.Html.Dom;
using System;
using System.Collections.Generic;
using System.Linq;

public class WebPage
{
    public IHtmlDocument htmlDocument;
    public string pageURL;

    public WebPage(IHtmlDocument htmlDocument, string pageURL)
    {
        this.htmlDocument = htmlDocument;
        this.pageURL = pageURL;
    }
}

namespace NLPWebScraper
{
    public static class NoiseFilteringManager
    {
        public const int maximalWordCount = 20;
        public const int minimumSentenceSize = 5;

        public static int Word2VecMaxCount { get; set; } = 150000;

        public static void NodeFiltering(List<WebPage> webDocuments, ref List<DocumentScrapingResult> scrapingResults)
        {
            if (webDocuments == null || scrapingResults == null)
                return;

            List<DocumentScrapingResult> filteredDocumentNodes = new List<DocumentScrapingResult>();

            int validPagesNumber = scrapingResults.Count(scrapingResult => scrapingResult.isValid);
            var nonValidWebPages = webDocuments.GetRange(validPagesNumber, webDocuments.Count - validPagesNumber);

            if (nonValidWebPages.Count == 0)
                return;

            // Filter away the elements that have no text content.
            var firstIterationFilteredDocuments = nonValidWebPages.Select(pageDocument => pageDocument.htmlDocument).Select(dom => dom.All.ToList()
                .Where(element => !string.IsNullOrEmpty(element.TextContent) &&
                (element is IHtmlDivElement || element is IHtmlParagraphElement || element is IHtmlTableCellElement))
                .ToList()).ToList();

            var docSort = firstIterationFilteredDocuments.First().OrderByDescending(test => test.TextContent.Length).ToList();

            //Since some HTML elements contain one another, we need to filter out the common content.
            foreach (var documentElements in firstIterationFilteredDocuments)
            {
                for (int iElementIdx = 0; iElementIdx < documentElements.Count; iElementIdx++)
                {
                    for (int iElementIdxTwo = 0; iElementIdxTwo < documentElements.Count; iElementIdxTwo++)
                    {
                        if (iElementIdx == iElementIdxTwo)
                            continue;

                        if (documentElements[iElementIdx].TextContent.Contains(documentElements[iElementIdxTwo].TextContent))
                        {
                            documentElements.RemoveAt(iElementIdxTwo);
                            iElementIdx = 0;
                            break;
                        }
                    }
                }
            }

            for (int iDocumentIdx = 0; iDocumentIdx < firstIterationFilteredDocuments.Count; iDocumentIdx++)
            {
                var document = firstIterationFilteredDocuments[iDocumentIdx];

                DocumentScrapingResult documentScrapingResult = new DocumentScrapingResult();
                documentScrapingResult.title = nonValidWebPages[iDocumentIdx].htmlDocument.Title;
                documentScrapingResult.linkToPage = webDocuments[iDocumentIdx].pageURL;

                // List of <element, text density, hyperlink density> tuples.
                List<Tuple<AngleSharp.Dom.IElement, float, float>> documentFeatureAnalyis = new List<Tuple<AngleSharp.Dom.IElement, float, float>>();
                for (int iNodeIdx = 0; iNodeIdx < document.Count; iNodeIdx++)
                {
                    var node = document[iNodeIdx];

                    if (node.BaseUrl.Href.Contains("about") && node.BaseUrl.Href.Contains("blank"))
                        node.BaseUrl.Href = string.Empty;

                    documentFeatureAnalyis.Add(new Tuple<AngleSharp.Dom.IElement, float, float>(node, node.GetNodeTextDensity(), node.GetNodeHyperlinkDensity()));
                }

                if (documentFeatureAnalyis.Count == 0)
                    continue;

                float textDensityThreshold = documentFeatureAnalyis.Average(feature => feature.Item2);
                float hyperLinkDensityThreshold = documentFeatureAnalyis.Average(feature => feature.Item3);

                for (int iNodeIdx = 1; iNodeIdx < documentFeatureAnalyis.Count - 1; iNodeIdx++)
                {
                    var previousNode = documentFeatureAnalyis[iNodeIdx - 1];
                    var currentNode = documentFeatureAnalyis[iNodeIdx];
                    var nextNode = documentFeatureAnalyis[iNodeIdx + 1];

                    if (currentNode.Item3 < hyperLinkDensityThreshold)
                    {
                        if (currentNode.Item2 < textDensityThreshold)
                        {
                            if (nextNode.Item2 < textDensityThreshold)
                            {
                                if (previousNode.Item2 < textDensityThreshold)
                                {
                                    documentFeatureAnalyis.RemoveAt(iNodeIdx);
                                    iNodeIdx--;
                                }
                                else
                                {
                                    documentScrapingResult.scrapingResults.Add(new ScrapingResult(currentNode.Item1, currentNode.Item2));
                                }
                            }
                            else
                            {
                                documentScrapingResult.scrapingResults.Add(new ScrapingResult(currentNode.Item1, currentNode.Item2));
                            }
                        }
                        else
                        {
                            documentScrapingResult.scrapingResults.Add(new ScrapingResult(currentNode.Item1, currentNode.Item2));
                        }
                    }
                    else
                    {
                        documentFeatureAnalyis.RemoveAt(iNodeIdx);
                        iNodeIdx--;
                    }
                }
                filteredDocumentNodes.Add(documentScrapingResult);
            }

            filteredDocumentNodes.AddRange(scrapingResults.GetRange(0, validPagesNumber));
            scrapingResults = new List<DocumentScrapingResult>(filteredDocumentNodes);
        }

        public static void FilterAllCommonStrings(List<DocumentScrapingResult> scrapingResults)
        {
            if (scrapingResults == null)
                return;

            List<string> commonStrings = scrapingResults.FirstOrDefault()?.scrapingResults.Select(node => node.element.TextContent).ToList();
            for (int iDocumentIdx = 1; iDocumentIdx < scrapingResults.Count; iDocumentIdx++)
                commonStrings = commonStrings.Intersect(scrapingResults[iDocumentIdx].scrapingResults.Select(node => node.element.TextContent).ToList()).ToList();

            foreach (var documentNodes in scrapingResults)
            {
                if (documentNodes.isValid)
                    continue;

                documentNodes.scrapingResults.ForEach(node => commonStrings.ForEach(commonString => node.element.TextContent = node.element.TextContent.Replace(commonString, string.Empty)));
                documentNodes.scrapingResults = documentNodes.scrapingResults.Where(node => node.element.TextContent.Length > 0).ToList();
            }
        }

        public static void ApplyNLPFiltering(ref List<DocumentScrapingResult> scrapingResults)
        {
            if (scrapingResults == null)
                return;

            // Aggregate all text content.
            foreach (var documentResult in scrapingResults)
            {
                if (documentResult.isValid)
                    continue;

                documentResult.scrapingResults.ForEach(element => documentResult.content += element.element.TextContent + ".");

                var sentences = OpenNLP.APIOpenNLP.SplitSentences(documentResult.content);

                List<string> filteredSentences = new List<string>();
                List<List<string>> sentencesWords = new List<List<string>>();
                foreach (var sentence in sentences)
                {
                    var filteredSentence = sentence.Replace("\n", "");
                    filteredSentence = filteredSentence.Replace("\t", "");
                    filteredSentences.Add(filteredSentence);

                    sentencesWords.Add(OpenNLP.APIOpenNLP.TokenizeSentence(filteredSentence).ToList());
                }

                List<List<string>> posSentences = new List<List<string>>();
                foreach (var sentenceWordList in sentencesWords)
                    posSentences.Add(OpenNLP.APIOpenNLP.PosTagTokens(sentenceWordList.ToArray()).ToList());

                List<int> indexesToRemove = new List<int>();
                for (int sentenceIndex = 0; sentenceIndex < posSentences.Count; sentenceIndex++)
                {
                    if (!posSentences[sentenceIndex].Any(pos => pos.Contains("V")) || sentencesWords[sentenceIndex].Any(word => word.Length > maximalWordCount))
                        indexesToRemove.Add(sentenceIndex);
                }

                bool wordRemovalConverged = true;
                do
                {
                    wordRemovalConverged = true;
                    for (int sentenceIndex = 0; sentenceIndex < sentencesWords.Count; sentenceIndex++)
                    {
                        List<bool> wordsFoundInSentence = new List<bool>();
                        for (int wordIndex = 0; wordIndex < sentencesWords[sentenceIndex].Count; wordIndex++)
                        {
                            var vec = Word2VecManager.GetVecForWord(sentencesWords[sentenceIndex][wordIndex], Word2VecMaxCount);
                            if (vec.Length == 0)
                                wordsFoundInSentence.Add(false);
                            else
                                wordsFoundInSentence.Add(true);
                        }

                        for (int wordIndex = 1; wordIndex < sentencesWords[sentenceIndex].Count - 1; wordIndex++)
                        {
                            if (!wordsFoundInSentence[wordIndex - 1] && !wordsFoundInSentence[wordIndex] && !wordsFoundInSentence[wordIndex + 1])
                            {
                                sentencesWords[sentenceIndex].RemoveAt(wordIndex + 1);
                                posSentences[sentenceIndex].RemoveAt(wordIndex + 1);
                                wordsFoundInSentence.RemoveAt(wordIndex + 1);

                                sentencesWords[sentenceIndex].RemoveAt(wordIndex);
                                posSentences[sentenceIndex].RemoveAt(wordIndex);
                                wordsFoundInSentence.RemoveAt(wordIndex);
                                wordIndex--;

                                sentencesWords[sentenceIndex].RemoveAt(wordIndex);
                                posSentences[sentenceIndex].RemoveAt(wordIndex);
                                wordsFoundInSentence.RemoveAt(wordIndex);

                                wordRemovalConverged = false;
                            }
                        }
                    }
                } while (!wordRemovalConverged);

                documentResult.content = string.Empty;
                for (int index = 0; index < sentencesWords.Count; index++)
                {
                    if (indexesToRemove.Contains(index))
                        continue;

                    if (sentencesWords[index].Count < minimumSentenceSize)
                        continue;

                    documentResult.sentencesWords.Add(sentencesWords[index]);
                    documentResult.posSentences.Add(posSentences[index]);

                    documentResult.content += sentencesWords[index].Aggregate((i, j) => i + " " + j);
                    documentResult.content += Environment.NewLine;
                }
            }
        }
    }
}
