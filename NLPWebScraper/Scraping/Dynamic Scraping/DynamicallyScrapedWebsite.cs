using AngleSharp.Html.Dom;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NLPWebScraper
{
    using BestCSData = Tuple<HashSet<string>, List<IHtmlDocument>, Dictionary<string, int>>;

    public class Connection<T>
    {
        public Connection(T end1, T end2)
        {
            this.end1 = end1;
            this.end2 = end2;
        }

        public T end1;
        public T end2;
    }

    public class DocumentScrapingResult
    {
        public string linkToPage;
        public string title;
        public string content;
        public List<ScrapingResult> scrapingResults = null;

        public List<List<string>> sentencesWords = null;
        public List<List<string>> posSentences = null;

        public DocumentScrapingResult(string linkToPage, List<ScrapingResult> scrapingResults, string content, List<List<string>> sentencesWords, List<List<string>> posSentences)
        {
            this.linkToPage = linkToPage;
            this.scrapingResults = scrapingResults;
            this.content = content;
            this.sentencesWords = sentencesWords;
            this.posSentences = posSentences;
        }

        public DocumentScrapingResult()
        {
            linkToPage = string.Empty;
            scrapingResults = new List<ScrapingResult>();
            content = string.Empty;
            sentencesWords = new List<List<string>>();
            posSentences = new List<List<string>>();
        }
    }

    public class ScrapingResult
    {
        public AngleSharp.Dom.IElement element;
        public float textDensity;

        public ScrapingResult(AngleSharp.Dom.IElement element, float textDensity)
        {
            this.element = element;
            this.textDensity = textDensity;
        }
        public ScrapingResult()
        {
            this.element = null;
            this.textDensity = 0.0f;
        }
    }

    class DynamicallyScrapedWebsite : ScrapedWebsite
    {
        public const int maximalWordCount = 20;
        public const int maximalSubdigraphSize = 4;
        public const float thresholdStandardDeviance = 15.0f;

        public DynamicallyScrapedWebsite(string siteUrl) : base(siteUrl) { }
        public async Task<List<DocumentScrapingResult>> DynamicScraping()
        {  
            var mainPageDocument = await GetDocumentFromLink(siteUrl);

            // Get outgoing links from the main page.
            var mainPageLinks = mainPageDocument.Links.Where(webPageLink => (webPageLink as IHtmlAnchorElement)?.Href != siteUrl)
                    .Select(webPageFilteredLink => (webPageFilteredLink as IHtmlAnchorElement)?.Href).ToHashSet();

            // Removed undefined links.
            mainPageLinks = mainPageLinks.Where(eLink => eLink != "javascript:void(0)").ToHashSet();

            // Mark the links that have already been processed to avoid infinite loops.
            HashSet<string> processedLinks = new HashSet<string>();

            // Eliminate links that are shorter than the starting URL.
            mainPageLinks = mainPageLinks.Where(link => link.Length > siteUrl.Length).ToHashSet();

            // Sort remaining links by length.
            mainPageLinks = mainPageLinks.OrderByDescending(link => link.Length).ToHashSet();

            // Get BestCSData, which is a tuple of the bestCS in links, the DOM for each page and the template frequency for the CS.
            var bestCSData = await GetBestCompleteSubdigraph(mainPageLinks, processedLinks);

            // Tags that don't appear are not really interesting.
            var templateFrequency = bestCSData.Item3.Where(tagCountPair => tagCountPair.Value != 0).ToDictionary(tagCountPair => tagCountPair.Key, tagCountPair => tagCountPair.Value);

            // Node filtering from "Main content extraction from web pages based on node."
            var filteredDocumentNodes = NodeFiltering(bestCSData.Item1, bestCSData.Item2);

            // Remove all common strings from every document. Usually, this will be the text on different buttons.
            FilterAllCommonStrings(filteredDocumentNodes);

            // Apply NLP techniques for filtering.
            ApplyNLPFiltering(filteredDocumentNodes);

            return filteredDocumentNodes;
        }

        public async Task<IHtmlDocument> GetSubPageFromLink(string url)
        {
            IHtmlDocument webPage = null;
            try
            {
                webPage = await GetDocumentFromLink(url);
            }
            catch (Exception)
            {
                try
                {
                    webPage = await GetDocumentFromLink(siteUrl + url);
                }
                catch (Exception)
                {
                    return null;
                }
            }

            return webPage;
        }

        public List<HashSet<string>> GetAllCompleteSubdigraphs(IEnumerable<string> processedLinks, HashSet<Connection<string>> connections)
        {
            List<HashSet<string>> allCompleteSubdigraphs = new List<HashSet<string>>();
            foreach (var pLink in processedLinks)
            {
                HashSet<string> newCS = new HashSet<string>();
                foreach (var connectionOne in connections)
                {
                    if (connectionOne.end1 == pLink)
                    {
                        foreach (var connectionTwo in connections)
                        {
                            if (connectionTwo.end2 == pLink && connectionTwo.end1 == connectionOne.end2)
                            {
                                newCS.Add(connectionTwo.end1);
                                newCS.Add(connectionTwo.end2);
                            }
                        }
                    }
                    else if (connectionOne.end2 == pLink)
                    {
                        foreach (var connectionTwo in connections)
                        {
                            if (connectionTwo.end1 == pLink && connectionTwo.end2 == connectionOne.end1)
                            {
                                newCS.Add(connectionTwo.end1);
                                newCS.Add(connectionTwo.end2);
                            }
                        }
                    }
                }

                if (!allCompleteSubdigraphs.Any(cs => cs.SetEquals(newCS)) && newCS.Count >= maximalSubdigraphSize)
                    allCompleteSubdigraphs.Add(newCS);
            }

            return allCompleteSubdigraphs;
        }

        public async Task<BestCSData> GetBestCompleteSubdigraph(HashSet<string> mainPageLinks, HashSet<string> processedLinks)
        {
            HashSet<string> bestCS = new HashSet<string>();
            List<IHtmlDocument> webDocuments = new List<IHtmlDocument>();
            HashSet<Connection<string>> connections = new HashSet<Connection<string>>();
            HashSet<Tuple<double, HashSet<string>>> testedGraphs = new HashSet<Tuple<double, HashSet<string>>>();

            // Get the median for each group of tags and use it as a template.
            Dictionary<string, int> templateFrequency = new Dictionary<string, int>();

            foreach (var link in mainPageLinks)
            {
                // Get the DOM of the current subpage.
                var webPage = await GetSubPageFromLink(link);
                if (webPage == null)
                    continue;

                // Take note of already processed links.
                processedLinks.Add(link);

                // Take all the links that run from this page and intersect them with the links from the main page (find menu links).
                var existingLinks = webPage.Links.Where(webPageLink => (webPageLink as IHtmlAnchorElement)?.Href != link)
                    .Select(webPageFilteredLink => (webPageFilteredLink as IHtmlAnchorElement)?.Href)
                    .ToList().Intersect(mainPageLinks).ToList();

                // Removed undefined links.
                existingLinks = existingLinks.Where(eLink => eLink != "javascript:void(0)").ToList();

                if (existingLinks.Count <= 1)
                    continue;

                // Add a connection between this link and current existing links.
                existingLinks.ForEach(webPageLink => connections.Add(new Connection<string>(link, webPageLink)));

                // Get all complete subdigraphs.
                List<HashSet<string>> allCompleteSubdigraphs = GetAllCompleteSubdigraphs(processedLinks, connections);
                bool foundSubdigraph = false;
                foreach (var iterationSubdigraph in allCompleteSubdigraphs)
                {
                    if (iterationSubdigraph.Count >= maximalSubdigraphSize)
                    {
                        // Found a good subdigraph, we can return;
                        bestCS = iterationSubdigraph;

                        if (testedGraphs.Any(cs => cs.Item2.SetEquals(bestCS)))
                            continue;

                        // Get the DOM for the pages in the graph.
                        foreach (var page in bestCS)
                        {
                            var webPageCS = await GetSubPageFromLink(page);

                            if (webPage == null)
                                continue;

                            webDocuments.Add(webPageCS);
                        }

                        // Compute the frequency dictionary for every DOM.
                        var pagesFrequencyDictionaryList = webDocuments.Select(dom => dom.All.GroupBy(element => element.GetType().ToString()).ToDictionary(x => x.Key, x => x.Count())).ToList();

                        // Get a set of all the tags that appear.
                        HashSet<string> allHTMLTags = new HashSet<string>();
                        pagesFrequencyDictionaryList.ForEach(pageFrequencyDictionary => pageFrequencyDictionary.Keys.ToList().ForEach(tag => allHTMLTags.Add(tag)));

                        // "Pad" the dictionaries with their respective missing entries.
                        foreach (var pageDictionary in pagesFrequencyDictionaryList)
                        {
                            foreach (var tag in allHTMLTags)
                            {
                                if (!pageDictionary.Keys.Contains(tag))
                                    pageDictionary[tag] = 0;
                            }
                        }

                        // Check standard deviation average.
                        Dictionary<string, double> templateStandardDeviation = new Dictionary<string, double>();

                        var tagsArray = allHTMLTags.ToArray();
                        for (int iTagIdx = 0; iTagIdx < allHTMLTags.Count; iTagIdx++)
                        {
                            List<int> tagValues = new List<int>();
                            foreach (var pageDictionary in pagesFrequencyDictionaryList)
                                tagValues.Add(pageDictionary[tagsArray[iTagIdx]]);

                            templateFrequency[tagsArray[iTagIdx]] = tagValues.GetMedian();
                            templateStandardDeviation[tagsArray[iTagIdx]] = tagValues.GetStandardDeviation();
                        }

                        double averageStandardDeviation = templateStandardDeviation.Values.Sum() / templateStandardDeviation.Values.Count;
                        if (averageStandardDeviation > 0 && averageStandardDeviation < thresholdStandardDeviance)
                        {
                            foundSubdigraph = true;
                            break;
                        }
                        else
                        {
                            testedGraphs.Add(new Tuple<double, HashSet<string>>(averageStandardDeviation, new HashSet<string>(bestCS)));
                            templateFrequency.Clear();
                            bestCS.Clear();
                            webDocuments.Clear();
                        }
                    }
                    else if (iterationSubdigraph.Count > bestCS.Count)
                    {
                        // Replace the best subdigraph if needed.
                        bestCS = iterationSubdigraph;
                    }
                }

                if (foundSubdigraph)
                    break;
            }

            return new Tuple<HashSet<string>, List<IHtmlDocument>, Dictionary<string, int>>(bestCS, webDocuments, templateFrequency);
        }

        public List<DocumentScrapingResult> NodeFiltering(HashSet<string> bestCS, List<IHtmlDocument> webDocuments)
        {
            List<DocumentScrapingResult> filteredDocumentNodes = new List<DocumentScrapingResult>();

            // Filter away the elements that have no text content.
            var firstIterationFilteredDocuments = webDocuments.Select(dom => dom.All.ToList()
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
                documentScrapingResult.title = webDocuments[iDocumentIdx].Title;
                documentScrapingResult.linkToPage = bestCS.ToList()[iDocumentIdx];

                // List of <element, text density, hyperlink density> tuples.
                List<Tuple<AngleSharp.Dom.IElement, float, float>> documentFeatureAnalyis = new List<Tuple<AngleSharp.Dom.IElement, float, float>>();
                for (int iNodeIdx = 0; iNodeIdx < document.Count; iNodeIdx++)
                {
                    var node = document[iNodeIdx];

                    if (node.BaseUrl.Href.Contains("about") && node.BaseUrl.Href.Contains("blank"))
                        node.BaseUrl.Href = string.Empty;

                    documentFeatureAnalyis.Add(new Tuple<AngleSharp.Dom.IElement, float, float>(node, node.GetNodeTextDensity(), node.GetNodeHyperlinkDensity()));
                }

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

            return filteredDocumentNodes;
        }

        public void FilterAllCommonStrings(List<DocumentScrapingResult> filteredDocumentNodes)
        {
            List<string> commonStrings = filteredDocumentNodes.FirstOrDefault()?.scrapingResults.Select(node => node.element.TextContent).ToList();
            for (int iDocumentIdx = 1; iDocumentIdx < filteredDocumentNodes.Count; iDocumentIdx++)
                commonStrings = commonStrings.Intersect(filteredDocumentNodes[iDocumentIdx].scrapingResults.Select(node => node.element.TextContent).ToList()).ToList();

            foreach (var documentNodes in filteredDocumentNodes)
            {
                documentNodes.scrapingResults.ForEach(node => commonStrings.ForEach(commonString => node.element.TextContent = node.element.TextContent.Replace(commonString, string.Empty)));
                documentNodes.scrapingResults = documentNodes.scrapingResults.Where(node => node.element.TextContent.Length > 0).ToList();
            }
        }

        public void ApplyNLPFiltering(List<DocumentScrapingResult> filteredDocumentNodes)
        {
            // Aggregate all text content.
            foreach (var documentResult in filteredDocumentNodes)
            {
                documentResult.scrapingResults.ForEach(element => documentResult.content += element.element.TextContent + ".");

                var sentences = OpenNLP.APIOpenNLP.SplitSentences(documentResult.content);

                List<string> filteredSentences = new List<string>();
                List<List<string>> sentencesWords = new List<List<string>>();
                foreach (var sentence in sentences)
                {
                    var filteredSentence = sentence.Replace("\n", "");
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

                documentResult.content = string.Empty;
                for (int index = 0; index < sentencesWords.Count; index++)
                {
                    if (indexesToRemove.Contains(index))
                        continue;

                    documentResult.sentencesWords.Add(sentencesWords[index]);
                    documentResult.posSentences.Add(posSentences[index]);

                    documentResult.content += filteredSentences[index]; //+ Environment.NewLine;
                }
            }
        }
    }
}
