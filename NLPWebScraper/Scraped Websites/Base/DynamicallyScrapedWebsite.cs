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

    class DynamicallyScrapedWebsite : ScrapedWebsite
    {
        public const int maximalSubdigraphSize = 4;

        public DynamicallyScrapedWebsite(string siteUrl) : base(siteUrl)
        {

        }
        public static int GetMedian(List<int> sourceNumbers)
        {
            if (sourceNumbers == null || sourceNumbers.Count == 0)
                throw new System.Exception("Median of empty array not defined.");

            int[] sortedPNumbers = sourceNumbers.ToArray();
            Array.Sort(sortedPNumbers);

            int size = sortedPNumbers.Length;
            int mid = size / 2;
            int median = (size % 2 != 0) ? sortedPNumbers[mid] : (sortedPNumbers[mid] + sortedPNumbers[mid - 1]) / 2;
            return median;
        }

        public async Task<List<List<AngleSharp.Dom.IElement>>> DynamicScraping()
        {  
            var mainPageDocument = await GetDocumentFromLink(siteUrl);
            var mainPageLinks = mainPageDocument.Links.Where(webPageLink => (webPageLink as IHtmlAnchorElement)?.Href != siteUrl)
                    .Select(webPageFilteredLink => (webPageFilteredLink as IHtmlAnchorElement)?.Href).ToHashSet();

            // Removed undefined links.
            mainPageLinks = mainPageLinks.Where(eLink => eLink != "javascript:void(0)").ToHashSet();

            HashSet<string> processedLinks = new HashSet<string>();
            HashSet<Connection<string>> connections = new HashSet<Connection<string>>();
            HashSet<string> bestCS = new HashSet<string>();

            var mainPageDescendingList = mainPageLinks.ToList().OrderByDescending(x => x).ToList();
            foreach (var link in mainPageDescendingList)
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

                // Get maximal subdigraph of this iteration.
                var iterationSubdigraph = allCompleteSubdigraphs.OrderByDescending(graph => graph.Count).FirstOrDefault();

                if (iterationSubdigraph.Count == maximalSubdigraphSize)
                {
                    // Found a good subdigraph, we can return;
                    bestCS = iterationSubdigraph;
                    break;
                }
                else if (iterationSubdigraph.Count > bestCS.Count)
                {
                    // Replace the best subdigraph if needed.
                    bestCS = iterationSubdigraph;
                }
            }

            // Get the DOM for the pages in the graph.
            List<IHtmlDocument> webDocuments = new List<IHtmlDocument>();
            foreach (var page in bestCS)
            {
                var webPage = await GetSubPageFromLink(page); 
                webDocuments.Add(webPage);
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
                    if(!pageDictionary.Keys.Contains(tag))
                        pageDictionary[tag] = 0;
                }
            }

            // Get the median for each group of tags and use it as a template.
            Dictionary<string, int> templateFrequency = new Dictionary<string, int>();

            var tagsArray = allHTMLTags.ToArray();
            for (int iTagIdx = 0; iTagIdx < allHTMLTags.Count; iTagIdx++)
            {
                List<int> tagValues = new List<int>();
                foreach (var pageDictionary in pagesFrequencyDictionaryList)
                    tagValues.Add(pageDictionary[tagsArray[iTagIdx]]);

                templateFrequency[tagsArray[iTagIdx]] = GetMedian(tagValues);
            }

            // Tags that don't appear are not really interesting.
            templateFrequency = templateFrequency.Where(tagCountPair => tagCountPair.Value != 0).ToDictionary(tagCountPair => tagCountPair.Key, tagCountPair => tagCountPair.Value);

            // Filter away the elements that have no text content.
            var firstIterationFilteredDocuments = webDocuments.Select(dom => dom.All.ToList()
                .Where(element => !string.IsNullOrEmpty(element.TextContent) 
                && (element is IHtmlDivElement || element is IHtmlParagraphElement || element is IHtmlSpanElement || element is IHtmlHeadElement) || element is IHtmlTableRowElement)
                .ToList()).ToList();

            // TO DO: Stuff from paper 7.

            return firstIterationFilteredDocuments;
        }

        public List<HashSet<string>> GetAllCompleteSubdigraphs(IEnumerable<string> processedLinks, HashSet<Connection<string>> connections, string currrentLink)
        {
            List<HashSet<string>> allCompleteSubdigraphs = new List<HashSet<string>>();

            int currentSubdigraph = 0;

            foreach (var pLink in processedLinks)
            {
                allCompleteSubdigraphs.Add(new HashSet<string>());

                foreach (var connectionOne in connections)
                {
                    if (connectionOne.end1 == pLink)
                    {
                        foreach (var connectionTwo in connections)
                        {
                            if (connectionTwo.end2 == pLink && connectionTwo.end1 == connectionOne.end2)
                            {
                                allCompleteSubdigraphs[currentSubdigraph].Add(connectionTwo.end1);
                                allCompleteSubdigraphs[currentSubdigraph].Add(connectionTwo.end2);
                            }
                        }
                    }
                    else if (connectionOne.end2 == pLink)
                    {
                        foreach (var connectionTwo in connections)
                        {
                            if (connectionTwo.end1 == pLink && connectionTwo.end2 == connectionOne.end1)
                            {
                                allCompleteSubdigraphs[currentSubdigraph].Add(connectionTwo.end1);
                                allCompleteSubdigraphs[currentSubdigraph].Add(connectionTwo.end2);
                            }
                        }
                    }
                }

                currentSubdigraph++;
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
