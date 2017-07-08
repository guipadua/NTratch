# NTratch
Exception flow and exception handling extractor for C# projects using .NET Compiler Platform (Roslyn).

This is a command line tool that process static C# source code analysing all (try) catch blocks and outputs a CSV file with try-catch metadata and metric values.

Metric data elements reference: 
https://docs.google.com/spreadsheets/d/1AsQqDldNO5zLYc2wjhIXvscNICBjaIxTxBaFNWvV5e0/edit?usp=sharing

# How to execute:

Execution outputs .log file(s), and .csv files.

f=[folder of the project to be evaluated]

NTratch\NTratch ByFolder f

# Anti-patterns detection:

I used these metrics to identify exception handling anti-patterns.
See my paper at: https://guipadua.github.io/icpc2017/


