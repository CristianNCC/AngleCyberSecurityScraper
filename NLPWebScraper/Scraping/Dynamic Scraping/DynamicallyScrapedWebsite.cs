using AngleSharp.Html.Dom;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NLPWebScraper
{
    #region Helper classes
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

        public bool isValid = false;
        public List<string> topFiveRelevantWords = new List<string>();

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
    #endregion

    #region Dynamic scraping
    class DynamicallyScrapedWebsite : ScrapedWebsite
    {
        #region Members and constructor
        public HashSet<string> bestCS = new HashSet<string>();
        List<IHtmlDocument> webDocuments = new List<IHtmlDocument>();
        public Dictionary<string, int> websiteTemplate = new Dictionary<string, int>();
        public List<DocumentScrapingResult> scrapingResults = new List<DocumentScrapingResult>();
        public HashSet<string> processedLinks = new HashSet<string>();

        private const int maximalWordCount = 20;
        private const int maximalSubdigraphSize = 4;
        private const float thresholdStandardDevianceTemplate = 15.0f;
        private const float thresholdStandardDevianceGathering = 1.0f;

        public DynamicallyScrapedWebsite(string siteUrl) : base(siteUrl) { }
        #endregion

        #region Template extraction
        public async Task DynamicScrapingForTemplateExtraction()
        {  
            var mainPageDocument = await GetDocumentFromLink(siteUrl).ConfigureAwait(true);

            // Get outgoing links from the main page.
            var mainPageLinks = mainPageDocument.Links.Where(webPageLink => (webPageLink as IHtmlAnchorElement)?.Href != siteUrl)
                    .Select(webPageFilteredLink => (webPageFilteredLink as IHtmlAnchorElement)?.Href).ToHashSet();

            // Do different heuristics to optimize the processing of links.
            RefreshLinksHashSet(ref mainPageLinks);

            // Get BestCSData, which is a tuple of the bestCS in links, the DOM for each page and the template frequency for the CS.
            await Task.Run(() => GetBestCompleteSubdigraph(mainPageLinks)).ConfigureAwait(true);

            // Tags that don't appear are not really interesting.
            var templateFrequency = websiteTemplate.Where(tagCountPair => tagCountPair.Value != 0).ToDictionary(tagCountPair => tagCountPair.Key, tagCountPair => tagCountPair.Value);

            // Node filtering from "Main content extraction from web pages based on node."
            scrapingResults = NodeFiltering();

            // Remove all common strings from every document. Usually, this will be the text on different buttons.
            FilterAllCommonStrings();

            // Apply NLP techniques for filtering.
            ApplyNLPFiltering();
        }

        #region Template extraction helper methods
        private async Task<IHtmlDocument> GetSubPageFromLink(string url)
        {
            IHtmlDocument webPage;
            try
            {
                webPage = await GetDocumentFromLink(url).ConfigureAwait(true);
            }
            catch (Exception)
            {
                try
                {
                    webPage = await GetDocumentFromLink(siteUrl + url).ConfigureAwait(true);
                }
                catch (Exception)
                {
                    return null;
                }
            }

            return webPage;
        }

        private List<HashSet<string>> GetAllCompleteSubdigraphs(HashSet<Connection<string>> connections)
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

        private async Task GetBestCompleteSubdigraph(HashSet<string> mainPageLinks)
        {
            HashSet<Connection<string>> connections = new HashSet<Connection<string>>();
            HashSet<Tuple<double, HashSet<string>>> testedGraphs = new HashSet<Tuple<double, HashSet<string>>>();

            foreach (var link in mainPageLinks)
            {
                // Skip links that sidetrack us to other sites.
                Uri currentLinkUri = new Uri(link);
                if (currentLinkUri == null || new Uri(siteUrl).Host != currentLinkUri.Host)
                    continue;

                // Get the DOM of the current subpage.
                var webPage = await GetSubPageFromLink(link).ConfigureAwait(true);
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
                List<HashSet<string>> allCompleteSubdigraphs = GetAllCompleteSubdigraphs(connections);
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
                            var webPageCS = await GetSubPageFromLink(page).ConfigureAwait(true);

                            if (webPage == null)
                                continue;

                            webDocuments.Add(webPageCS);
                        }

                        // Compute the frequency dictionary for every DOM.
                        var pagesFrequencyDictionaryList = webDocuments.Select(dom => dom.All.GroupBy(element => element.GetType().ToString()).ToDictionary(x => x.Key, x => x.Count())).ToList();

                        double averageStandardDeviation = GetSimilarityBetweenTemplates(pagesFrequencyDictionaryList);
                        if (averageStandardDeviation > 0 && averageStandardDeviation < thresholdStandardDevianceTemplate)
                        {
                            foundSubdigraph = true;
                            break;
                        }
                        else
                        {
                            testedGraphs.Add(new Tuple<double, HashSet<string>>(averageStandardDeviation, new HashSet<string>(bestCS)));
                            websiteTemplate.Clear();
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
        }

        private List<DocumentScrapingResult> NodeFiltering()
        {
            List<DocumentScrapingResult> filteredDocumentNodes = new List<DocumentScrapingResult>();

            int validPagesNumber = scrapingResults.Count(scrapingResult => scrapingResult.isValid);
            var nonValidWebPages = webDocuments.GetRange(validPagesNumber, webDocuments.Count - validPagesNumber);

            // Filter away the elements that have no text content.
            var firstIterationFilteredDocuments = nonValidWebPages.Select(dom => dom.All.ToList()
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
                documentScrapingResult.title = nonValidWebPages[iDocumentIdx].Title;
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

            filteredDocumentNodes.AddRange(scrapingResults.GetRange(0, validPagesNumber));
            return filteredDocumentNodes;
        }

        private void FilterAllCommonStrings()
        {
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

        private void ApplyNLPFiltering()
        {
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

        private void RefreshLinksHashSet(ref HashSet<string> linksHashSet)
        {
            // Removed undefined links.
            linksHashSet = linksHashSet.Where(eLink => eLink != "javascript:void(0)").ToHashSet();

            // Eliminate links that are shorter than the starting URL.
            linksHashSet = linksHashSet.Where(link => link.Length > siteUrl.Length).ToHashSet();

            // Sort remaining links by length.
            //linksHashSet = linksHashSet.OrderByDescending(link => link.Length).ToHashSet();
        }

        private double GetSimilarityBetweenTemplates(List<Dictionary<string, int>> mainAndCurrentPageTemplates)
        {
            // Get a set of all the tags that appear.
            HashSet<string> allHTMLTags = new HashSet<string>();
            mainAndCurrentPageTemplates.ForEach(pageFrequencyDictionary => pageFrequencyDictionary.Keys.ToList().ForEach(tag => allHTMLTags.Add(tag)));

            // "Pad" the dictionaries with their respective missing entries.
            foreach (var pageDictionary in mainAndCurrentPageTemplates)
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
                foreach (var pageDictionary in mainAndCurrentPageTemplates)
                    tagValues.Add(pageDictionary[tagsArray[iTagIdx]]);

                websiteTemplate[tagsArray[iTagIdx]] = tagValues.GetMedian();
                templateStandardDeviation[tagsArray[iTagIdx]] = tagValues.GetStandardDeviation();
            }

            return templateStandardDeviation.Values.Sum() / templateStandardDeviation.Values.Count;
        }
        #endregion

        #endregion

        #region Information gathering
        private async Task<Tuple<IHtmlDocument, HashSet<string>>> AddLinksToSetAndRefresh(HashSet<string> linksHashSet, string link)
        {
            var document = await GetSubPageFromLink(link).ConfigureAwait(true);
            if (document == null)
                return null;

            var documentLinks = document.Links.Where(webPageLink => (webPageLink as IHtmlAnchorElement)?.Href != link)
                .Select(webPageFilteredLink => (webPageFilteredLink as IHtmlAnchorElement)?.Href).ToList();

            documentLinks.ForEach(documentLink => linksHashSet.Add(documentLink));

            RefreshLinksHashSet(ref linksHashSet);

            return new Tuple<IHtmlDocument, HashSet<string>> (document, linksHashSet);
        }

        public async Task DynamicScrapingForInformationGathering(List<string> queryTerms, int numberOfPagesToGather)
        {
            // Clear the processed links list and add the starting page.
            processedLinks.Clear();
            processedLinks.Add(siteUrl);

            // Mark the pages used for the template extraction as processed too.
            bestCS.ToList().ForEach(link => processedLinks.Add(link));

            // Initialize the set that holds the links to be processed.
            HashSet<string> linksToProcess = new HashSet<string>();

            // Get the main page document.
            var mainPageDocument = await GetDocumentFromLink(siteUrl).ConfigureAwait(true);

            // Get the main page host part needed to avoid going to other websites.
            string mainPageHost = new Uri(siteUrl).Host;

            // Get the main page links as a starting point.
            var mainPageLinks = mainPageDocument.Links.Where(webPageLink => (webPageLink as IHtmlAnchorElement)?.Href != siteUrl)
                    .Select(webPageFilteredLink => (webPageFilteredLink as IHtmlAnchorElement)?.Href).ToList();

            // Add the links from the main page to the set of links to process.
            mainPageLinks.ForEach(link => linksToProcess.Add(link));

            // Also add the links from the documents used for the template extraction.
            foreach (var link in processedLinks)
            {
                var documentLinksTuple = await Task.Run(() => AddLinksToSetAndRefresh(linksToProcess, link)).ConfigureAwait(true);
                linksToProcess = documentLinksTuple.Item2;
            }

            // Do different heuristics to optimize the processing of links.
            RefreshLinksHashSet(ref linksToProcess);

            // The main working loop.
            while(true)
            {
                // Stop when we've found enough pages.
                if (webDocuments.Count == numberOfPagesToGather)
                {
                    // Node filtering from "Main content extraction from web pages based on node."
                    scrapingResults = NodeFiltering();

                    // Remove all common strings from every document. Usually, this will be the text on different buttons.
                    FilterAllCommonStrings();

                    // Apply NLP techniques for filtering.
                    ApplyNLPFiltering();

                    var documentsTFIDF = Utils.Transform(scrapingResults.Select(result => result.sentencesWords).ToList());

                    for (int iDocIdx = 0; iDocIdx < documentsTFIDF.Count; iDocIdx++)
                    {
                        if (scrapingResults[iDocIdx].isValid)
                            continue;

                        var documentVocabulary = documentsTFIDF[iDocIdx].ToList();
                        documentVocabulary.Sort((pair1, pair2) => pair1.Value.CompareTo(pair2.Value));
                        documentVocabulary.Reverse();
                        var topFiveWordsDictionary = documentVocabulary.Take(5).ToList();

                        scrapingResults[iDocIdx].topFiveRelevantWords = topFiveWordsDictionary.Select(wordDictionary => wordDictionary.Key).ToList();
                        queryTerms = queryTerms.Select(term => term.ToLower()).ToList();

                        if (scrapingResults[iDocIdx].topFiveRelevantWords.Intersect(queryTerms, StringComparer.InvariantCultureIgnoreCase).Count() == 0)
                            scrapingResults[iDocIdx].isValid = false;
                        else
                            scrapingResults[iDocIdx].isValid = true;
                    }

                    for (int iDocIdx = 0; iDocIdx < scrapingResults.Count; iDocIdx++)
                    {
                        if (!scrapingResults[iDocIdx].isValid)
                        {
                            bestCS.Remove(scrapingResults[iDocIdx].linkToPage);
                            webDocuments.RemoveAt(iDocIdx);
                            scrapingResults.RemoveAt(iDocIdx);
                            iDocIdx--;
                        }
                    }

                    if (scrapingResults.Count == numberOfPagesToGather)
                        break;
                    else
                        continue;
                }

                // Pop the first element out of the set.
                string link = linksToProcess.First();
                linksToProcess.Remove(link);

                // Mark the element as processed or skip it if it has already been processed.
                if (processedLinks.Contains(link))
                    continue;
                else
                    processedLinks.Add(link);

                // Skip links that sidetrack us to other sites.
                Uri currentLinkUri = new Uri(link);
                if (currentLinkUri == null || mainPageHost != currentLinkUri.Host)
                    continue;

                // Get the document and also add its outgoing links to the set of links to be processed.
                var documentLinksTuple = await Task.Run(() => AddLinksToSetAndRefresh(linksToProcess, link)).ConfigureAwait(true);

                var document = documentLinksTuple.Item1;
                linksToProcess = documentLinksTuple.Item2;

                // Compute the frequency array for this page.
                var currentPageDictionary = document.All.GroupBy(element => element.GetType().ToString()).ToDictionary(x => x.Key, x => x.Count());

                // Make a list of the two templates we're comparing.
                List<Dictionary<string, int>> mainAndCurrentPageTemplates = new List<Dictionary<string, int>>
                {
                    websiteTemplate,
                    currentPageDictionary
                };

                double averageStandardDeviation = GetSimilarityBetweenTemplates(mainAndCurrentPageTemplates);
                if (averageStandardDeviation < 0 || averageStandardDeviation > thresholdStandardDevianceGathering)
                    continue;

                bestCS.Add(link);
                webDocuments.Add(document);
            }
        }
        #endregion
    }
    #endregion
}
