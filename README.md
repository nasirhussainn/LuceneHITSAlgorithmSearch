This repository contains a simple console application that demonstrates the implementation of the HITS (Hyperlink-Induced Topic Search) algorithm using Lucene.Net. The application builds an inverted index from a set of crawled text files, allowing users to search for keywords and rank the resulting documents using the HITS algorithm.

Features:
Index Creation: Parses text files to extract document IDs and content, creating an inverted index stored in a CSV file.
Inverted Index: The inverted index maps keywords to document IDs along with their frequency.
HITS Algorithm: Implements the HITS algorithm to rank documents based on authority and hub scores.
Search Functionality: Allows users to input a keyword and retrieve ranked document IDs based on the HITS algorithm.
Usage:
Indexing:
Place your text files in the specified directory.
Run the application to create the inverted index and store it in a CSV file.
Searching:
Enter a keyword when prompted by the application.
The application will return the document IDs ranked by their authority and hub scores.
Requirements:
Lucene.Net (version 3.0.3)
.NET Framework
How to Run:
Clone the repository.
Update the file paths in the code to match your directory structure.
Build and run the application.
Follow the on-screen instructions to create the index and search for keywords.
This project serves as a practical example for learning and implementing the HITS algorithm using Lucene.Net.
