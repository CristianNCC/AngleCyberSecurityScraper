using System.Collections.Generic;
using System;

namespace NLPWebScraper
{
    #region Helper classes
    public class SiteTopWordsEntry
    {
        public string pageUrl = null;
        public List<string> topWords = null;

        public SiteTopWordsEntry(string pageUrl, List<string> topWords)
        {
            this.pageUrl = pageUrl;
            this.topWords = topWords;
        }
    }

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
        public string contentSummary;
        public List<ScrapingResult> scrapingResults = null;

        public List<List<string>> sentencesWords = null;
        public List<List<string>> posSentences = null;

        public bool isValid = false;
        public List<string> topWords = new List<string>();

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
            element = null;
            textDensity = 0.0f;
        }
    }

    public class LinkToBeProcessed
    {
        public string link = null;
        public string parentLink = null;
        public int priority = 1;
        public LinkToBeProcessed(string link, string parentLink, int priority)
        {
            this.link = link;
            this.parentLink = parentLink;
            this.priority = priority;
        }
    }
    #endregion
}
