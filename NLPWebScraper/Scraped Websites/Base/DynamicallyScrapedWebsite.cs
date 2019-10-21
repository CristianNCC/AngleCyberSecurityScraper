using AngleSharp.Html.Dom;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NLPWebScraper
{
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
        public List<ScrapingResult> scrapingResults;

        public DocumentScrapingResult(string linkToPage, List<ScrapingResult> scrapingResults)
        {
            this.linkToPage = linkToPage;
            this.scrapingResults = scrapingResults;
        }

        public DocumentScrapingResult()
        {
            this.linkToPage = string.Empty;
            this.scrapingResults = new List<ScrapingResult>();
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
        public const int maximalSubdigraphSize = 4;
        public const float nodeDifferenceEpsilon = 0.1f;
        public const float hyperLinkDensityThreshold = 0.333f;
        public const float textDensityThreshold = 0.5f; 
        public const float thresholdStandardDeviance = 5.0f;

        public DynamicallyScrapedWebsite(string siteUrl) : base(siteUrl)
        {

        }

        public async Task<List<DocumentScrapingResult>> DynamicScraping()
        {  
            var mainPageDocument = await GetDocumentFromLink(siteUrl);
            var mainPageLinks = mainPageDocument.Links.Where(webPageLink => (webPageLink as IHtmlAnchorElement)?.Href != siteUrl)
                    .Select(webPageFilteredLink => (webPageFilteredLink as IHtmlAnchorElement)?.Href).ToHashSet();

            // Removed undefined links.
            mainPageLinks = mainPageLinks.Where(eLink => eLink != "javascript:void(0)").ToHashSet();

            HashSet<string> processedLinks = new HashSet<string>();
            HashSet<Connection<string>> connections = new HashSet<Connection<string>>();
            HashSet<string> bestCS = new HashSet<string>();

            // Get the median for each group of tags and use it as a template.
            Dictionary<string, int> templateFrequency = new Dictionary<string, int>();

            // Get the DOM for the pages in the graph.
            List<IHtmlDocument> webDocuments = new List<IHtmlDocument>();

            // Eliminate links that are shorter than the starting URL.
            mainPageLinks = mainPageLinks.Where(link => link.Length > siteUrl.Length).ToHashSet();

            // Sort remaining links by length.
            mainPageLinks = mainPageLinks.OrderByDescending(link => link.Length).ToHashSet();

            HashSet<HashSet<string>> testedGraphs = new HashSet<HashSet<string>>();
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
                List<HashSet<string>> allCompleteSubdigraphs = GetAllCompleteSubdigraphs(processedLinks, connections, link);

                bool foundSubdigraph = false;
                foreach (var iterationSubdigraph in allCompleteSubdigraphs)
                {
                    if (iterationSubdigraph.Count >= maximalSubdigraphSize)
                    {
                        // Found a good subdigraph, we can return;
                        bestCS = iterationSubdigraph;

                        if (testedGraphs.Any(cs => cs.SetEquals(bestCS)))
                            continue;

                        foreach (var page in bestCS)
                        {
                            var webPageCS = await GetSubPageFromLink(page);
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
                            testedGraphs.Add(new HashSet<string>(bestCS));
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

            // Tags that don't appear are not really interesting.
            templateFrequency = templateFrequency.Where(tagCountPair => tagCountPair.Value != 0).ToDictionary(tagCountPair => tagCountPair.Key, tagCountPair => tagCountPair.Value);

            // Filter away the elements that have no text content.
            var firstIterationFilteredDocuments = webDocuments.Select(dom => dom.All.ToList()
                .Where(element => !string.IsNullOrEmpty(element.TextContent)
                && (element is IHtmlDivElement || element is IHtmlParagraphElement || element is IHtmlHeadElement))
                .ToList()).ToList();


            // Node filtering from "Main content extraction from web pages based on node."
            List<DocumentScrapingResult> filteredDocumentNodes = new List<DocumentScrapingResult>();
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

        public List<HashSet<string>> GetAllCompleteSubdigraphs(IEnumerable<string> processedLinks, HashSet<Connection<string>> connections, string currrentLink)
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
    }
}
