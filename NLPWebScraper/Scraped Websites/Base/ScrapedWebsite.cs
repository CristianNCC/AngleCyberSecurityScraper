using System.Collections.Generic;
using AngleSharp.Html.Dom;
using System.Threading.Tasks;
using System.Threading;
using System.Net.Http;
using AngleSharp.Html.Parser;
using System.IO;

namespace NLPWebScraper
{
    public class Edge<T>
    {
        public Vertex<T> node1;
        public Vertex<T> node2;
    }

    public class Vertex<T>
    {
        public T data;
        public LinkedList<Edge<T>> neighbors;
    }

    public abstract class ScrapedWebsite
    {
        public string siteUrl;
        public ScrapedWebsite(string siteUrl)
        {
            this.siteUrl = siteUrl;
        }

        public async Task<IHtmlDocument> GetDocumentFromLink(string url)
        {
            CancellationTokenSource cancellationToken = new CancellationTokenSource();
            HttpClient httpClient = new HttpClient();
            HtmlParser parser = new HtmlParser();

            HttpResponseMessage request = await httpClient.GetAsync(url);
            cancellationToken.Token.ThrowIfCancellationRequested();

            Stream response = await request.Content.ReadAsStreamAsync();
            cancellationToken.Token.ThrowIfCancellationRequested();

            IHtmlDocument document = parser.ParseDocument(response);

            httpClient.Dispose();
            cancellationToken.Dispose();
            return document;
        }
    }
}
