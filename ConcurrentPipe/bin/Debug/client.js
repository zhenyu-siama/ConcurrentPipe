"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
console.log('node js http client tester');
var http = require("http");
var options = {};
options.host = 'localhost';
options.port = 8776;
var request = http.request(options, function (res) {
    var data = '';
    res.on('data', function (chunk) { return data += chunk; });
    res.on('end', function () {
        if (data == "node js server is working!") {
            console.log('corrrect message from server');
            process.exit();
        }
        else {
            console.error('wrong message is obtained from server!');
            process.exit(1);
        }
    });
});
request.end();
//# sourceMappingURL=client.js.map