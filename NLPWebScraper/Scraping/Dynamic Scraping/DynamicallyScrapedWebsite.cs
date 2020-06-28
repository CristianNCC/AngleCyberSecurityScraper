// This is a personal academic project. Dear PVS-Studio, please check it.

// PVS-Studio Static Code Analyzer for C, C++, C#, and Java: http://www.viva64.com

using AngleSharp.Html.Dom;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace NLPWebScraper
{
    #region Dynamic scraping
    class DynamicallyScrapedWebsite : ScrapedWebsite
    {
        public delegate void UpdateGUIMethod(string textToPrint);

        #region Members and constructor
        public HashSet<string> bestCS = new HashSet<string>();
        List<WebPage> webDocuments = new List<WebPage>();
        public Dictionary<string, int> websiteTemplate = new Dictionary<string, int>();
        public List<DocumentScrapingResult> scrapingResults = new List<DocumentScrapingResult>();
        public HashSet<string> processedLinks = new HashSet<string>();

        public int previousSerializationMoment = 0;
        public UpdateGUIMethod callbackToGUI;

        public bool isLookingForTemplate = true;

        private const float thresholdStandardDevianceTemplate = 1.0f;
        private const float thresholdStandardDevianceGathering = 1.0f;

        public int MaximalSubdigraphSize { get; set; } = 4;
        public int MaxConnectionsCount { get; set; } = 3000;

        public DynamicallyScrapedWebsite(string siteUrl, int subdigraphSize, int maxConnections, int word2VecMaxCount, UpdateGUIMethod callback) : base(siteUrl) 
        {
            NoiseFilteringManager.Word2VecMaxCount = word2VecMaxCount;
            MaximalSubdigraphSize = subdigraphSize;
            MaxConnectionsCount = maxConnections;
            callbackToGUI = callback;
            SiteDatabaseManager.DeserializeSiteInformation();
        }
        #endregion

        #region Template extraction
        public async Task DynamicScrapingForTemplateExtraction()
        {
            isLookingForTemplate = true;

            var mainPageDocument = await MainUtils.GetDocumentFromLink(siteUrl).ConfigureAwait(true);

            // Get outgoing links from the main page.
            var mainPageLinksStrings = mainPageDocument.Links.Where(webPageLink => (webPageLink as IHtmlAnchorElement)?.Href != siteUrl)
                    .Select(webPageFilteredLink => (webPageFilteredLink as IHtmlAnchorElement)?.Href).ToList();

            HashSet<LinkToBeProcessed> mainPageLinks = new HashSet<LinkToBeProcessed>();
            mainPageLinksStrings.ForEach(mainPageLinkString => mainPageLinks.Add(new LinkToBeProcessed(mainPageLinkString, siteUrl, 1)));

            // Do different heuristics to optimize the processing of links.
            RefreshLinksHashSet(ref mainPageLinks);

            UpdateGUIWithState("Looking for the best complete subdigraph...");

            // Get BestCSData, which is a tuple of the bestCS in links, the DOM for each page and the template frequency for the CS.
            await Task.Run(() => GetBestCompleteSubdigraph(mainPageLinks.Select(mainPageLink => mainPageLink.link).ToHashSet())).ConfigureAwait(true);

            // Tags that don't appear are not really interesting.
            var templateFrequency = websiteTemplate.Where(tagCountPair => tagCountPair.Value != 0).ToDictionary(tagCountPair => tagCountPair.Key, tagCountPair => tagCountPair.Value);

            // Node filtering from "Main content extraction from web pages based on node."
            NoiseFilteringManager.NodeFiltering(webDocuments, ref scrapingResults);
            UpdateGUIWithState("Node filtering with text density and hyperlink density metrics...");

            // Remove all common strings from every document. Usually, this will be the text on different buttons.
            NoiseFilteringManager.FilterAllCommonStrings(scrapingResults);

            UpdateGUIWithState("Node filtering with NLP metrics...");

            // Apply NLP techniques for filtering.
            NoiseFilteringManager.ApplyNLPFiltering(ref scrapingResults);

            // Compute the top words for this selection or articles.
            ComputeTopWords();

            isLookingForTemplate = false;
        }

        #region Template extraction helper methods
        private void UpdateGUIWithState(string stringToPrint)
        {
            // We are not on the main (GUI) thread so we need to update the GUI with an invoke.
            Application.Current.Dispatcher.Invoke(() =>
            {
                callbackToGUI(stringToPrint);
            });
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

                if (!allCompleteSubdigraphs.Any(cs => cs.SetEquals(newCS)) && newCS.Count >= MaximalSubdigraphSize)
                    allCompleteSubdigraphs.Add(newCS);
            }

            return allCompleteSubdigraphs;
        }

        private async Task GetBestCompleteSubdigraph(HashSet<string> mainPageLinks)
        {
            HashSet<Connection<string>> connections = new HashSet<Connection<string>>();
            HashSet<Tuple<double, HashSet<string>, List<IHtmlDocument>, Dictionary<string, int>>> testedGraphs = 
                new HashSet<Tuple<double, HashSet<string>, List<IHtmlDocument>, Dictionary<string, int>>>();

            foreach (var iLink in mainPageLinks)
            {
                var link = iLink;

                // Skip links that sidetrack us to other sites.
                Uri currentLinkUri = new Uri(link);

                string currentLinkHost = currentLinkUri.Host;
                if (link.Contains("about://"))
                    link = link.Replace("about://", "");
                else if (new Uri(siteUrl).Host != currentLinkHost) 
                    continue;

                // Get the DOM of the current subpage.
                var webPage = await MainUtils.GetSubPageFromLink(link, siteUrl).ConfigureAwait(true);
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

                // If the number of connections grows to be too large, exit.
                if (connections.Count > MaxConnectionsCount && testedGraphs.Count > 0)
                    break;

                foreach (var iterationSubdigraph in allCompleteSubdigraphs)
                {
                    if (iterationSubdigraph.Count >= MaximalSubdigraphSize)
                    {
                        // Found a good subdigraph, we can return;
                        bestCS = iterationSubdigraph;

                        if (testedGraphs.Any(cs => cs.Item2.SetEquals(bestCS)))
                            continue;

                        // Get the DOM for the pages in the graph.
                        foreach (var page in bestCS)
                        {
                            var webPageCS = await MainUtils.GetSubPageFromLink(page, siteUrl).ConfigureAwait(true);
                            webDocuments.Add(new WebPage(webPageCS, page));
                        }

                        // Compute the frequency dictionary for every DOM.
                        var pagesFrequencyDictionaryList = webDocuments.Select(pageDocument => pageDocument.htmlDocument).
                            Select(dom => dom.All.GroupBy(element => element.GetType().ToString()).
                            ToDictionary(x => x.Key, x => x.Count())).ToList();

                        // If the standard deviation suits our threshold, stop. If it does not, then remember some data about it.
                        double averageStandardDeviation = GetSimilarityBetweenTemplates(pagesFrequencyDictionaryList);
                        if (averageStandardDeviation > 0 && averageStandardDeviation < thresholdStandardDevianceTemplate)
                        {
                            UpdateGUIWithState("Found subdigraph with deviation below required threshold (" + averageStandardDeviation.ToString() + ")...");
                            foundSubdigraph = true;
                            break;
                        }
                        else
                        {
                            testedGraphs.Add(new Tuple<double, HashSet<string>, List<IHtmlDocument>, Dictionary<string, int>>
                                (averageStandardDeviation, new HashSet<string>(bestCS), 
                                new List<IHtmlDocument>(webDocuments.Select(pageDocument => pageDocument.htmlDocument)), 
                                new Dictionary<string, int>(websiteTemplate)));

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
                    return;
            }

            if (testedGraphs.Count > 0)
            {
                // If no graph is perfectly suitable according to the threshold, just return a best-effort graph.
                var bestTestedGraph = testedGraphs.Where(testedGraph => Math.Abs(testedGraph.Item1 - testedGraphs.Min(minDeviation => minDeviation.Item1)) < 1e-6).First();

                UpdateGUIWithState("No perfectly fitting subdigraph found, using a best-effort subdigraph with " + bestTestedGraph.Item1.ToString() + " deviation...");

                bestCS = bestTestedGraph.Item2;
                websiteTemplate = bestTestedGraph.Item4;

                var csArray = bestCS.ToArray();
                for (int iDoc = 0; iDoc < bestTestedGraph.Item3.Count; iDoc++)
                {
                    webDocuments.Add(new WebPage(bestTestedGraph.Item3[iDoc], csArray[iDoc]));
                }
            }
        }

        private void ComputeTopWords()
        {
            if (isLookingForTemplate)
             UpdateGUIWithState("Computing top words using TF-IDF...");

            var documentsTFIDF = MainUtils.Transform(scrapingResults.Select(result => result.sentencesWords).ToList());
            for (int iDocIdx = 0; iDocIdx < documentsTFIDF.Count; iDocIdx++)
            {
                if (scrapingResults[iDocIdx].isValid)
                    continue;

                var documentVocabulary = documentsTFIDF[iDocIdx].ToList();
                documentVocabulary.Sort((pair1, pair2) => pair1.Value.CompareTo(pair2.Value));
                documentVocabulary.Reverse();
                var topFiveWordsDictionary = documentVocabulary.Take(10).ToList();

                scrapingResults[iDocIdx].topWords = topFiveWordsDictionary.Select(wordDictionary => wordDictionary.Key).ToList();
            }
        }

        private void RefreshLinksHashSet(ref HashSet<LinkToBeProcessed> linksHashSet)
        {
            // Removed undefined links.
            linksHashSet = linksHashSet.Where(linkToProcess => linkToProcess.link != "javascript:void(0)").ToHashSet();

            // Eliminate links that are shorter than the starting URL.
            linksHashSet = linksHashSet.Where(linkToProcess => linkToProcess.link.Length > siteUrl.Length).ToHashSet();

            // Sort remaining links by length.
            linksHashSet = linksHashSet.OrderByDescending(linkToProcess => linkToProcess.priority).ThenByDescending(linkToProcess => linkToProcess.link.Length).ToHashSet();
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
        private HashSet<LinkToBeProcessed> AddLinksToSetAndRefresh(IHtmlDocument document, HashSet<LinkToBeProcessed> linksHashSet, string parentLink)
        {
            var documentLinks = document.Links.Where(webPageLink => (webPageLink as IHtmlAnchorElement)?.Href != parentLink && !processedLinks.Contains((webPageLink as IHtmlAnchorElement)?.Href))
                .Select(webPageFilteredLink => (webPageFilteredLink as IHtmlAnchorElement)?.Href).ToList();

            documentLinks.ForEach(documentLink => linksHashSet.Add(new LinkToBeProcessed(documentLink, parentLink, 1)));

            RefreshLinksHashSet(ref linksHashSet);

            return linksHashSet;
        }

        public async Task DynamicScrapingForInformationGathering(List<string> queryTerms, int numberOfPagesToGather)
        {
            // Make sure we know the template.
            if (websiteTemplate.Count == 0)
            {
                await DynamicScrapingForTemplateExtraction().ConfigureAwait(true);
            }

            UpdateGUIWithState("Template done. Looking for query terms...");

            // Clear the processed links list and add the starting page.
            processedLinks.Clear();
            processedLinks.Add(siteUrl);

            // Mark the pages used for the template extraction as processed too.
            bestCS.ToList().ForEach(link => processedLinks.Add(link));

            // Initialize the set that holds the links to be processed.
            HashSet<LinkToBeProcessed> linksToProcess = new HashSet<LinkToBeProcessed>();

            // Get the main page document.
            var mainPageDocument = await MainUtils.GetDocumentFromLink(siteUrl).ConfigureAwait(true);

            // Get the main page host part needed to avoid going to other websites.
            string mainPageHost = new Uri(siteUrl).Host;

            // Get the main page links as a starting point.
            var mainPageLinks = mainPageDocument.Links.Where(webPageLink => (webPageLink as IHtmlAnchorElement)?.Href != siteUrl)
                    .Select(webPageFilteredLink => (webPageFilteredLink as IHtmlAnchorElement)?.Href).ToList();

            // Add the links from the main page to the set of links to process.
            mainPageLinks.ForEach(link => linksToProcess.Add(new LinkToBeProcessed(link, siteUrl, 1)));

            // Also add the links from the documents used for the template extraction.
            foreach (var link in processedLinks)
            {
                var document = await MainUtils.GetSubPageFromLink(link, siteUrl).ConfigureAwait(true);
                if (document == null)
                    continue;

                linksToProcess = AddLinksToSetAndRefresh(document, linksToProcess, link);
            }

            // Check if we already have relevant information in the database.
            foreach (var databaseEntry in SiteDatabaseManager.extractionDatabase)
            {
                if (new Uri(databaseEntry.pageUrl).Host == new Uri(databaseEntry.pageUrl).Host && 
                    databaseEntry.topWords.Intersect(queryTerms, StringComparer.InvariantCultureIgnoreCase).Count() != 0)
                {
                    if (bestCS.Contains(databaseEntry.pageUrl))
                        continue;

                    var document = await MainUtils.GetSubPageFromLink(databaseEntry.pageUrl, siteUrl).ConfigureAwait(true);
                    if (document == null)
                        continue;

                    bestCS.Add(databaseEntry.pageUrl);
                    webDocuments.Add(new WebPage(document, databaseEntry.pageUrl));
                }
            }

            NoiseFilteringManager.NodeFiltering(webDocuments, ref scrapingResults);
            NoiseFilteringManager.FilterAllCommonStrings(scrapingResults);
            NoiseFilteringManager.ApplyNLPFiltering(ref scrapingResults);

            // Mark elements added from the database as valid.
            foreach (var scrapingResult in scrapingResults)
            {
                foreach (var databaseEntry in SiteDatabaseManager.extractionDatabase)
                {
                    if (databaseEntry.pageUrl == scrapingResult.linkToPage)
                    {
                        scrapingResult.topWords = databaseEntry.topWords;
                        scrapingResult.isValid = true;
                    }
                }
            }

            // Do different heuristics to optimize the processing of links.
            RefreshLinksHashSet(ref linksToProcess);

            List<string> databasePages = SiteDatabaseManager.extractionDatabase.Select(entry => entry.pageUrl).ToList();
            // The main working loop.
            while (true)
            {
                // Stop when we've found enough pages.
                if (webDocuments.Count >= numberOfPagesToGather)
                {
                    // Node filtering from "Main content extraction from web pages based on node."
                    NoiseFilteringManager.NodeFiltering(webDocuments, ref scrapingResults);

                    // Remove all common strings from every document. Usually, this will be the text on different buttons.
                    NoiseFilteringManager.FilterAllCommonStrings(scrapingResults);

                    // Apply NLP techniques for filtering.
                    NoiseFilteringManager.ApplyNLPFiltering(ref scrapingResults);

                    // Compute the top words for this selection or articles.
                    ComputeTopWords();

                    if (queryTerms.Count != 0)
                    {
                        queryTerms = queryTerms.Select(term => term.ToLower()).ToList();
                        for (int iDocIdx = 0; iDocIdx < scrapingResults.Count; iDocIdx++)
                        {
                            if (scrapingResults[iDocIdx].isValid)
                                continue;

                            // Add any new found data to the database of page-topWords entries. It may be useful in the future.
                            SiteDatabaseManager.extractionDatabase.Add(new SiteTopWordsEntry(scrapingResults[iDocIdx].linkToPage, scrapingResults[iDocIdx].topWords));

                            if (scrapingResults[iDocIdx].topWords.Intersect(queryTerms, StringComparer.InvariantCultureIgnoreCase).Count() == 0)
                            {
                                scrapingResults[iDocIdx].isValid = false;
                            }
                            else
                            {
                                // If we find a valid page in regards to our query, any outgoing pages from this one page 
                                // should have an increased priority because they are related to it.
                                scrapingResults[iDocIdx].isValid = true;
                                foreach (var linkToBeProcessed in linksToProcess)
                                {
                                    if (linkToBeProcessed.parentLink == scrapingResults[iDocIdx].linkToPage)
                                        linkToBeProcessed.priority = 10;
                                }
                                RefreshLinksHashSet(ref linksToProcess);
                            }
                        }
                    }
                    else
                    {
                        for (int iDocIdx = 0; iDocIdx < scrapingResults.Count; iDocIdx++)
                        {
                            // Add any new found data to the database of page-topWords entries. It may be useful in the future.
                            SiteDatabaseManager.extractionDatabase.Add(new SiteTopWordsEntry(scrapingResults[iDocIdx].linkToPage, scrapingResults[iDocIdx].topWords));

                            scrapingResults[iDocIdx].isValid = false;
                        }
                    }

                    // Remove all invalid pages and move on.
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

                    UpdateGUIWithState("Scraped: " + MainUtils.PagesScrapedSoFar + ".   Queue: " + linksToProcess.Count + ".   Found: " + scrapingResults.Count + "...");

                    // Update the database of sites periodically.
                    if (MainUtils.PagesScrapedSoFar - previousSerializationMoment > SiteDatabaseManager.databaseUpdateCount)
                    {
                        previousSerializationMoment = MainUtils.PagesScrapedSoFar;
                        SiteDatabaseManager.SerializeSiteInformation();
                    }

                    if (scrapingResults.Count >= numberOfPagesToGather)
                    {
                        SiteDatabaseManager.SerializeSiteInformation();
                        break;
                    }
                    else
                        continue;
                }

                // Stop if we've reached the end of the queue.
                if (linksToProcess.Count == 0)
                    break;

                // Pop the first element out of the set.
                string link = linksToProcess.First().link;
                linksToProcess.Remove(linksToProcess.First());

                // Mark the element as processed or skip it if it has already been processed.
                if (processedLinks.Contains(link) || databasePages.Contains(link))
                    continue;
                else
                    processedLinks.Add(link);

                // Skip links that sidetrack us to other sites.
                Uri currentLinkUri = new Uri(link);
                if (mainPageHost != currentLinkUri.Host)
                    continue;

                var document = await MainUtils.GetSubPageFromLink(link, siteUrl).ConfigureAwait(true);
                if (document == null)
                    continue;

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

                // Add outgoing links to the set of links to be processed.
                linksToProcess = AddLinksToSetAndRefresh(document, linksToProcess, link);

                if (queryTerms.Count != 0)
                {
                    // Before we do all the NLP and Word2Vec processing, we can do a shallow search of the query terms.
                    bool shallowSearch = document.All.Select(element => element.TextContent).ToList().Any(content => queryTerms.Any(queryTerm => content.Contains(queryTerm)));
                    if (!shallowSearch)
                        continue;
                }

                // Add the links and documents.
                bestCS.Add(link);
                webDocuments.Add(new WebPage(document, link));
            }
        }
        #endregion
    }
    #endregion
}
