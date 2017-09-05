# ConcurrentPipe
concurrent cli tool for team city builds/tests pipeline. it make team city pipelines organize in proper serial and parallel pattern.

## Why Concurrent?
In git flow, we always have to wait for CI builds/tests for conflicts and bug fixes before merges. Many developer have the experience that productivity is compromised by the CI pipeline if CI takes too long to do all the tasks.

Did you know:
1) In angular, the @angular/cli multiple ng builds and ng test can be run in parallel. In our case, the serial builds of angular project targeting to 3 different environments cost around 4 minutes. The ng unit test takes another 1.3 minute. But they are be run in parallel and take only 1.3 min in all.
2) For dotnet, except for the dotnet restore step (which is similar to npm install), all the rest of your builds, tests, publishes, packs can be also run in parallel. The parallal flow will also save you a few minutes depends on how many steps you have.

## What is ConcurrentPipe? (cpipe tool)
The cpipe provide concurrent and serial orchestration for all command line tools and capture their output and errors and report them in teamcity log blocks.

## How to use cpipe?

cpipe has a setting file (concurrent.json, located in the same folder with cpipe.exe), in which you can define your steps as json. However, to make cpipe work with teamcity setting, cpipe can accept json runner to execute an orchestration.
```cpipe -j <JSON Runner> ```


Here is an example of setting file. In this file, you have a defined runners: "ngbuilds", "dotnet", "publish" and "orchestra". We will explain how to orchestrate the concurrent and serial job flow later (it is simple).

1) If you want to run one or a few defined runners in the concurrent.json, use ```cpipe -r <runner key> <another runner key> <...>```
2) If you want to run all the defined runners, just call ```cpipe -r```
3) If you want to run a runner defined by a json input, use ```cpipe -j <escaped json of the runner>```. But the json must be escaped for command line. Because the command line escape is tricky, so you can actually edit your runner in the json file and emit one or a few runner by calling ```cpipe -e <runner key> <another runner key> <...>```, or use ```cpipe -e``` to emit all of them.

*This is concurrent.json example*
```javascript
{
  "Runners": {
    "ngbuilds": {
      "Name": "ng builds in parallel",
      "Timeout": 900,
      "Commands": [
        "npm run pipeline-ci",
        "npm run pipeline-devtest",
        "npm run pipeline-staging",
        "npm run pipeline-test"
      ]
    },
    "dotnet": {
      "Name": ".Net Builds and Tests",
      "Timeout": 900,
      "Commands": [
        "dotnet build Sims.Bridges.sln"
      ],
      "Runners": [
        {
          "Name": ".Net Tests",
          "Timeout": 900,
          "Commands": [
            "cd inventory\\Sims.Core.Inventory.UnitTests & dotnet test",
            "cd Sims.Bridges.UnitTests & dotnet test"
          ]
        }
      ]
    },
    "publish": {
      "Name": ".Net Publish",
      "Timeout": 900,
      "Commands": [
        "cd Sims.Bridges.Web & dotnet publish -c Release",
        "cd Sims.Bridges.Import\\Client\\Sims.Bridges.Import.Client\\Sims.Bridges.Import.Client & dotnet publish -c Release",
        "cd Shared\\Sims.Shared & dotnet pack -c Release"
      ]
    },
    "orchestra": {
      "Name": "orchestra",
      "Timeout": 900,
      "Commands": [],
      "Runners": [
        {
          "Name": ".Net Restore",
          "Timeout": 900,
          "Commands": [
            "dotnet restore Sims.Bridges.sln"
          ],
          "Runners":[
            {
              "Name": ".Net Build Test Publish",
              "Timeout": 900,
              "Commands": [
                "dotnet build Sims.Bridges.sln -c Release -o build",
                "cd inventory\\Sims.Core.Inventory.UnitTests & dotnet test -o test",
                "cd Sims.Bridges.UnitTests & dotnet test -o test",
                "cd Sims.Bridges.Web & dotnet publish -c Release",
                "cd Sims.Bridges.Import\\Client\\Sims.Bridges.Import.Client\\Sims.Bridges.Import.Client & dotnet publish -c Release",
                "cd Shared\\Sims.Shared & dotnet pack -c Release"
              ]
            }
          ]
        },
        {
          "Name": "NPM Install",
          "Timeout": 900,
          "Commands": [
            "cd Sims.Bridges.UI & \"C:\\SIAMATools\\npmci\\npmc.exe\""
          ],
          "Runners":[
            {
              "Name": "ng builds and tests",
              "Timeout": 900,
              "Commands": [
                "cd Sims.Bridges.UI & npm run pipeline-ci",
                "cd Sims.Bridges.UI & npm run pipeline-devtest",
                "cd Sims.Bridges.UI & npm run pipeline-staging",
                "cd Sims.Bridges.UI & npm run pipeline-test"
              ]
            }
          ]
        }
      ]
    }
  }
}
```
*This is the emit output for command line by using ```cpipe -e```*
```
Key: ngbuilds
-j "{\"Name\":\"ng builds in parallel\",\"Timeout\":900,\"Commands\":[\"npm run pipeline-ci\",\"npm run pipeline-devtest\",\"npm run pipeline-staging\",\"npm run pipeline-test\"],\"ReadyTimeout\":100}"
Key: dotnet
-j "{\"Name\":\".Net Builds and Tests\",\"Timeout\":900,\"Commands\":[\"dotnet build Sims.Bridges.sln\"],\"ReadyTimeout\":100,\"Runners\":[{\"Name\":\".Net Tests\",\"Timeout\":900,\"Commands\":[\"cd inventory\\Sims.Core.Inventory.UnitTests & dotnet test\",\"cd Sims.Bridges.UnitTests & dotnet test\"],\"ReadyTimeout\":100}]}"
Key: publish
-j "{\"Name\":\".Net Publish\",\"Timeout\":900,\"Commands\":[\"cd Sims.Bridges.Web & dotnet publish -c Release\",\"cd Sims.Bridges.Import\\Client\\Sims.Bridges.Import.Client\\Sims.Bridges.Import.Client & dotnet publish -c Release\",\"cd Shared\\Sims.Shared & dotnet pack -c Release\"],\"ReadyTimeout\":100}"
Key: orchestra
-j "{\"Name\":\"orchestra\",\"Timeout\":900,\"Commands\":[],\"ReadyTimeout\":100,\"Runners\":[{\"Name\":\".Net Restore\",\"Timeout\":900,\"Commands\":[\"dotnet restore Sims.Bridges.sln\"],\"ReadyTimeout\":100,\"Runners\":[{\"Name\":\".Net Build Test Publish\",\"Timeout\":900,\"Commands\":[\"dotnet build Sims.Bridges.sln -c Release -o build\",\"cd inventory\\Sims.Core.Inventory.UnitTests & dotnet test -o test\",\"cd Sims.Bridges.UnitTests & dotnet test -o test\",\"cd Sims.Bridges.Web & dotnet publish -c Release\",\"cd Sims.Bridges.Import\\Client\\Sims.Bridges.Import.Client\\Sims.Bridges.Import.Client & dotnet publish -c Release\",\"cd Shared\\Sims.Shared & dotnet pack -c Release\"],\"ReadyTimeout\":100,\"Runners\":[{\"Name\":\".Net Tests and Publish\",\"Timeout\":900,\"Commands\":[],\"ReadyTimeout\":100}]}]},{\"Name\":\"NPM Install\",\"Timeout\":900,\"Commands\":[\"cd Sims.Bridges.UI & \\\"C:\\SIAMATools\\npmci\\npmc.exe\\\"\"],\"ReadyTimeout\":100,\"Runners\":[{\"Name\":\"ng builds and tests\",\"Timeout\":900,\"Commands\":[\"cd Sims.Bridges.UI & npm run pipeline-ci\",\"cd Sims.Bridges.UI & npm run pipeline-devtest\",\"cd Sims.Bridges.UI & npm run pipeline-staging\",\"cd Sims.Bridges.UI & npm run pipeline-test\"],\"ReadyTimeout\":100}]}]}"
```

So you can grab any of the -j section as command line argument for your teamcity.

For example:
```
cpipe -j "{\"Name\":\".Net Publish\",\"Timeout\":900,\"Commands\":[\"cd Sims.Bridges.Web & dotnet publish -c Release\",\"cd Sims.Bridges.Import\\Client\\Sims.Bridges.Import.Client\\Sims.Bridges.Import.Client & dotnet publish -c Release\",\"cd Shared\\Sims.Shared & dotnet pack -c Release\"],\"ReadyTimeout\":100}"
```

## How to orchestrate the cpipe runners?
To do this, you need to understand how cpipe execute the tasks.
A typical runner has "Name", "Timeout", "Alives", "ReadyChecks" (which should be regulare expression patterns), "Commands", "Runners". 
when cpipe execute a runner, it will: 

*1) if "Alives" are defined, execute the "Alives" command lines in parallel.*
*2) if "ReadyChecks" are defined, use "ReadyChecks" to test if the "Alives" processes have produced some string patterns. It won't execute the "Commands" until it detected all the patterns. This step is for integration tests orchestration where you need to start a backend server before you can run the UI or other tests.*
*3) execute the "Commands" in parallel*
*4) execute all the "Runners" in parallel*
*5) kill the "Alives" if they are still alive*

To orchestrate parallel execution, you only need to put all the parallel tasks in the "Commands" of a runner.

If you want to schedule something after a set of "Commands", you need to add Runner to the "Runners". But be aware that all the "Runners" are also run in parallel. Additional in serial steps need to be then in the "Runners" of your sub-runner...

Take the following one as example:

1. It top level runner has no commands, it actually separate the frond end and back end builds/tests in two sub-runners.
2. The ".Net Restore" runner run ```dotnet restore``` first and then run all the rest builds and tests in parallel.
3. The "NPM Install" runner use npmc to do cached npm install and then run all the builds and tests in paralle.
```javascript
{
      "Name": "orchestra",
      "Timeout": 900,
      "Commands": [],
      "Runners": [
        {
          "Name": ".Net Restore",
          "Timeout": 900,
          "Commands": [
            "dotnet restore Sims.Bridges.sln"
          ],
          "Runners":[
            {
              "Name": ".Net Build Test Publish",
              "Timeout": 900,
              "Commands": [
                "dotnet build Sims.Bridges.sln -c Release -o build",
                "cd inventory\\Sims.Core.Inventory.UnitTests & dotnet test -o test",
                "cd Sims.Bridges.UnitTests & dotnet test -o test",
                "cd Sims.Bridges.Web & dotnet publish -c Release",
                "cd Sims.Bridges.Import\\Client\\Sims.Bridges.Import.Client\\Sims.Bridges.Import.Client & dotnet publish -c Release",
                "cd Shared\\Sims.Shared & dotnet pack -c Release"
              ]
            }
          ]
        },
        {
          "Name": "NPM Install",
          "Timeout": 900,
          "Commands": [
            "cd Sims.Bridges.UI & \"C:\\SIAMATools\\npmci\\npmc.exe\""
          ],
          "Runners":[
            {
              "Name": "ng builds and tests",
              "Timeout": 900,
              "Commands": [
                "cd Sims.Bridges.UI & npm run pipeline-ci",
                "cd Sims.Bridges.UI & npm run pipeline-devtest",
                "cd Sims.Bridges.UI & npm run pipeline-staging",
                "cd Sims.Bridges.UI & npm run pipeline-test"
              ]
            }
          ]
        }
      ]
    }
```

## Some tricks for dotnet builds and tests
The dotnet build commands can all accept the ```-o [folder name]``` option to do something in separate folder. When you are running your tests, please specify a "test" folder so that you can avoid file access conflicts in the parallel orchestration.

cpipe has specific support for dotnet exception of "because it is being used by another process." and will retry a task if it captured that message in the output/error. With this feature, it can run almost all dotnet tasks in parallel.
