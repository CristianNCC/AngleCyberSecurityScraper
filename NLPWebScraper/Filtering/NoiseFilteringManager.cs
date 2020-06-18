using AngleSharp.Dom;
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
    }
}
