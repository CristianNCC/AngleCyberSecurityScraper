## Disclaimer

The code is somewhat dirty since it was written in a very crunch-like manner on top of the fact that it started of as a simple static webscraper, then it turned into a dynamic webscraper, then it needed NLP features and key-word searching and gathering and so on... 

## Description
This is the application I developed for my disertation thesis at the University of Bucharest. It is a web scraper that only takes the URL to the homepage of website as input and then scrapes the pages that contain text information by matching them in a certain template. The scraped text output is then summarized and displayed. It can also search for keywords and display pages where those words are among the most important, while also storing data for further queries. Sadly, the documentation in the repo is in Romanian since this was required.

## Technologies and approaches

Everything is written in C# (the GUI being developed using WPF). The choice of C# over Python was motivated by the extensive use of LINQ that I planned on. The web scraping part is broken in two parts: identifying the template of content pages and filtering out noise. Both tasks are done using modified versions of existing approaches I found while researching (with an extra layer of NLP processing and another layer of Word2Vec filtering). The summarization part uses Word2Vec embeddings to calculate the cosine similarity between sentences and rank them with the TextRank algorithm.

## Building the source

The project can simply be opened in a modern version of Visual Studio using the .sln file and then simply compiled. However, the summarization part requires a trained model of [Word2Vec](https://drive.google.com/file/d/0B7XkCwpI5KDYNlNUTTlSS21pQmM/edit) in the project directory.
