// This is a personal academic project. Dear PVS-Studio, please check it.

// PVS-Studio Static Code Analyzer for C, C++, C#, and Java: http://www.viva64.com

using System;
using System.Collections.Generic;
using System.Linq;

namespace NLPWebScraper
{
    public static class Extensions
    {
        public static double GetStandardDeviation(this List<int> values)
        {
            if (values == null)
                return 0.0f;

            double ret = 0;
            if (values.Count > 1)
            {  
                double avg = values.Average();     
                double sum = values.Sum(d => Math.Pow(d - avg, 2));    
                ret = Math.Sqrt((sum) / (values.Count - 1));
            }
            return ret;
        }

        public static int GetMedian(this List<int> sourceNumbers)
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

        public static float GetNodeTextDensity(this AngleSharp.Dom.IElement element)
        {
            if (element == null)
                return 0.0f;

            return ((float)element.TextContent.Length) / ((float)element.InnerHtml.Length);
        }

        public static float GetNodeHyperlinkDensity(this AngleSharp.Dom.IElement element)
        {
            if (element == null)
                return 0.0f;

            return ((float)element.BaseUri.Length) / ((float)element.InnerHtml.Length);
        }

        public static bool IsSimilarWith(this Tuple<AngleSharp.Dom.IElement, float, float> element, Tuple<AngleSharp.Dom.IElement, float, float> toCompare, float epsilon)
        {
            if (element == null || toCompare == null)
                return false;

            return Math.Abs((element.Item2 - toCompare.Item2) * 0.2) + Math.Abs((element.Item3 - toCompare.Item3) * 0.8) < epsilon;
        }

        public static float DotProduct(this float[] vec1, float[] vec2)
        {
            if (vec1 == null)
                return 0;

            if (vec2 == null)
                return 0;

            if (vec1.Length != vec2.Length)
                return 0;

            float tVal = 0;
            for (int x = 0; x < vec1.Length; x++)
            {
                tVal += vec1[x] * vec2[x];
            }

            return tVal;
        }
    }
}
