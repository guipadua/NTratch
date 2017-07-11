# NTratch

This is a command line tool that process static C# source code analysing all (try) catch blocks and outputs a CSV file with try-catch metadata and metric values.

Metric data elements reference: 
https://docs.google.com/spreadsheets/d/1AsQqDldNO5zLYc2wjhIXvscNICBjaIxTxBaFNWvV5e0/edit?usp=sharing

# How to execute:

Execution outputs .log file(s), and .csv files.

f=[folder of the project to be evaluated]

NTratch\NTratch ByFolder f

# Research

See related research papers:

## Anti-patterns detection

Guilherme B. de Pádua and Weiyi Shang. *Studying the Prevalence of Exception Handling Anti-Patterns*. 2017 IEEE/ACM 25th International Conference on Program Comprehension (ICPC 2017), ERA Track. **Best ERA Paper Award** . [Website](https://guipadua.github.io/icpc2017/) [Paper](https://guipadua.github.io/resources/icpc2017-era_prevalence-eh-anti-patterns_cr.pdf) [Slides](https://www.slideshare.net/GuilhermePadua/ieee-icpc-2017-studying-the-prevalence-of-exception-handling-antipatterns)



