using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using AngleSharp.Dom;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Diagnostics;
using System.Windows.Navigation;
using System.Windows.Input;
using System.Text.RegularExpressions;
using AngleSharp.Html.Dom;
using System.Threading.Tasks;
using Word2Vec.Net;
using PageRank.Rank;
using PageRank.Graph;

namespace NLPWebScraper
{
    public partial class MainWindow : Window
    {
        public bool analyzeNamedEntities = true;
        public int numberOfPages;
        public List<string> queryTerms;
        List<ScrapedWebsite> scrapedWebsites = new List<ScrapedWebsite>();

        // If scraping for information is false, then the scraping will stop at finding the template.
        public bool scrapeOnlyForTemplate = false;

        // List of dictionaries where Key=Term and list of tuples <url, articleTitle, titlePolarity>
        public List<Dictionary<string, List<Tuple<string, string, int>>>> listTermToScrapeDictionary = new List<Dictionary<string, List<Tuple<string, string, int>>>>();

        public MainWindow()
        {
            InitializeComponent();
            dynamicScrapingCheckbox.IsChecked = true;
            dynamicScrapingGroupBox.IsEnabled = dynamicScrapingCheckbox.IsChecked == true;
            LoadUpSupportedWebsites();
        }

        private async void DynamicScraping()
        {
            string results = string.Empty;
            for (int iWebsiteIdx = 0; iWebsiteIdx < scrapedWebsites.Count; iWebsiteIdx++)
            {
                DynamicallyScrapedWebsite scrapedWebsite = scrapedWebsites[iWebsiteIdx] as DynamicallyScrapedWebsite;
                if (scrapedWebsite == null)
                    continue;

                await Task.Run(() => scrapedWebsite.DynamicScrapingForTemplateExtraction()).ConfigureAwait(true);

                if (!scrapeOnlyForTemplate)
                {
                    await Task.Run(() => scrapedWebsite.DynamicScrapingForInformationGathering(queryTerms, numberOfPages)).ConfigureAwait(true);
                }
                ConcatenateDataForPrint(scrapedWebsite, ref results);
            }
            PrintDataToGUI(results);
        }

        private void PrintDataToGUI(string results)
        {
            foreach (Control control in resultsStackPanel.Children)
            {
                if (control.GetType() == typeof(TextBox))
                {
                    resultsStackPanel.Children.Remove(control);
                    break;
                }
            }

            TextBox textbox = new TextBox()
            {
                Text = results,
                TextWrapping = TextWrapping.Wrap
            };

            resultsStackPanel.Children.Add(textbox);
            spinnerControl.Visibility = System.Windows.Visibility.Collapsed;
        }

        private void ConcatenateDataForPrint(DynamicallyScrapedWebsite scrapedWebsite, ref string results) 
        {
            int iDocumentIdx = 0;
            foreach (var documentResult in scrapedWebsite.scrapingResults)
            {
                results += Environment.NewLine + Environment.NewLine + Environment.NewLine;
                results += "========================================================= Link: " + documentResult.linkToPage + "==============================================================";
                results += Environment.NewLine + Environment.NewLine + Environment.NewLine;

                results += documentResult.content + Environment.NewLine;
                results += "Summary: " + documentResult.contentSummary + Environment.NewLine;

                if (analyzeNamedEntities)
                {
                    results += Environment.NewLine + Environment.NewLine + Environment.NewLine;
                    results += "============== Named Entities ==============";
                    results += Environment.NewLine + Environment.NewLine + Environment.NewLine;

                    var namedEntities = OpenNLP.APIOpenNLP.FindNames(documentResult.content);
                    var dateListTupleIndexes = Utils.MergeToTuples(Utils.AllIndexesOf(namedEntities, "<date>"), (Utils.AllIndexesOf(namedEntities, "</date>")));
                    var personListTupleIndexes = Utils.MergeToTuples(Utils.AllIndexesOf(namedEntities, "<person>"), (Utils.AllIndexesOf(namedEntities, "</person>")));
                    var timeListTupleIndexes = Utils.MergeToTuples(Utils.AllIndexesOf(namedEntities, "<time>"), (Utils.AllIndexesOf(namedEntities, "</time>")));
                    var organizationListTupleIndexes = Utils.MergeToTuples(Utils.AllIndexesOf(namedEntities, "<organization>"), (Utils.AllIndexesOf(namedEntities, "</organization>")));

                    results += dateListTupleIndexes.Count > 0 ? "Dates: " : "";
                    foreach (var tuple in dateListTupleIndexes)
                        results += namedEntities.Substring(tuple.Item1 + 6, (tuple.Item2 - 6) - tuple.Item1) + (tuple != dateListTupleIndexes.Last() ? ", " : "." + Environment.NewLine);

                    results += personListTupleIndexes.Count > 0 ? "People: " : "";
                    foreach (var tuple in personListTupleIndexes)
                        results += namedEntities.Substring(tuple.Item1 + 8, (tuple.Item2 - 8) - tuple.Item1) + (tuple != personListTupleIndexes.Last() ? ", " : "." + Environment.NewLine);

                    results += timeListTupleIndexes.Count > 0 ? "Time: " : "";
                    foreach (var tuple in timeListTupleIndexes)
                        results += namedEntities.Substring(tuple.Item1 + 6, (tuple.Item2 - 6) - tuple.Item1) + (tuple != timeListTupleIndexes.Last() ? ", " : "." + Environment.NewLine);

                    results += organizationListTupleIndexes.Count > 0 ? "Organizations: " : "";
                    foreach (var tuple in organizationListTupleIndexes)
                        results += namedEntities.Substring(tuple.Item1 + 14, (tuple.Item2 - 14) - tuple.Item1) + (tuple != organizationListTupleIndexes.Last() ? ", " : "." + Environment.NewLine);

                    results += Environment.NewLine + Environment.NewLine + Environment.NewLine;
                    results += "============== END Named Entities ==============";
                    results += Environment.NewLine + Environment.NewLine + Environment.NewLine;
                }

                results += Environment.NewLine;

                if (documentResult.topWords.Count > 0)
                {
                    results += "Most important words in document: ";
                    for (int iWordIdx = 0; iWordIdx < documentResult.topWords.Count; iWordIdx++)
                    {
                        results += documentResult.topWords[iWordIdx] + " ";

                        if (iWordIdx != 4)
                            results += ", ";
                        else
                            results += ".";
                    }
                }

                results += Environment.NewLine + Environment.NewLine + Environment.NewLine;
                results += "========================================================= END ==============================================================";
                results += Environment.NewLine + Environment.NewLine + Environment.NewLine;

                iDocumentIdx++;
            }
        }

        private async void StaticScraping()
        {
            for (int iWebsiteIdx = 0; iWebsiteIdx < scrapedWebsites.Count; iWebsiteIdx++)
            {
                StaticallyScrapedWebsite scrapedWebsite = scrapedWebsites[iWebsiteIdx] as StaticallyScrapedWebsite;
                listTermToScrapeDictionary.Add(new Dictionary<string, List<Tuple<string, string, int>>>());

                if (scrapedWebsite == null)
                    continue;

                Task<List<IHtmlDocument>> scrapeWebSiteTask = scrapedWebsite.ScrapeWebsite(numberOfPages);
                List<IHtmlDocument> webDocuments = await scrapeWebSiteTask.ConfigureAwait(true);      

                foreach (var document in webDocuments)
                {
                    FillResultsDictionary(iWebsiteIdx, document.All.Where(x => x.ClassName == scrapedWebsite.storyClassName),
                        scrapedWebsite.CleanUpResultsForUrlAndTitle);
                }
            }

            foreach (var websiteDictionary in listTermToScrapeDictionary)
            {
                foreach (var termToScrape in websiteDictionary)
                {
                    List<Tuple<string, string, int>> termResults = termToScrape.Value;

                    GroupBox termGroupBox = new GroupBox()
                    {
                        Header = termToScrape.Key,
                        Content = new StackPanel()
                        {
                            Orientation = Orientation.Vertical,
                            Margin = new Thickness(5, 5, 5, 5)
                        }
                    };

                    resultsStackPanel.Children.Add(termGroupBox);

                    List<Tuple<string, string, int>> sortedResults = termResults.OrderBy(result => result.Item3).ToList();

                    for (int iTermResult = 0; iTermResult < sortedResults.Count; iTermResult++)
                    {
                        TextBlock title = new TextBlock()
                        {
#pragma warning disable CA1305 // Specify IFormatProvider
                            Text = " Polarity: (" + sortedResults[iTermResult].Item3.ToString() + "): " + sortedResults[iTermResult].Item1
#pragma warning restore CA1305 // Specify IFormatProvider
                        };

                        (termGroupBox.Content as StackPanel).Children.Add(title);

                        Hyperlink hyperlink = new Hyperlink();
                        hyperlink.Inlines.Add(sortedResults[iTermResult].Item2);
                        hyperlink.NavigateUri = new Uri(sortedResults[iTermResult].Item2);
                        hyperlink.RequestNavigate += Hyperlink_RequestNavigate;

                        TextBlock urlTextBlock = new TextBlock();
                        urlTextBlock.Inlines.Add(hyperlink);
                        urlTextBlock.Margin = new Thickness(5, 5, 5, 10);

                        (termGroupBox.Content as StackPanel).Children.Add(urlTextBlock);
                    }
                }
            }

            spinnerControl.Visibility = System.Windows.Visibility.Collapsed;
        }

        public void FillResultsDictionary(int websiteIdx, IEnumerable<IElement> articleLinksList, Func<IElement, Tuple<string, string>> CleanUpResults)
        {
            if (articleLinksList == null)
                return;

            foreach (var result in articleLinksList)
            {
                Tuple<string, string> urlTitleTuple = CleanUpResults(result);
                string url = urlTitleTuple.Item1;
                string articleTitle = urlTitleTuple.Item2;

                foreach (var term in queryTerms)
                {
                    if (!string.IsNullOrWhiteSpace(articleTitle) && !string.IsNullOrWhiteSpace(url))
                    {
#pragma warning disable CA1304 // Specify CultureInfo
                        List<string> tokenizedTitle = OpenNLP.APIOpenNLP.TokenizeSentence(articleTitle).Select(token => token.ToLower()).ToList();
#pragma warning restore CA1304 // Specify CultureInfo

#pragma warning disable CA1304 // Specify CultureInfo
                        if (!tokenizedTitle.Contains(term.ToLower()))
#pragma warning restore CA1304 // Specify CultureInfo
                            continue;

                        int sentencePolarity = sentencePolarity = OpenNLP.APIOpenNLP.AFINNAnalysis(tokenizedTitle.ToArray());

                        if (!listTermToScrapeDictionary[websiteIdx].ContainsKey(term))
                            listTermToScrapeDictionary[websiteIdx][term] = new List<Tuple<string, string, int>>();

                        listTermToScrapeDictionary[websiteIdx][term].Add(new Tuple<string, string, int>(articleTitle, url, sentencePolarity));
                    }
                }
            }
        }

        private void LoadUpSupportedWebsites()
        {
            scrapedWebsites.Add(new HackerNews("https://thehackernews.com/"));
        }

        private void ScrapWebsiteEvent(object sender, RoutedEventArgs e)
        {
            listTermToScrapeDictionary.Clear();
            resultsStackPanel.Children.Clear();
            spinnerControl.Visibility = System.Windows.Visibility.Visible;

            bool castSucceded = int.TryParse(numberOfPagesTextBox.Text, out numberOfPages);
            if (!castSucceded)
                return;

            queryTerms = queryTermsTextBox.Text.Split(';').ToList();

            if (dynamicScrapingCheckbox.IsChecked == true)
            {
                scrapedWebsites.RemoveAll(scrapedWebsite => scrapedWebsite is DynamicallyScrapedWebsite);
                scrapedWebsites.Add(new DynamicallyScrapedWebsite(targetWebsiteTextbox.Text, UpdateTextBoxWithStatus));
                DynamicScraping();
            }
            else
                StaticScraping();
        }

        private void SummarizeEvent(object sender, RoutedEventArgs e)
        {
            spinnerControl.Visibility = System.Windows.Visibility.Visible;
            Application.Current.Dispatcher.Invoke(() =>
            {
                Word2VecWrapper();
            });
        }

        private void Word2VecWrapper()
        {
            var distance = new Distance("googleWord2Vec.bin");

            for (int iWebsiteIdx = 0; iWebsiteIdx < scrapedWebsites.Count; iWebsiteIdx++)
            {
                DynamicallyScrapedWebsite scrapedWebsite = scrapedWebsites[iWebsiteIdx] as DynamicallyScrapedWebsite;
                if (scrapedWebsite == null)
                    continue;

                Dictionary<string, float[]> word2VecCache = new Dictionary<string, float[]>();
                foreach (var scrapingResult in scrapedWebsite.scrapingResults)
                {
                    List<List<float>> documentMatrix = new List<List<float>>();

                    for (int sentenceOneIdx = 0; sentenceOneIdx < scrapingResult.sentencesWords.Count; sentenceOneIdx++)
                    {
                        documentMatrix.Add(new List<float>());
                        for (int sentenceTwoIdx = 0; sentenceTwoIdx < scrapingResult.sentencesWords.Count; sentenceTwoIdx++)
                        {
                            if (sentenceOneIdx == sentenceTwoIdx)
                            {
                                documentMatrix[sentenceOneIdx].Add(new float());
                                documentMatrix[sentenceOneIdx][sentenceTwoIdx] = 0.0f;
                                continue;
                            }

                            string[] sentenceOne = scrapingResult.sentencesWords[sentenceOneIdx].ToArray();
                            string[] sentenceTwo = scrapingResult.sentencesWords[sentenceTwoIdx].ToArray();

                            float[] sumSentenceOne = new float[300];
                            Array.Clear(sumSentenceOne, 0, sumSentenceOne.Length);
                            float[] sumSentenceTwo = new float[300];
                            Array.Clear(sumSentenceTwo, 0, sumSentenceTwo.Length);

                            foreach (var word in sentenceOne)
                            {
                                float[] currentWordVec;
                                if (word2VecCache.ContainsKey(word))
                                {
                                    currentWordVec = word2VecCache[word];
                                }
                                else
                                {
                                    currentWordVec = distance.GetVecForWord(word);
                                    word2VecCache[word] = currentWordVec;
                                }


                                if (currentWordVec.Length == 0)
                                    continue;

                                for (int i = 0; i < 300; i++)
                                    sumSentenceOne[i] += currentWordVec[i];
                            }

                            foreach (var word in sentenceTwo)
                            {
                                float[] currentWordVec;
                                if (word2VecCache.ContainsKey(word))
                                {
                                    currentWordVec = word2VecCache[word];
                                }
                                else
                                {
                                    currentWordVec = distance.GetVecForWord(word);
                                    word2VecCache[word] = currentWordVec;
                                }


                                if (currentWordVec.Length == 0)
                                    continue;

                                for (int i = 0; i < 300; i++)
                                    sumSentenceTwo[i] += currentWordVec[i];
                            }

                            float ratioOne = 1.0f / sumSentenceOne.Sum();
                            sumSentenceOne = sumSentenceOne.Select(o => o * ratioOne).ToList().ToArray();
                            float ratioTwo = 1.0f / sumSentenceTwo.Sum();
                            sumSentenceTwo = sumSentenceTwo.Select(o => o * ratioTwo).ToList().ToArray();

                            documentMatrix[sentenceOneIdx].Add(new float());
                            documentMatrix[sentenceOneIdx][sentenceTwoIdx] = Utils.CalculateCosineSimilarity(sumSentenceOne, sumSentenceTwo);
                        }
                    }

                    for (int rowIdx = 0; rowIdx < scrapingResult.sentencesWords.Count; rowIdx++)
                    {
                        float ratio = 1.0f / documentMatrix[rowIdx].Sum();
                        documentMatrix[rowIdx] = documentMatrix[rowIdx].Select(o => o * ratio).ToList();
                    }

                    // Convert the similarity matrix into an undirected weighted graph.
                    UnDirectedGraph<int> documentGraph = new UnDirectedGraph<int>();
                    for (int sentenceOneIdx = 0; sentenceOneIdx < scrapingResult.sentencesWords.Count; sentenceOneIdx++)
                    {
                        for (int sentenceTwoIdx = 0; sentenceTwoIdx < scrapingResult.sentencesWords.Count; sentenceTwoIdx++)
                        {
                            if (sentenceOneIdx == sentenceTwoIdx)
                                continue;

                            documentGraph.AddEdge(sentenceOneIdx, sentenceTwoIdx, documentMatrix[sentenceOneIdx][sentenceTwoIdx]);
                        }
                    }

                    for (int rowIdx = 0; rowIdx < scrapingResult.sentencesWords.Count; rowIdx++)
                        documentMatrix[rowIdx].RemoveAll(o => o == 0.0f);

                    // TextRank algorithm for sentences in documents.
                    var rankedDictionary = new PageRank<string>().Rank(documentGraph);
                    var topSentencesIndexes = rankedDictionary.ToList().OrderByDescending(sentence => sentence.Key).Take(5).ToList();

                    topSentencesIndexes = topSentencesIndexes.OrderBy(sentence => sentence.Key).ToList();

                    foreach (var topSentencesIdx in topSentencesIndexes)
                    {
                        scrapingResult.contentSummary += scrapingResult.sentencesWords[topSentencesIdx.Key].Aggregate((i,j) => i + " " + j);
                    }
                }
            }

            string results = string.Empty;
            for (int iWebsiteIdx = 0; iWebsiteIdx < scrapedWebsites.Count; iWebsiteIdx++)
            {
                DynamicallyScrapedWebsite scrapedWebsite = scrapedWebsites[iWebsiteIdx] as DynamicallyScrapedWebsite;
                if (scrapedWebsite == null)
                    continue;
                ConcatenateDataForPrint(scrapedWebsite, ref results);
            }
            PrintDataToGUI(results);
        }

        private void UpdateTextBoxWithStatus(int numberOfPagesSoFar, int numberOfPagesInQueue, int numberOfAdequatePagesFound)
        {
            scrapingStatusTextbox.Text = "Scraped: " + numberOfPagesSoFar + ".   Queue: " + numberOfPagesInQueue + ".   Found: " + numberOfAdequatePagesFound + ".";
        }

        private void NumberValidationTextBox(object sender, TextCompositionEventArgs e)
        {
            Regex regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }

        private void ToggleScrapingMode(object sender, RoutedEventArgs e)
        {
            dynamicScrapingGroupBox.IsEnabled = dynamicScrapingCheckbox.IsChecked == true;
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri));
            e.Handled = true;
        }

        private void ScrapeForTemplateIsChecked(object sender, RoutedEventArgs e)
        {
            scrapeOnlyForTemplate = scrapeOnlyForTemplateCheckbox.IsChecked == true;
        }
    }
}
