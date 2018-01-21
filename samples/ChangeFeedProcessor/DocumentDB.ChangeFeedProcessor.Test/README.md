# Change Feed Processor Test
This project contains tests for Change Feed Processor library.

## To run the tests, do the following:
* Install local emulator. See https://docs.microsoft.com/en-us/azure/cosmos-db/local-emulator.
* Run local emulator using command line arguments like this: /port=443 /DefaultPartitionCount=5
* Make sure build configuration is set to x64 (Debug or Release).
* Make sure test are configured to use process architechture x64 (Test->Test Settings->Default Processor Architechture->x64).
* Build the solution
* Open Test Explorer (Test->Windows->Test Explorer) and run tests from there.
