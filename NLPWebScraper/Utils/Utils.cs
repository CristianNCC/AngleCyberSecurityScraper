using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NLPWebScraper
{
    class Utils
    {
        // https://en.wikipedia.org/wiki/Levenshtein_distance
        public static int ComputeLevenshteinDistance(string a, string b)
        {
            if (String.IsNullOrEmpty(a) && String.IsNullOrEmpty(b))
                return 0;

            if (String.IsNullOrEmpty(a))
                return b.Length;

            if (String.IsNullOrEmpty(b))
                return a.Length;

            int lengthA = a.Length;
            int lengthB = b.Length;
            var distances = new int[lengthA + 1, lengthB + 1];

            for (int i = 0; i <= lengthA; distances[i, 0] = i++) ;
            for (int j = 0; j <= lengthB; distances[0, j] = j++) ;

            for (int i = 1; i <= lengthA; i++)
            {
                for (int j = 1; j <= lengthB; j++)
                {
                    int cost = b[j - 1] == a[i - 1] ? 0 : 1;
                    distances[i, j] = Math.Min
                        (
                        Math.Min(distances[i - 1, j] + 1, distances[i, j - 1] + 1),
                        distances[i - 1, j - 1] + cost
                        );
                }
            }

            return distances[lengthA, lengthB];
        }

        public static List<int> AllIndexesOf(string content, string value)
        {
            if (String.IsNullOrEmpty(value))
                return new List<int>();

            List<int> indexes = new List<int>();
            for (int index = 0; ; index += value.Length)
            {
                index = content.IndexOf(value, index);
                if (index == -1)
                    return indexes;

                indexes.Add(index);
            }
        }

        public static List<Tuple<int, int>> MergeToTuples(List<int> listOne, List<int> listTwo)
        {
            for (int index = 0; index < listOne.Count - 1; index++)
            {
                for (int indexTwo = 1; indexTwo < listTwo.Count; indexTwo++)
                {
                    if (index == indexTwo)
                        continue;

                    if (listTwo[indexTwo] < listOne[index + 1] && listTwo[indexTwo] > listOne[index])
                    {
                        listTwo.RemoveAt(indexTwo - 1);
                        indexTwo--;
                    }
                }
            }

            if (listOne.Count != listTwo.Count)
                return new List<Tuple<int, int>>();

            List<Tuple<int, int>> tupleList = new List<Tuple<int, int>>();
            for (int index = 0; index < listOne.Count; index++)
                tupleList.Add(new Tuple<int, int>(listOne[index], listTwo[index]));

            return tupleList;
        }

        public static List<string> GetNamedEntities(string content, string namedEntity, List<Tuple<int, int>> indexes)
        {
            List<string> namedEntities = new List<string>();

            foreach (var index in indexes)
                namedEntities.Add(content.Substring(index.Item1, index.Item2 - index.Item1));

            return namedEntities;
        }
    }
}
