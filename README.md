# ConcurrentPipe
concurrent cli tool for team city builds/tests pipeline. it make team city pipelines organize in proper serial and parallel pattern.

cpipe will run cmd tasks in serial and parallel ways

the following example of "concurrent.json" shows how it can orchestrate the serial and parallel tasks
```javascript
{
  "Runners": { // dictionary of runners
    "test": { // this is the key of a runner in the dictionary. use "cpipe test" in cmd to run only this runner
      "Name": "Basic Tests",
      "Timeout": 900, // the timeout for this runner
      "Commands": [ // the command lines that will be run in parallel, the commands run before the subrunners
        "dir",
        "npmc"
      ],
      "Alives": [ // services that need to be kept alive while the subrunners are running.
        "node server"
      ],
      "ReadyChecks": [
        "Node Server is Listening at 8776" // regex text to check the output of "alive" service. if any match is hit, it means the service is ready.
      ],
      "Runners": [ // sub runners that are also run in parallel
        {
          "Name": "client 1",
          "Timeout": 900,
          "Commands": [
            "node client"
          ],
          "Runners": [
            {
              "Name": "client error 1",
              "Timeout": 900,
              "Commands": [
                "node client"
              ]
            },
            {
              "Name": "client 2",
              "Timeout": 900,
              "Commands": [
                "node client"
              ],
              "Runners": [ // add child runner to a runner will result in serial tasks, this will not run before the parent commands have completed.
                {
                  "Name": "client 3",
                  "Timeout": 900,
                  "Commands": [
                    "node client"
                  ],
                  "Runners": [
                  ]
                }
              ]
            },
            {
              "Name": "client error 2",
              "Timeout": 900,
              "Commands": [
                "node client"
              ]
            }
          ]
        }
      ]
    },
    "build": { // this runner will run after the first one is done, if no parameters are specified in the command line for cpipe
      "Name": "ng builds in parallel",
      "Timeout": 900,
      "Commands": [
        "ng build",
        "ng build --prod --aot=false --environment=staging --output-path=dist/staging"
      ]
    }
  }
}

```

to run the "test" section, simply use:
```
cpipe test
```

if you want to run all the sections, just use:
```
cpipe
```
