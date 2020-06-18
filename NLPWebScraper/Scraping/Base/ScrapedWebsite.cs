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
    }
}
