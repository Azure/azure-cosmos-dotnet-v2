# Capture the .net SDK trace in ETL file

The Azure DocumentDB .NET SDK (from version 1.11.0) support capturing trace in ETL trace log file. The trace is emitted and captured using Windows ETW with very little overhead.

## Steps to collect ETL trace file.

1.  In Windows command prompt:
 
	     logman create trace docdbclienttrace -rt -nb 500 500 -bs 40 -p {B30ABF1C-6A50-4F2B-85C4-61823ED6CF24} -o docdbclient.etl -ft 10
2. When you are ready to collect trace:

         logman start docdbclienttrace

3. When finish collecting:

	    logman stop docdbclienttrace

4. You should see docdbclientxxx.etl in current directory.

## You can send the ETL file to us.  

## Steps to open ETL if you are interested:
 1.  Download the svcperf at [https://svcperf.codeplex.com/](https://svcperf.codeplex.com/)
 2.  Open svcperf.exe.
 3.  In "Manifests|Add", add the ClientEvents.man found in this folder with this 
 4.  Open the etl file you have captured, you might need to hit F5 to refresh it.


