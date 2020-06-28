// This is a personal academic project. Dear PVS-Studio, please check it.

// PVS-Studio Static Code Analyzer for C, C++, C#, and Java: http://www.viva64.com

using System.Collections.Generic;

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
